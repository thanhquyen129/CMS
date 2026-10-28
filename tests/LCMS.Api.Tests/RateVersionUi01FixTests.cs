using System.Text.Json;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.PricingRules.Commands;
using LCMS.Application.RateCards;
using LCMS.Application.RateCards.Commands;
using LCMS.Application.RateCards.Queries;
using LCMS.Application.RateVersions.Commands;
using LCMS.Application.RateVersions.Queries;
using LCMS.Application.Ratings;
using LCMS.Application.Ratings.Commands;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using LCMS.Domain.Pricing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class RateVersionUi01FixTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public RateVersionUi01FixTests(LcmsApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeDatabaseAsync();
        _client = _factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    private IServiceScope CreateTenantScope(Guid tenantId, Guid? userId = null)
    {
        var scope = _factory.Services.CreateScope();
        var tc = scope.ServiceProvider.GetRequiredService<LCMS.Infrastructure.Tenancy.HttpTenantContext>();
        tc.TenantId = tenantId;
        return scope;
    }

    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
        var tenant = new Tenant
        {
            Code = code,
            Name = name,
            IsActive = true
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(CancellationToken.None);
        await LCMS.Application.Catalog.TransportModeCatalog.EnsureBaselineAsync(db, tenant.Id, CancellationToken.None);
        return tenant.Id;
    }

    private async Task<Guid> CreatePartyAsync(Guid tenantId, string code, string name)
    {
        using var scope = CreateTenantScope(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
        var party = new BusinessParty
        {
            TenantId = tenantId,
            Code = code,
            Name = name,
            LegalName = name,
            IsActive = true
        };
        db.BusinessParties.Add(party);
        await db.SaveChangesAsync(CancellationToken.None);
        return party.Id;
    }

    private async Task<Guid> CreateBillAsync(Guid tenantId, string billNo, Guid customerId, string mode = "air")
    {
        using var scope = CreateTenantScope(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
        var bill = new Bill
        {
            TenantId = tenantId,
            BillNo = billNo,
            BillType = "house",
            CustomerPartyId = customerId,
            TransportMode = mode,
            RouteCode = "SGN-HAN",
            ServiceTypeCode = "FREIGHT",
            IsActive = true,
            OperationalStatus = "active",
            SourceSystem = OperationalSourceSystems.LcmsManual
        };
        db.Bills.Add(bill);
        await db.SaveChangesAsync(CancellationToken.None);
        return bill.Id;
    }

    private async Task<(Guid cardId, Guid v1Id)> CreatePublishedCardV1Async(Guid tenantId, string code, decimal price = 12000m)
    {
        using var scope = CreateTenantScope(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
        var vendor = new BusinessParty
        {
            TenantId = tenantId,
            Code = "VND-" + code,
            Name = "Vendor " + code,
            LegalName = "Vendor " + code,
            IsActive = true
        };
        db.BusinessParties.Add(vendor);
        db.PartyRoles.Add(new PartyRole
        {
            TenantId = tenantId,
            PartyId = vendor.Id,
            RoleCode = "vendor"
        });

        var card = new RateCard
        {
            TenantId = tenantId,
            Code = code,
            Name = "Bảng giá " + code,
            PartyType = "vendor",
            SupplierPartyId = vendor.Id,
            CurrencyCode = "VND",
            DeclarationMode = "basic",
            TransportMode = "air",
            RouteCode = "SGN-HAN",
            CarrierName = "VN Cargo",
            IsActive = true
        };
        db.RateCards.Add(card);

        var v1 = new RateVersion
        {
            TenantId = tenantId,
            RateCardId = card.Id,
            VersionNo = 1,
            Status = RateVersionStatuses.Published,
            PublishedAt = DateTimeOffset.UtcNow,
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
            VatRate = 8m
        };
        db.RateVersions.Add(v1);

        var rule = new PricingRule
        {
            TenantId = tenantId,
            RateVersionId = v1.Id,
            Code = "QT0001",
            Name = "SGN-HAN",
            CalcMethod = PricingCalcMethods.UnitRate,
            UnitAmount = price,
            CurrencyCode = "VND",
            ServiceTypeCode = "FREIGHT",
            TransportMode = "air",
            RouteCode = "SGN-HAN",
            WeightBasis = "chargeable",
            WeightMin = 1m,
            WeightMax = 120m,
            ChargeCode = "FREIGHT",
            IsActive = true
        };
        db.PricingRules.Add(rule);

        var comp = new PricingRuleComponent
        {
            TenantId = tenantId,
            PricingRuleId = rule.Id,
            Code = "FREIGHT",
            Name = "Freight",
            FinancialNature = "cost",
            CostTypeCode = "FREIGHT",
            Amount = price,
            CurrencyCode = "VND"
        };
        db.PricingRuleComponents.Add(comp);

        await db.SaveChangesAsync(CancellationToken.None);
        return (card.Id, v1.Id);
    }

    [Fact]
    public async Task UI01_To_UI03_CloneDraftV2_SameCardId_NewVersionId_V1RemainsPublished()
    {
        var tenantId = await CreateTenantAsync("TN-UI01", "Tenant UI01");
        var (cardId, v1Id) = await CreatePublishedCardV1Async(tenantId, "TDNT_SGN_HAN", 12000m);

        Guid v2Id;
        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            v2Id = await sender.Send(new CreateRateVersionCommand(cardId, DateTimeOffset.UtcNow, null, "v2 draft", CloneFromVersionId: v1Id));
        }

        Assert.NotEqual(Guid.Empty, v2Id);
        Assert.NotEqual(v1Id, v2Id);

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var v1 = await db.RateVersions.FirstAsync(v => v.Id == v1Id);
            Assert.Equal(RateVersionStatuses.Published, v1.Status);
            Assert.Equal(1, v1.VersionNo);
            Assert.Equal(cardId, v1.RateCardId);

            var v2 = await db.RateVersions.FirstAsync(v => v.Id == v2Id);
            Assert.Equal(RateVersionStatuses.Draft, v2.Status);
            Assert.Equal(2, v2.VersionNo);
            Assert.Equal(cardId, v2.RateCardId); // Same RateCard ID!

            var card = await db.RateCards.FirstAsync(c => c.Id == cardId);
            Assert.Equal("TDNT_SGN_HAN", card.Code); // RateCard code preserved!
        }
    }

    [Fact]
    public async Task UI04_Clone_PreservesAllBasicFieldsAndRules()
    {
        var tenantId = await CreateTenantAsync("TN-UI04", "Tenant UI04");
        var (cardId, v1Id) = await CreatePublishedCardV1Async(tenantId, "TDNT_SGN_HAN", 12000m);

        Guid v2Id;
        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            v2Id = await sender.Send(new CreateRateVersionCommand(cardId, null, null, null, CloneFromVersionId: v1Id));
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var v2 = await db.RateVersions.FirstAsync(v => v.Id == v2Id);
            Assert.Equal(8m, v2.VatRate); // VAT preserved

            var v2Rules = await db.PricingRules.Where(r => r.RateVersionId == v2Id).ToListAsync();
            Assert.Single(v2Rules);
            var r = v2Rules[0];
            Assert.Equal("QT0001", r.Code);
            Assert.Equal("SGN-HAN", r.Name);
            Assert.Equal(12000m, r.UnitAmount);
            Assert.Equal("VND", r.CurrencyCode);
            Assert.Equal("FREIGHT", r.ServiceTypeCode);
            Assert.Equal("chargeable", r.WeightBasis);
            Assert.Equal(1m, r.WeightMin);
            Assert.Equal(120m, r.WeightMax);
            Assert.Equal("SGN-HAN", r.RouteCode);
            var card = await db.RateCards.FirstAsync(c => c.Id == cardId);
            Assert.Equal("VN Cargo", card.CarrierName);
            Assert.Equal("basic", card.DeclarationMode);
            Assert.Equal("FREIGHT", r.ChargeCode);

            var v2Comps = await db.PricingRuleComponents.Where(c => c.PricingRuleId == r.Id).ToListAsync();
            Assert.Single(v2Comps);
            Assert.Equal(12000m, v2Comps[0].Amount);
            Assert.Equal("cost", v2Comps[0].FinancialNature);
        }
    }

    [Fact]
    public async Task UI05_EditDraftV2_ExpandWeightBandTo200_ReloadPreservesChanges_V1Intact()
    {
        var tenantId = await CreateTenantAsync("TN-UI05", "Tenant UI05");
        var (cardId, v1Id) = await CreatePublishedCardV1Async(tenantId, "TDNT_SGN_HAN", 12000m);

        Guid v2Id;
        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            v2Id = await sender.Send(new CreateRateVersionCommand(cardId, null, null, null, CloneFromVersionId: v1Id));
        }

        // Edit v2: Expand weight band max from 120 to 200 kg
        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var r = await db.PricingRules.FirstAsync(x => x.RateVersionId == v2Id);
            r.WeightMax = 200m;
            await db.SaveChangesAsync(CancellationToken.None);
        }

        // Verify v2 has [1, 200) and v1 still has [1, 120)
        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var r1 = await db.PricingRules.FirstAsync(x => x.RateVersionId == v1Id);
            Assert.Equal(120m, r1.WeightMax);

            var r2 = await db.PricingRules.FirstAsync(x => x.RateVersionId == v2Id);
            Assert.Equal(200m, r2.WeightMax);
        }
    }

    [Fact]
    public async Task UI08_Clone_AdvancedMode_PreservesMatrixGroups()
    {
        var tenantId = await CreateTenantAsync("TN-UI08", "Tenant UI08");
        using var scope = CreateTenantScope(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();

        var card = new RateCard
        {
            TenantId = tenantId,
            Code = "RC_ADV_01",
            Name = "Bảng giá nâng cao",
            PartyType = "vendor",
            CurrencyCode = "VND",
            DeclarationMode = "advanced",
            IsActive = true
        };
        db.RateCards.Add(card);

        var v1 = new RateVersion
        {
            TenantId = tenantId,
            RateCardId = card.Id,
            VersionNo = 1,
            Status = RateVersionStatuses.Published,
            PublishedAt = DateTimeOffset.UtcNow,
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1)
        };
        db.RateVersions.Add(v1);

        var matrixRule = new PricingRule
        {
            TenantId = tenantId,
            RateVersionId = v1.Id,
            Code = "MTR_01",
            Name = "Ma trận SGN-HAN",
            CalcMethod = PricingCalcMethods.WeightStep,
            UnitAmount = 15000m,
            CurrencyCode = "VND",
            ChargeCode = "FREIGHT",
            MatrixGroupCode = "GRP-01",
            IsActive = true
        };
        db.PricingRules.Add(matrixRule);

        var brk = new RateBreak
        {
            TenantId = tenantId,
            PricingRuleId = matrixRule.Id,
            SequenceNo = 1,
            MinQuantity = 1m,
            MaxQuantity = 50m,
            UnitAmount = 15000m
        };
        db.RateBreaks.Add(brk);
        await db.SaveChangesAsync(CancellationToken.None);

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var v2Id = await sender.Send(new CreateRateVersionCommand(card.Id, null, null, null, CloneFromVersionId: v1.Id));

        var v2Rules = await db.PricingRules.Where(r => r.RateVersionId == v2Id).ToListAsync();
        Assert.Single(v2Rules);
        Assert.Equal("GRP-01", v2Rules[0].MatrixGroupCode);
        Assert.Equal(PricingCalcMethods.WeightStep, v2Rules[0].CalcMethod);

        var v2Breaks = await db.RateBreaks.Where(b => b.PricingRuleId == v2Rules[0].Id).ToListAsync();
        Assert.Single(v2Breaks);
        Assert.Equal(15000m, v2Breaks[0].UnitAmount);
    }

    [Fact]
    public async Task UI09_Publish_OverlappingBands_BlockedWithConflictMessage()
    {
        var tenantId = await CreateTenantAsync("TN-UI09", "Tenant UI09");
        var (cardId, v1Id) = await CreatePublishedCardV1Async(tenantId, "TDNT_SGN_HAN", 12000m);

        Guid v2Id;
        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            v2Id = await sender.Send(new CreateRateVersionCommand(cardId, null, null, null, CloneFromVersionId: v1Id));
        }

        // Add overlapping rule on v2: [50, 150) overlaps with [1, 120) on same charge and route
        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var overlapRule = new PricingRule
            {
                TenantId = tenantId,
                RateVersionId = v2Id,
                Code = "QT0002",
                Name = "SGN-HAN Overlap",
                CalcMethod = PricingCalcMethods.UnitRate,
                UnitAmount = 14000m,
                CurrencyCode = "VND",
                ServiceTypeCode = "FREIGHT",
                TransportMode = "air",
                RouteCode = "SGN-HAN",
                WeightBasis = "chargeable",
                WeightMin = 50m,
                WeightMax = 150m,
                ChargeCode = "FREIGHT",
                IsActive = true
            };
            db.PricingRules.Add(overlapRule);
            await db.SaveChangesAsync(CancellationToken.None);
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var ex = await Assert.ThrowsAsync<ConflictAppException>(() =>
                sender.Send(new PublishRateVersionCommand(v2Id)));
            Assert.Equal(RateRuleConflicts.Code, ex.ErrorCode);
            Assert.Contains("RATE_CONFLICT", ex.Message);
        }

        // v1 remains usable and untouched
        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var v1 = await db.RateVersions.FirstAsync(v => v.Id == v1Id);
            Assert.Equal(RateVersionStatuses.Published, v1.Status);
        }
    }

    [Fact]
    public async Task UI10_To_UI12_PublishV2_RatingCW120MatchesV2_SnapshotsImmutable()
    {
        var tenantId = await CreateTenantAsync("TN-UI10", "Tenant UI10");
        var (cardId, v1Id) = await CreatePublishedCardV1Async(tenantId, "TDNT_SGN_HAN", 12000m);

        var custId = await CreatePartyAsync(tenantId, "CUST-UI10", "Cust UI10");
        var billId = await CreateBillAsync(tenantId, "BILL-UI10", custId);

        // 1. Rating with CW = 120 under v1 fails (half open interval [1, 120))
        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var ex = await Assert.ThrowsAsync<RatingNotReadyAppException>(() =>
                sender.Send(new CreateRatingCommand(billId, v1Id, 120m, 120m, "FREIGHT", null, "SGN-HAN", null, null, TransportMode: "air")));
            Assert.Contains(ex.Missing, f => f.Field == "weight_band");
        }

        // 2. Rating with CW = 100 under v1 succeeds (snapshot created)
        Guid snap1Id;
        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            snap1Id = await sender.Send(new CreateRatingCommand(billId, v1Id, 100m, 100m, "FREIGHT", null, "SGN-HAN", null, null, TransportMode: "air"));
            Assert.NotEqual(Guid.Empty, snap1Id);
        }

        // 3. Clone to v2, expand weight band to [1, 200) and publish
        Guid v2Id;
        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            v2Id = await sender.Send(new CreateRateVersionCommand(cardId, DateTimeOffset.UtcNow, null, "v2 200kg", CloneFromVersionId: v1Id));
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var r = await db.PricingRules.FirstAsync(x => x.RateVersionId == v2Id);
            r.WeightMax = 200m;
            await db.SaveChangesAsync(CancellationToken.None);
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new PublishRateVersionCommand(v2Id));
        }

        // 4. Rating with CW = 120 under v2 succeeds!
        Guid snap2Id;
        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            snap2Id = await sender.Send(new CreateRatingCommand(billId, v2Id, 120m, 120m, "FREIGHT", null, "SGN-HAN", null, null, TransportMode: "air"));
            Assert.NotEqual(Guid.Empty, snap2Id);
        }

        // 5. Verify v1 snapshot is unchanged (immutable)
        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var s1 = await db.Ratings.FirstAsync(s => s.Id == snap1Id);
            Assert.Equal(v1Id, s1.RateVersionId);
            Assert.Equal(100m, s1.Weight);

            var s2 = await db.Ratings.FirstAsync(s => s.Id == snap2Id);
            Assert.Equal(v2Id, s2.RateVersionId);
            Assert.Equal(120m, s2.Weight);
        }
    }

    [Fact]
    public async Task UI13_DuplicateClickGuard_ReturnsExistingDraft()
    {
        var tenantId = await CreateTenantAsync("TN-UI13", "Tenant UI13");
        var (cardId, v1Id) = await CreatePublishedCardV1Async(tenantId, "TDNT_SGN_HAN", 12000m);

        Guid v2IdFirst;
        Guid v2IdSecond;

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            v2IdFirst = await sender.Send(new CreateRateVersionCommand(cardId, null, null, null, CloneFromVersionId: v1Id));
            // Second click while draft exists returns same ID
            v2IdSecond = await sender.Send(new CreateRateVersionCommand(cardId, null, null, null, CloneFromVersionId: v1Id));
        }

        Assert.Equal(v2IdFirst, v2IdSecond);

        // Verify only ONE draft version exists
        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var drafts = await db.RateVersions.Where(v => v.RateCardId == cardId && v.Status == RateVersionStatuses.Draft).ToListAsync();
            Assert.Single(drafts);
        }
    }

    [Fact]
    public async Task Appendix_OrphanVersionsOfDeletedCards_AreFilteredOut()
    {
        var tenantId = await CreateTenantAsync("TN-ORPH", "Tenant ORPH");
        var (cardId, v1Id) = await CreatePublishedCardV1Async(tenantId, "TDNT_DEL_TEST", 12000m);

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var list = await sender.Send(new ListRateAppendicesQuery());
            Assert.Contains(list, x => x.CardCode == "TDNT_DEL_TEST");
        }

        // Soft-delete the card
        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var card = await db.RateCards.FirstAsync(c => c.Id == cardId);
            card.SoftDelete(null);
            await db.SaveChangesAsync(CancellationToken.None);
        }

        // Now query appendices: orphan version of deleted card must NOT be returned!
        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var list = await sender.Send(new ListRateAppendicesQuery());
            Assert.DoesNotContain(list, x => x.CardCode == "TDNT_DEL_TEST");
            Assert.DoesNotContain(list, x => string.IsNullOrEmpty(x.CardCode));
        }
    }
}
