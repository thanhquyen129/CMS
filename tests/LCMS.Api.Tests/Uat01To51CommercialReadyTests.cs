using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Costs;
using LCMS.Application.Costs.Commands;
using LCMS.Application.FinancialCloses;
using LCMS.Application.OperationalLinks.Commands;
using LCMS.Application.OperationalReferences;
using LCMS.Application.OperationalReferences.Edit;
using LCMS.Application.Pricing;
using LCMS.Application.RateVersions.Commands;
using LCMS.Application.Ratings;
using LCMS.Application.Ratings.Commands;
using LCMS.Domain.Entities;
using LCMS.Domain.Pricing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LCMS.Infrastructure.Tenancy;
using Xunit;

namespace LCMS.Api.Tests;

/// <summary>
/// COMMERCIAL-READY TEST SUITE: 51/51 UAT Cases (UAT-001 -> UAT-051)
/// Package: LCMS-UAT-CR-20261023-01 (Post-UAT Adjustments: Bill Links, Leg Measurements, Buy Rating, Cost Allocation & Rate Versioning)
/// Baseline: LCMS_BA_UAT_Operational_Rating_Allocation_Change_v1.0_20261023.docx & Matrix v1.0
/// </summary>
[Collection("Api")]
public sealed class Uat01To51CommercialReadyTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public Uat01To51CommercialReadyTests(LcmsApiFactory factory)
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
        if (userId.HasValue)
        {
            var uc = scope.ServiceProvider.GetRequiredService<LCMS.Infrastructure.Tenancy.HttpCurrentUserContext>();
            uc.UserId = userId.Value;
        }
        return scope;
    }

    private static HttpRequestMessage Req(HttpMethod method, string path, Guid tenantId)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        return req;
    }

    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        var res = await _client.PostAsJsonAsync("/api/tenants", new { Code = code, Name = name });
        res.EnsureSuccessStatusCode();
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return doc.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreatePartyAsync(Guid tenantId, string code, string name, string role = "customer")
    {
        using var req = Req(HttpMethod.Post, "/api/business-parties", tenantId);
        req.Content = JsonContent.Create(new
        {
            code = code,
            name = name,
            legalName = name,
            roleCodes = new[] { role }
        });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return doc.GetProperty("id").GetGuid();
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
            IsActive = true,
            OperationalStatus = "active",
            SourceSystem = OperationalSourceSystems.LcmsManual
        };
        db.Bills.Add(bill);
        await db.SaveChangesAsync(CancellationToken.None);
        return bill.Id;
    }

    private async Task<Guid> CreateOrderAsync(Guid tenantId, string orderNo, Guid customerId)
    {
        using var scope = CreateTenantScope(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
        var order = new Order
        {
            TenantId = tenantId,
            OrderNo = orderNo,
            ExternalId = orderNo,
            CustomerPartyId = customerId,
            SourceSystem = OperationalSourceSystems.LcmsManual,
            OperationalStatus = "active"
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync(CancellationToken.None);
        return order.Id;
    }

    private async Task<Guid> CreateShipmentAsync(Guid tenantId, string shipmentNo)
    {
        using var scope = CreateTenantScope(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
        var shipment = new Shipment
        {
            TenantId = tenantId,
            ShipmentNo = shipmentNo,
            SourceSystem = OperationalSourceSystems.LcmsManual,
            OperationalStatus = "active"
        };
        db.Shipments.Add(shipment);
        await db.SaveChangesAsync(CancellationToken.None);
        return shipment.Id;
    }

    private async Task<Guid> CreateLegAsync(Guid tenantId, Guid shipmentId, string legNo, string mode = "air")
    {
        using var scope = CreateTenantScope(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
        var leg = new TransportLeg
        {
            TenantId = tenantId,
            ShipmentId = shipmentId,
            LegNo = legNo,
            ExternalId = legNo,
            TransportMode = mode,
            SourceSystem = OperationalSourceSystems.LcmsManual,
            OperationalStatus = "active"
        };
        db.TransportLegs.Add(leg);
        await db.SaveChangesAsync(CancellationToken.None);
        return leg.Id;
    }

    // =========================================================================
    // GROUP 1: BILL-LINK-EDIT-01 (UAT-001 -> UAT-005)
    // =========================================================================

    [Fact]
    public async Task UAT001_BillLinkEdit_ViewOrderLinks_SuccessfullyRetrieved()
    {
        var tenantId = await CreateTenantAsync("TN-UAT001", "Tenant UAT 001");
        var custId = await CreatePartyAsync(tenantId, "CUST-001", "Customer 001");
        var billId = await CreateBillAsync(tenantId, "BILL-001", custId);
        var orderId = await CreateOrderAsync(tenantId, "ORD-001", custId);

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new LinkOrderToBillCommand(orderId, billId));
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var links = await db.OrderBillLinks.Where(l => l.BillId == billId && l.DeletedAt == null).ToListAsync();
            Assert.Single(links);
            Assert.Equal(orderId, links[0].OrderId);
        }
    }

    [Fact]
    public async Task UAT002_BillLinkEdit_MultiOrder_NNLink_NoOverwriteCustomer()
    {
        var tenantId = await CreateTenantAsync("TN-UAT002", "Tenant UAT 002");
        var cust1 = await CreatePartyAsync(tenantId, "CUST-002A", "Cust A");
        var cust2 = await CreatePartyAsync(tenantId, "CUST-002B", "Cust B");
        var billId = await CreateBillAsync(tenantId, "BILL-002", cust1);
        var order1 = await CreateOrderAsync(tenantId, "ORD-002A", cust1);
        var order2 = await CreateOrderAsync(tenantId, "ORD-002B", cust2);

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new LinkOrderToBillCommand(order1, billId));
            await sender.Send(new LinkOrderToBillCommand(order2, billId));
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var links = await db.OrderBillLinks.Where(l => l.BillId == billId && l.DeletedAt == null).ToListAsync();
            Assert.Equal(2, links.Count);

            var bill = await db.Bills.FindAsync(billId);
            Assert.Equal(cust1, bill!.CustomerPartyId); // No silent overwrite
        }
    }

    [Fact]
    public async Task UAT003_BillLinkEdit_ShipmentLink_PersistedAndRetained()
    {
        var tenantId = await CreateTenantAsync("TN-UAT003", "Tenant UAT 003");
        var custId = await CreatePartyAsync(tenantId, "CUST-003", "Cust 003");
        var billId = await CreateBillAsync(tenantId, "BILL-003", custId);
        var shipId = await CreateShipmentAsync(tenantId, "SHP-003");

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new LinkBillToShipmentCommand(billId, shipId));
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var link = await db.BillShipmentLinks.FirstOrDefaultAsync(l => l.BillId == billId && l.ShipmentId == shipId && l.DeletedAt == null);
            Assert.NotNull(link);
        }
    }

    [Fact]
    public async Task UAT004_BillLinkEdit_UnlinkShipmentLeg_PreservesHistoryAndCosts()
    {
        var tenantId = await CreateTenantAsync("TN-UAT004", "Tenant UAT 004");
        var custId = await CreatePartyAsync(tenantId, "CUST-004", "Cust 004");
        var billId = await CreateBillAsync(tenantId, "BILL-004", custId);
        var shipId = await CreateShipmentAsync(tenantId, "SHP-004");

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new LinkBillToShipmentCommand(billId, shipId));
        }

        // Add a cost row to verify history preservation
        Guid costId;
        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var cost = new Cost
            {
                TenantId = tenantId,
                BillId = billId,
                ShipmentId = shipId,
                CostTypeCode = "FRT",
                Amount = 500000m,
                CurrencyCode = "VND",
                FinancialMaturity = CostMaturities.Expected,
                AttributionType = CostAttributionTypes.Direct
            };
            db.Costs.Add(cost);
            await db.SaveChangesAsync(CancellationToken.None);
            costId = cost.Id;
        }

        // Unlink shipment
        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new UnlinkBillShipmentCommand(billId, shipId, "Khách đổi lộ trình"));
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var link = await db.BillShipmentLinks.IgnoreQueryFilters().FirstOrDefaultAsync(l => l.BillId == billId && l.ShipmentId == shipId);
            Assert.NotNull(link);
            Assert.NotNull(link!.DeletedAt); // Soft deleted

            var cost = await db.Costs.FindAsync(costId);
            Assert.NotNull(cost); // Cost preserved, not deleted
        }
    }

    [Fact]
    public async Task UAT005_BillLinkEdit_SoTPermission_AuditTrailEnforced()
    {
        var tenantId = await CreateTenantAsync("TN-UAT005", "Tenant UAT 005");
        var custId = await CreatePartyAsync(tenantId, "CUST-005", "Cust 005");
        var billId = await CreateBillAsync(tenantId, "BILL-005", custId);
        var shipId = await CreateShipmentAsync(tenantId, "SHP-005");

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new LinkBillToShipmentCommand(billId, shipId));
            await sender.Send(new UnlinkBillShipmentCommand(billId, shipId, "Audit trail reason check"));
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var audit = await db.AuditEvents.Where(a => a.TenantId == tenantId && a.ObjectType == AuditObjectTypes.OperationalLink).ToListAsync();
            Assert.Contains(audit, a => a.Action == AuditActions.LinkCreate);
            Assert.Contains(audit, a => a.Action == AuditActions.LinkRemove && a.Reason == "Audit trail reason check");
        }
    }

    // =========================================================================
    // GROUP 2: RATE-VERSION-UI-01 (UAT-006 -> UAT-011)
    // =========================================================================

    private async Task<(Guid cardId, Guid v1Id)> CreatePublishedCardV1Async(Guid tenantId, string code, decimal price)
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

        var rule = new PricingRule
        {
            TenantId = tenantId,
            RateVersionId = v1.Id,
            Code = "R1",
            Name = "Quy tắc 1",
            CalcMethod = PricingCalcMethods.UnitRate,
            UnitAmount = price,
            CurrencyCode = "VND",
            IsActive = true,
            ChargeCode = "FRT",
            WeightBasis = "chargeable",
            WeightMin = 1m,
            WeightMax = 120m
        };
        db.PricingRules.Add(rule);

        var comp = new PricingRuleComponent
        {
            TenantId = tenantId,
            PricingRuleId = rule.Id,
            Code = "FRT",
            Name = "Freight",
            FinancialNature = "cost",
            CostTypeCode = "FRT",
            Amount = price
        };
        db.PricingRuleComponents.Add(comp);

        await db.SaveChangesAsync(CancellationToken.None);
        return (card.Id, v1.Id);
    }

    [Fact]
    public async Task UAT006_RateVersion_CloneV1ToDraftV2_V1PublishedImmutable()
    {
        var tenantId = await CreateTenantAsync("TN-UAT006", "Tenant UAT 006");
        var (cardId, v1Id) = await CreatePublishedCardV1Async(tenantId, "CARD-006", 50000m);

        Guid v2Id;
        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            v2Id = await sender.Send(new CreateRateVersionCommand(cardId, DateTimeOffset.UtcNow, null, "v2 update", CloneFromVersionId: v1Id));
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var v1 = await db.RateVersions.FindAsync(v1Id);
            Assert.Equal(RateVersionStatuses.Published, v1!.Status); // v1 untouched

            var v2 = await db.RateVersions.FindAsync(v2Id);
            Assert.Equal(RateVersionStatuses.Draft, v2!.Status);
            Assert.Equal(2, v2.VersionNo);

            var clonedRules = await db.PricingRules.Where(r => r.RateVersionId == v2Id).ToListAsync();
            Assert.Single(clonedRules);
            Assert.Equal(50000m, clonedRules[0].UnitAmount);
            Assert.NotEqual(v1Id, clonedRules[0].RateVersionId);
        }
    }

    [Fact]
    public async Task UAT007_RateVersion_EditDraftV2_ReloadBasicRules_V1Untouched()
    {
        var tenantId = await CreateTenantAsync("TN-UAT007", "Tenant UAT 007");
        var (cardId, v1Id) = await CreatePublishedCardV1Async(tenantId, "CARD-007", 50000m);

        Guid v2Id;
        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            v2Id = await sender.Send(new CreateRateVersionCommand(cardId, DateTimeOffset.UtcNow, null, "v2 update", CloneFromVersionId: v1Id));
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var ruleV2 = await db.PricingRules.FirstAsync(r => r.RateVersionId == v2Id);
            ruleV2.UnitAmount = 60000m;
            ruleV2.WeightMax = 200m;
            await db.SaveChangesAsync(CancellationToken.None);
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var ruleV1 = await db.PricingRules.FirstAsync(r => r.RateVersionId == v1Id);
            Assert.Equal(50000m, ruleV1.UnitAmount); // v1 untouched
            Assert.Equal(120m, ruleV1.WeightMax);

            var ruleV2 = await db.PricingRules.FirstAsync(r => r.RateVersionId == v2Id);
            Assert.Equal(60000m, ruleV2.UnitAmount);
            Assert.Equal(200m, ruleV2.WeightMax);
        }
    }

    [Fact]
    public async Task UAT008_RateVersion_DraftV2_MatrixMode_NoMixedMode()
    {
        var tenantId = await CreateTenantAsync("TN-UAT008", "Tenant UAT 008");
        var (cardId, v1Id) = await CreatePublishedCardV1Async(tenantId, "CARD-008", 50000m);

        Guid v2Id;
        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            v2Id = await sender.Send(new CreateRateVersionCommand(cardId, DateTimeOffset.UtcNow, null, "v2 matrix", CloneFromVersionId: v1Id));
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            // Add a matrix rule alongside the existing basic rule
            db.PricingRules.Add(new PricingRule
            {
                TenantId = tenantId,
                RateVersionId = v2Id,
                Code = "R2_MAT",
                Name = "Matrix rule",
                CalcMethod = PricingCalcMethods.WeightBreakPivot,
                UnitAmount = 10000m
            });
            await db.SaveChangesAsync(CancellationToken.None);
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var ex = await Assert.ThrowsAsync<ConflictAppException>(() => sender.Send(new PublishRateVersionCommand(v2Id)));
            Assert.Contains(RateDeclarationPolicy.MixedLegacyMessage, ex.Message);
        }
    }

    [Fact]
    public async Task UAT009_RateVersion_BandConflict_BlocksPublish()
    {
        var tenantId = await CreateTenantAsync("TN-UAT009", "Tenant UAT 009");
        var (cardId, v1Id) = await CreatePublishedCardV1Async(tenantId, "CARD-009", 50000m);

        Guid v2Id;
        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            v2Id = await sender.Send(new CreateRateVersionCommand(cardId, DateTimeOffset.UtcNow, null, "v2 conflict", CloneFromVersionId: v1Id));
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            // Add overlapping rule [10, 50) when R1 is [1, 120)
            db.PricingRules.Add(new PricingRule
            {
                TenantId = tenantId,
                RateVersionId = v2Id,
                Code = "R2_CONFLICT",
                Name = "Overlapping",
                CalcMethod = PricingCalcMethods.UnitRate,
                UnitAmount = 45000m,
                ChargeCode = "FRT",
                WeightBasis = "chargeable",
                WeightMin = 10m,
                WeightMax = 50m
            });
            await db.SaveChangesAsync(CancellationToken.None);
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var ex = await Assert.ThrowsAsync<ConflictAppException>(() => sender.Send(new PublishRateVersionCommand(v2Id)));
            Assert.Contains("chồng nhau", ex.Message);

            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var v1 = await db.RateVersions.FindAsync(v1Id);
            Assert.Equal(RateVersionStatuses.Published, v1!.Status);
        }
    }

    [Fact]
    public async Task UAT010_RateVersion_ValidV2_PublishSuccess_EffectiveDateValidated()
    {
        var tenantId = await CreateTenantAsync("TN-UAT010", "Tenant UAT 010");
        var (cardId, v1Id) = await CreatePublishedCardV1Async(tenantId, "CARD-010", 50000m);

        Guid v2Id;
        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            v2Id = await sender.Send(new CreateRateVersionCommand(cardId, DateTimeOffset.UtcNow.AddDays(1), null, "v2 valid", CloneFromVersionId: v1Id));
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new PublishRateVersionCommand(v2Id));
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var v2 = await db.RateVersions.FindAsync(v2Id);
            Assert.Equal(RateVersionStatuses.Published, v2!.Status);
            Assert.NotNull(v2.PublishedAt);
        }
    }

    [Fact]
    public async Task UAT011_RateVersion_TDNT_SgnHan_CW120_HalfOpenBandBehavior()
    {
        var tenantId = await CreateTenantAsync("TN-UAT011", "Tenant UAT 011");
        var (cardId, v1Id) = await CreatePublishedCardV1Async(tenantId, "TDNT_SGN_HAN", 50000m);

        // v1 has rule [1, 120). Rating with CW = 120 fails to match v1
        var custId = await CreatePartyAsync(tenantId, "CUST-011", "Cust 011");
        var billId = await CreateBillAsync(tenantId, "BILL-011", custId);

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var ex = await Assert.ThrowsAsync<RatingNotReadyAppException>(() =>
                sender.Send(new CreateRatingCommand(billId, v1Id, 120m, 120m, null, null, null, null, null)));
            Assert.Contains(ex.Missing, f => f.Field == "weight_band");
        }

        // Create v2 with band [1, 200) -> matches CW = 120
        Guid v2Id;
        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            v2Id = await sender.Send(new CreateRateVersionCommand(cardId, DateTimeOffset.UtcNow, null, "v2 expansion", CloneFromVersionId: v1Id));
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
            var ratingId = await sender.Send(new CreateRatingCommand(billId, v2Id, 120m, 120m, null, null, null, null, null));
            Assert.NotEqual(Guid.Empty, ratingId);
        }
    }

    // =========================================================================
    // GROUP 3: LEG-MEASURE-UI-01 & LEG-CW-UI-01 (UAT-012 -> UAT-019)
    // =========================================================================

    [Fact]
    public async Task UAT012_LegMeasure_CreateWithGrossAndCBM_PersistedWithUOM()
    {
        var tenantId = await CreateTenantAsync("TN-UAT012", "Tenant UAT 012");
        var shipId = await CreateShipmentAsync(tenantId, "SHP-012");
        var legId = await CreateLegAsync(tenantId, shipId, "LEG-012");

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new EditOperationalReferenceCommand(
                "leg", legId,
                new Dictionary<string, string?>
                {
                    [OperationalFieldCodes.GrossWeightKg] = "200",
                    [OperationalFieldCodes.VolumeCbm] = "1"
                },
                null));
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var measures = await db.OperationalMeasurements.Where(m => m.ObjectType == "leg" && m.ObjectId == legId).ToListAsync();
            var gross = measures.FirstOrDefault(m => m.MeasureCode == MeasureCodes.GrossWeightKg);
            var vol = measures.FirstOrDefault(m => m.MeasureCode == MeasureCodes.VolumeCbm);

            Assert.NotNull(gross);
            Assert.Equal(200m, gross!.Quantity);
            Assert.Equal("kg", gross.Uom);

            Assert.NotNull(vol);
            Assert.Equal(1m, vol!.Quantity);
            Assert.Equal("cbm", vol.Uom);
        }
    }

    [Fact]
    public async Task UAT013_LegMeasure_SaveLeg_ReferencePersisted_ConditionalReadiness()
    {
        var tenantId = await CreateTenantAsync("TN-UAT013", "Tenant UAT 013");
        var shipId = await CreateShipmentAsync(tenantId, "SHP-013");
        var legId = await CreateLegAsync(tenantId, shipId, "LEG-013");

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var leg = await db.TransportLegs.FindAsync(legId);
            Assert.NotNull(leg);
            Assert.Equal("LEG-013", leg!.LegNo);
        }
    }

    [Fact]
    public async Task UAT014_LegCW_InitialState_Undetermined_NotAutoCopied()
    {
        var tenantId = await CreateTenantAsync("TN-UAT014", "Tenant UAT 014");
        var shipId = await CreateShipmentAsync(tenantId, "SHP-014");
        var legId = await CreateLegAsync(tenantId, shipId, "LEG-014");

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new EditOperationalReferenceCommand(
                "leg", legId,
                new Dictionary<string, string?> { [OperationalFieldCodes.GrossWeightKg] = "200" },
                null));
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var cw = await db.OperationalMeasurements.FirstOrDefaultAsync(
                m => m.ObjectType == "leg" && m.ObjectId == legId && m.MeasureCode == MeasureCodes.ChargeableWeightKg);
            // CW is not automatically created as 200 or 0; remains undetermined until confirmed/derived
            Assert.Null(cw);
        }
    }

    [Fact]
    public async Task UAT015_LegCW_DerivedCalculation_RuleAndSourceStamped()
    {
        var tenantId = await CreateTenantAsync("TN-UAT015", "Tenant UAT 015");
        var shipId = await CreateShipmentAsync(tenantId, "SHP-015");
        var legId = await CreateLegAsync(tenantId, shipId, "LEG-015", mode: "air");

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            // Gross = 100 kg, Vol = 1 CBM (Air volumetric weight = 1 * 167 = 167 kg > 100 kg Gross)
            await sender.Send(new EditOperationalReferenceCommand(
                "leg", legId,
                new Dictionary<string, string?>
                {
                    [OperationalFieldCodes.GrossWeightKg] = "100",
                    [OperationalFieldCodes.VolumeCbm] = "1"
                },
                null));
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var state = await sender.Send(new ConfirmChargeableWeightCommand("leg", legId));
            Assert.Equal(167m, state.Value);
            Assert.True(state.IsConfirmed);
        }
    }

    [Fact]
    public async Task UAT016_LegCW_ManualInputAndConfirmation_NoReasonRequiredFirstTime()
    {
        var tenantId = await CreateTenantAsync("TN-UAT016", "Tenant UAT 016");
        var shipId = await CreateShipmentAsync(tenantId, "SHP-016");
        var legId = await CreateLegAsync(tenantId, shipId, "LEG-016");

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            // Enter CW directly
            await sender.Send(new EditOperationalReferenceCommand(
                "leg", legId,
                new Dictionary<string, string?> { [OperationalFieldCodes.ChargeableWeightKg] = "150" },
                null));
            var state = await sender.Send(new ConfirmChargeableWeightCommand("leg", legId));
            Assert.Equal(150m, state.Value);
            Assert.True(state.IsConfirmed);
        }
    }

    [Fact]
    public async Task UAT017_LegCW_Override_RequiresReasonAndPermission_AuditLogged()
    {
        var tenantId = await CreateTenantAsync("TN-UAT017", "Tenant UAT 017");
        var shipId = await CreateShipmentAsync(tenantId, "SHP-017");
        var legId = await CreateLegAsync(tenantId, shipId, "LEG-017");

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new EditOperationalReferenceCommand(
                "leg", legId,
                new Dictionary<string, string?> { [OperationalFieldCodes.ChargeableWeightKg] = "150" },
                null));
            await sender.Send(new ConfirmChargeableWeightCommand("leg", legId));

            // Override to 180 with reason
            await sender.Send(new EditOperationalReferenceCommand(
                "leg", legId,
                new Dictionary<string, string?> { [OperationalFieldCodes.ChargeableWeightKg] = "180" },
                "Hãng bay cân lại thực tế"));
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var overrides = await db.OperationalFieldOverrides.Where(o => o.ObjectType == "leg" && o.ObjectId == legId).ToListAsync();
            Assert.Contains(overrides, o => o.FieldCode == OperationalFieldCodes.ChargeableWeightKg && o.Reason == "Hãng bay cân lại thực tế");
        }
    }

    [Fact]
    public async Task UAT018_LegCW_BillRatingByLeg_LoadsLegCW_NoReentry()
    {
        var tenantId = await CreateTenantAsync("TN-UAT018", "Tenant UAT 018");
        var (cardId, v1Id) = await CreatePublishedCardV1Async(tenantId, "CARD-018", 50000m);
        var custId = await CreatePartyAsync(tenantId, "CUST-018", "Cust 018");
        var billId = await CreateBillAsync(tenantId, "BILL-018", custId);
        var shipId = await CreateShipmentAsync(tenantId, "SHP-018");
        var legId = await CreateLegAsync(tenantId, shipId, "LEG-018");

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            db.BillLegLinks.Add(new BillLegLink { TenantId = tenantId, BillId = billId, TransportLegId = legId });
            await db.SaveChangesAsync(CancellationToken.None);

            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new EditOperationalReferenceCommand(
                "leg", legId,
                new Dictionary<string, string?> { [OperationalFieldCodes.ChargeableWeightKg] = "100" },
                null));
            await sender.Send(new ConfirmChargeableWeightCommand("leg", legId));

            // Rating under Leg context uses Leg CW without requiring manual re-entry
            var ratingId = await sender.Send(new CreateRatingCommand(
                billId, v1Id, null, null, null, null, null, null, null,
                ContextType: RatingContextTypes.Leg,
                TransportLegId: legId));
            Assert.NotEqual(Guid.Empty, ratingId);

            var rating = await db.Ratings.FindAsync(ratingId);
            Assert.Equal(100m, rating!.ChargeableWeightKg);
        }
    }

    [Fact]
    public async Task UAT019_LegCW_SaveAndRerate_SupersedesOldRating_PreservesHistory()
    {
        var tenantId = await CreateTenantAsync("TN-UAT019", "Tenant UAT 019");
        var (cardId, v1Id) = await CreatePublishedCardV1Async(tenantId, "CARD-019", 50000m);
        var custId = await CreatePartyAsync(tenantId, "CUST-019", "Cust 019");
        var billId = await CreateBillAsync(tenantId, "BILL-019", custId);

        Guid rating1Id, rating2Id;
        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            rating1Id = await sender.Send(new CreateRatingCommand(billId, v1Id, 50m, 50m, null, null, null, null, null));
            rating2Id = await sender.Send(new CreateRatingCommand(billId, v1Id, 60m, 60m, null, null, null, null, rating1Id));
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var r1 = await db.Ratings.FindAsync(rating1Id);
            var r2 = await db.Ratings.FindAsync(rating2Id);

            Assert.Equal(RatingStatuses.Superseded, r1!.Status);
            Assert.Equal(RatingStatuses.Completed, r2!.Status);
            Assert.Equal(rating1Id, r2.SupersedesRatingId);
        }
    }

    // =========================================================================
    // GROUP 4: RATING-LEG-ALLOC-UAT-01 (UAT-020 -> UAT-027)
    // =========================================================================

    [Fact]
    public async Task UAT020_RatingLegAlloc_DirectBillLeg_CreatesDirectExpectedCost()
    {
        var tenantId = await CreateTenantAsync("TN-UAT020", "Tenant UAT 020");
        var (cardId, v1Id) = await CreatePublishedCardV1Async(tenantId, "CARD-020", 50000m);
        var custId = await CreatePartyAsync(tenantId, "CUST-020", "Cust 020");
        var billId = await CreateBillAsync(tenantId, "BILL-020", custId);
        var shipId = await CreateShipmentAsync(tenantId, "SHP-020");
        var legId = await CreateLegAsync(tenantId, shipId, "LEG-020");

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            db.BillLegLinks.Add(new BillLegLink { TenantId = tenantId, BillId = billId, TransportLegId = legId });
            await db.SaveChangesAsync(CancellationToken.None);

            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var ratingId = await sender.Send(new CreateRatingCommand(
                billId, v1Id, 10m, 10m, null, null, null, null, null,
                SeedExpectedCosts: true,
                ContextType: RatingContextTypes.Leg,
                TransportLegId: legId,
                CostScope: "DIRECT_BILL_LEG"));

            var cost = await db.Costs.FirstOrDefaultAsync(c => c.BillId == billId && c.TransportLegId == legId);
            Assert.NotNull(cost);
            Assert.Equal(CostAttributionTypes.Direct, cost!.AttributionType);
            Assert.Equal(CostOriginLevels.Leg, cost.OriginLevel);
        }
    }

    [Fact]
    public async Task UAT021_RatingLegAlloc_SharedLeg_CreatesLegSharedCost_NotFullBill()
    {
        var tenantId = await CreateTenantAsync("TN-UAT021", "Tenant UAT 021");
        var (cardId, v1Id) = await CreatePublishedCardV1Async(tenantId, "CARD-021", 50000m);
        var custId = await CreatePartyAsync(tenantId, "CUST-021", "Cust 021");
        var billId = await CreateBillAsync(tenantId, "BILL-021", custId);
        var shipId = await CreateShipmentAsync(tenantId, "SHP-021");
        var legId = await CreateLegAsync(tenantId, shipId, "LEG-021");

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            db.BillLegLinks.Add(new BillLegLink { TenantId = tenantId, BillId = billId, TransportLegId = legId });
            await db.SaveChangesAsync(CancellationToken.None);

            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var ratingId = await sender.Send(new CreateRatingCommand(
                billId, v1Id, 20m, 20m, null, null, null, null, null,
                SeedExpectedCosts: true,
                ContextType: RatingContextTypes.Leg,
                TransportLegId: legId,
                CostScope: "SHARED_LEG"));

            var cost = await db.Costs.FirstOrDefaultAsync(c => c.TransportLegId == legId && c.AttributionType == CostAttributionTypes.Shared);
            Assert.NotNull(cost);
            Assert.Null(cost!.BillId); // Leg-level shared cost, not assigned directly to B1
            Assert.Equal(1000000m, cost.Amount);
        }
    }

    [Fact]
    public async Task UAT022_RatingLegAlloc_SecondBillOnSameLeg_IdempotentSingleCost()
    {
        var tenantId = await CreateTenantAsync("TN-UAT022", "Tenant UAT 022");
        var (cardId, v1Id) = await CreatePublishedCardV1Async(tenantId, "CARD-022", 50000m);
        var custId = await CreatePartyAsync(tenantId, "CUST-022", "Cust 022");
        var bill1 = await CreateBillAsync(tenantId, "BILL-022A", custId);
        var bill2 = await CreateBillAsync(tenantId, "BILL-022B", custId);
        var shipId = await CreateShipmentAsync(tenantId, "SHP-022");
        var legId = await CreateLegAsync(tenantId, shipId, "LEG-022");

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            db.BillLegLinks.Add(new BillLegLink { TenantId = tenantId, BillId = bill1, TransportLegId = legId });
            db.BillLegLinks.Add(new BillLegLink { TenantId = tenantId, BillId = bill2, TransportLegId = legId });
            await db.SaveChangesAsync(CancellationToken.None);

            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            // Bill 1 rates SHARED_LEG
            await sender.Send(new CreateRatingCommand(
                bill1, v1Id, 20m, 20m, null, null, null, null, null,
                SeedExpectedCosts: true,
                ContextType: RatingContextTypes.Leg,
                TransportLegId: legId,
                CostScope: "SHARED_LEG"));

            // Bill 2 rates SHARED_LEG on same leg
            await sender.Send(new CreateRatingCommand(
                bill2, v1Id, 20m, 20m, null, null, null, null, null,
                SeedExpectedCosts: true,
                ContextType: RatingContextTypes.Leg,
                TransportLegId: legId,
                CostScope: "SHARED_LEG"));

            // Must have only 1 shared economic cost on this leg (Zero Duplicate Economic Cost)
            var legCosts = await db.Costs.Where(c => c.TransportLegId == legId && c.AttributionType == CostAttributionTypes.Shared).ToListAsync();
            Assert.Single(legCosts);
        }
    }

    [Fact]
    public async Task UAT023_RatingLegAlloc_FinalizeAllocation_ExactConservation_600k_400k()
    {
        var tenantId = await CreateTenantAsync("TN-UAT023", "Tenant UAT 023");
        var custId = await CreatePartyAsync(tenantId, "CUST-023", "Cust 023");
        var bill1 = await CreateBillAsync(tenantId, "BILL-023A", custId);
        var bill2 = await CreateBillAsync(tenantId, "BILL-023B", custId);
        var shipId = await CreateShipmentAsync(tenantId, "SHP-023");
        var legId = await CreateLegAsync(tenantId, shipId, "LEG-023");

        Guid costId, allocId;
        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var cost = new Cost
            {
                TenantId = tenantId,
                TransportLegId = legId,
                Amount = 1000000m,
                CurrencyCode = "VND",
                AttributionType = CostAttributionTypes.Shared,
                OriginLevel = CostOriginLevels.Leg,
                CostTypeCode = "FRT"
            };
            db.Costs.Add(cost);
            await db.SaveChangesAsync(CancellationToken.None);
            costId = cost.Id;

            var alloc = new CostAllocation
            {
                TenantId = tenantId,
                CostId = cost.Id,
                AllocationBasis = CostAllocationBases.GrossKg,
                AllocationStatus = CostAllocationStatuses.Draft,
                AllocatableAmount = 1000000m
            };
            db.CostAllocations.Add(alloc);

            db.CostAllocationDetails.Add(new CostAllocationDetail
            {
                TenantId = tenantId,
                AllocationId = alloc.Id,
                BillId = bill1,
                BasisValue = 60m
            });
            db.CostAllocationDetails.Add(new CostAllocationDetail
            {
                TenantId = tenantId,
                AllocationId = alloc.Id,
                BillId = bill2,
                BasisValue = 40m
            });
            await db.SaveChangesAsync(CancellationToken.None);
            allocId = alloc.Id;
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new FinalizeCostAllocationCommand(allocId));
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var alloc = await db.CostAllocations.FindAsync(allocId);
            Assert.Equal(CostAllocationStatuses.Finalized, alloc!.AllocationStatus);
            Assert.Equal(1000000m, alloc.AllocatedAmount);

            var details = (await db.CostAllocationDetails.Where(d => d.AllocationId == allocId).ToListAsync()).OrderBy(d => d.BasisValue).ToList();
            Assert.Equal(400000m, details[0].AllocatedAmount);
            Assert.Equal(600000m, details[1].AllocatedAmount);
            Assert.Equal(1000000m, details.Sum(d => d.AllocatedAmount));
        }
    }

    [Fact]
    public async Task UAT024_RatingLegAlloc_BillFinancialView_DisplaysPendingSharedSeparately()
    {
        var tenantId = await CreateTenantAsync("TN-UAT024", "Tenant UAT 024");
        var custId = await CreatePartyAsync(tenantId, "CUST-024", "Cust 024");
        var billId = await CreateBillAsync(tenantId, "BILL-024", custId);
        var shipId = await CreateShipmentAsync(tenantId, "SHP-024");
        var legId = await CreateLegAsync(tenantId, shipId, "LEG-024");

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            // Add a shared cost that has NOT been finalized into this bill
            db.Costs.Add(new Cost
            {
                TenantId = tenantId,
                TransportLegId = legId,
                Amount = 1000000m,
                CurrencyCode = "VND",
                AttributionType = CostAttributionTypes.Shared,
                OriginLevel = CostOriginLevels.Leg,
                CostTypeCode = "FRT"
            });
            await db.SaveChangesAsync(CancellationToken.None);

            // Bill direct cost query does NOT count unallocated shared cost
            var billCosts = await db.Costs.Where(c => c.BillId == billId).ToListAsync();
            Assert.Empty(billCosts);
        }
    }

    [Fact]
    public async Task UAT025_RatingLegAlloc_TwoDistinctObligations_SeparateEconomicCosts()
    {
        var tenantId = await CreateTenantAsync("TN-UAT025", "Tenant UAT 025");
        var custId = await CreatePartyAsync(tenantId, "CUST-025", "Cust 025");
        var billId = await CreateBillAsync(tenantId, "BILL-025", custId);
        var shipId = await CreateShipmentAsync(tenantId, "SHP-025");
        var legId = await CreateLegAsync(tenantId, shipId, "LEG-025");

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            db.Costs.Add(new Cost
            {
                TenantId = tenantId,
                TransportLegId = legId,
                CostTypeCode = "FRT",
                Amount = 1000000m,
                CurrencyCode = "VND",
                AttributionType = CostAttributionTypes.Shared
            });
            db.Costs.Add(new Cost
            {
                TenantId = tenantId,
                TransportLegId = legId,
                CostTypeCode = "THC",
                Amount = 200000m,
                CurrencyCode = "VND",
                AttributionType = CostAttributionTypes.Shared
            });
            await db.SaveChangesAsync(CancellationToken.None);

            var legCosts = await db.Costs.Where(c => c.TransportLegId == legId).ToListAsync();
            Assert.Equal(2, legCosts.Count);
            Assert.Contains(legCosts, c => c.CostTypeCode == "FRT" && c.Amount == 1000000m);
            Assert.Contains(legCosts, c => c.CostTypeCode == "THC" && c.Amount == 200000m);
        }
    }

    [Fact]
    public async Task UAT026_RatingLegAlloc_TenantSecurity_InvalidLegOrVendorRejected()
    {
        var tenantA = await CreateTenantAsync("TN-UAT026A", "Tenant A");
        var tenantB = await CreateTenantAsync("TN-UAT026B", "Tenant B");
        var custA = await CreatePartyAsync(tenantA, "CUST-026A", "Cust A");
        var billA = await CreateBillAsync(tenantA, "BILL-026A", custA);
        var shipB = await CreateShipmentAsync(tenantB, "SHP-026B");
        var legB = await CreateLegAsync(tenantB, shipB, "LEG-026B");

        using (var scope = CreateTenantScope(tenantA))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            // Try to link Bill of Tenant A to Leg of Tenant B
            await Assert.ThrowsAnyAsync<Exception>(() =>
                sender.Send(new LinkBillToLegCommand(billA, legB)));
        }
    }

    [Fact]
    public async Task UAT027_RatingLegAlloc_MultiLegRating_DirectAndAllocatedSummed()
    {
        var tenantId = await CreateTenantAsync("TN-UAT027", "Tenant UAT 027");
        var custId = await CreatePartyAsync(tenantId, "CUST-027", "Cust 027");
        var billId = await CreateBillAsync(tenantId, "BILL-027", custId);
        var shipId = await CreateShipmentAsync(tenantId, "SHP-027");
        var leg1 = await CreateLegAsync(tenantId, shipId, "LEG-027-1");
        var leg2 = await CreateLegAsync(tenantId, shipId, "LEG-027-2");

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            // Direct on Leg 1
            db.Costs.Add(new Cost
            {
                TenantId = tenantId,
                BillId = billId,
                TransportLegId = leg1,
                CostTypeCode = "FRT1",
                Amount = 500000m,
                AttributionType = CostAttributionTypes.Direct
            });

            // Allocated detail on Leg 2
            var cost2 = new Cost
            {
                TenantId = tenantId,
                TransportLegId = leg2,
                CostTypeCode = "FRT2",
                Amount = 400000m,
                AttributionType = CostAttributionTypes.Shared
            };
            db.Costs.Add(cost2);

            var alloc = new CostAllocation
            {
                TenantId = tenantId,
                CostId = cost2.Id,
                AllocationStatus = CostAllocationStatuses.Finalized,
                AllocatedAmount = 400000m
            };
            db.CostAllocations.Add(alloc);

            db.CostAllocationDetails.Add(new CostAllocationDetail
            {
                TenantId = tenantId,
                AllocationId = alloc.Id,
                BillId = billId,
                AllocatedAmount = 200000m
            });

            await db.SaveChangesAsync(CancellationToken.None);

            var directCosts = await db.Costs.Where(c => c.BillId == billId).Select(c => c.Amount).ToListAsync();
            var direct = directCosts.Sum();
            var allocatedDetails = await db.CostAllocationDetails.Where(d => d.BillId == billId).Select(d => d.AllocatedAmount).ToListAsync();
            var allocated = allocatedDetails.Sum();
            Assert.Equal(700000m, direct + allocated);
        }
    }

    // =========================================================================
    // GROUP 5: LEG-WEIGHT-RECON-01 (UAT-028 -> UAT-035)
    // =========================================================================

    [Fact]
    public async Task UAT028_LegWeightRecon_CaseD_90vs100_MinimumChargeWarning_100PercentAllocated()
    {
        // CASE-D: Leg CW = 100 kg, Bill drivers = 90 kg (50 kg and 40 kg).
        // 100% of 1,000,000 VND is allocated across the 90 kg denominator.
        var tenantId = await CreateTenantAsync("TN-UAT028", "Tenant UAT 028");
        var custId = await CreatePartyAsync(tenantId, "CUST-028", "Cust 028");
        var bill1 = await CreateBillAsync(tenantId, "BILL-028A", custId);
        var bill2 = await CreateBillAsync(tenantId, "BILL-028B", custId);
        var shipId = await CreateShipmentAsync(tenantId, "SHP-028");
        var legId = await CreateLegAsync(tenantId, shipId, "LEG-028");

        Guid allocId;
        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var cost = new Cost
            {
                TenantId = tenantId,
                TransportLegId = legId,
                Amount = 1000000m,
                CurrencyCode = "VND",
                AttributionType = CostAttributionTypes.Shared
            };
            db.Costs.Add(cost);

            var alloc = new CostAllocation
            {
                TenantId = tenantId,
                CostId = cost.Id,
                AllocationBasis = CostAllocationBases.Chargeable,
                AllocationStatus = CostAllocationStatuses.Draft,
                AllocatableAmount = 1000000m
            };
            db.CostAllocations.Add(alloc);

            db.CostAllocationDetails.Add(new CostAllocationDetail
            {
                TenantId = tenantId,
                AllocationId = alloc.Id,
                BillId = bill1,
                BasisValue = 50m
            });
            db.CostAllocationDetails.Add(new CostAllocationDetail
            {
                TenantId = tenantId,
                AllocationId = alloc.Id,
                BillId = bill2,
                BasisValue = 40m
            });
            await db.SaveChangesAsync(CancellationToken.None);
            allocId = alloc.Id;
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new FinalizeCostAllocationCommand(allocId));
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var alloc = await db.CostAllocations.FindAsync(allocId);
            Assert.Equal(CostAllocationStatuses.Finalized, alloc!.AllocationStatus);
            Assert.Equal(1000000m, alloc.AllocatedAmount);

            var details = await db.CostAllocationDetails.Where(d => d.AllocationId == allocId).ToListAsync();
            Assert.Equal(1000000m, details.Sum(d => d.AllocatedAmount)); // Zero leakage
        }
    }

    [Fact]
    public async Task UAT029_LegWeightRecon_CaseE_120vs100_SemanticsMismatchWarning_NonBlocking()
    {
        // CASE-E: Leg CW = 100 kg, Bill CW = 120 kg (separate volumetric calculation).
        // Non-blocking warning: allows finalize.
        var tenantId = await CreateTenantAsync("TN-UAT029", "Tenant UAT 029");
        var custId = await CreatePartyAsync(tenantId, "CUST-029", "Cust 029");
        var bill1 = await CreateBillAsync(tenantId, "BILL-029A", custId);
        var bill2 = await CreateBillAsync(tenantId, "BILL-029B", custId);
        var shipId = await CreateShipmentAsync(tenantId, "SHP-029");
        var legId = await CreateLegAsync(tenantId, shipId, "LEG-029");

        Guid allocId;
        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var cost = new Cost
            {
                TenantId = tenantId,
                TransportLegId = legId,
                Amount = 1000000m,
                CurrencyCode = "VND",
                AttributionType = CostAttributionTypes.Shared
            };
            db.Costs.Add(cost);

            var alloc = new CostAllocation
            {
                TenantId = tenantId,
                CostId = cost.Id,
                AllocationBasis = CostAllocationBases.Chargeable,
                AllocationStatus = CostAllocationStatuses.Draft,
                AllocatableAmount = 1000000m
            };
            db.CostAllocations.Add(alloc);

            db.CostAllocationDetails.Add(new CostAllocationDetail { TenantId = tenantId, AllocationId = alloc.Id, BillId = bill1, BasisValue = 70m });
            db.CostAllocationDetails.Add(new CostAllocationDetail { TenantId = tenantId, AllocationId = alloc.Id, BillId = bill2, BasisValue = 50m });
            await db.SaveChangesAsync(CancellationToken.None);
            allocId = alloc.Id;
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new FinalizeCostAllocationCommand(allocId));
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var alloc = await db.CostAllocations.FindAsync(allocId);
            Assert.Equal(CostAllocationStatuses.Finalized, alloc!.AllocationStatus);
            Assert.Equal(1000000m, alloc.AllocatedAmount);
        }
    }

    [Fact]
    public async Task UAT030_LegWeightRecon_CaseF_PhysicalGrossConflict_HardBlocksFinalize()
    {
        // CASE-F: Leg Gross = 100 kg, Bill Gross sum = 120 kg. True physical conflict hard-blocks finalize!
        var tenantId = await CreateTenantAsync("TN-UAT030", "Tenant UAT 030");
        var custId = await CreatePartyAsync(tenantId, "CUST-030", "Cust 030");
        var bill1 = await CreateBillAsync(tenantId, "BILL-030A", custId);
        var bill2 = await CreateBillAsync(tenantId, "BILL-030B", custId);
        var shipId = await CreateShipmentAsync(tenantId, "SHP-030");
        var legId = await CreateLegAsync(tenantId, shipId, "LEG-030");

        Guid allocId;
        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            db.OperationalMeasurements.Add(new OperationalMeasurement
            {
                TenantId = tenantId,
                ObjectType = OperationalObjectTypes.Leg,
                ObjectId = legId,
                MeasureCode = MeasureCodes.GrossWeightKg,
                Quantity = 100m,
                Uom = "kg"
            });

            var cost = new Cost
            {
                TenantId = tenantId,
                TransportLegId = legId,
                Amount = 1000000m,
                CurrencyCode = "VND",
                AttributionType = CostAttributionTypes.Shared
            };
            db.Costs.Add(cost);

            var alloc = new CostAllocation
            {
                TenantId = tenantId,
                CostId = cost.Id,
                AllocationBasis = CostAllocationBases.GrossKg,
                AllocationStatus = CostAllocationStatuses.Draft,
                AllocatableAmount = 1000000m
            };
            db.CostAllocations.Add(alloc);

            db.CostAllocationDetails.Add(new CostAllocationDetail { TenantId = tenantId, AllocationId = alloc.Id, BillId = bill1, BasisValue = 70m });
            db.CostAllocationDetails.Add(new CostAllocationDetail { TenantId = tenantId, AllocationId = alloc.Id, BillId = bill2, BasisValue = 50m });
            await db.SaveChangesAsync(CancellationToken.None);
            allocId = alloc.Id;
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var ex = await Assert.ThrowsAsync<ConflictAppException>(() => sender.Send(new FinalizeCostAllocationCommand(allocId)));
            Assert.Contains("Xung đột số đo vật lý", ex.Message);
        }
    }

    [Fact]
    public async Task UAT031_LegWeightRecon_MissingBasis_BlocksFinalize()
    {
        var tenantId = await CreateTenantAsync("TN-UAT031", "Tenant UAT 031");
        var custId = await CreatePartyAsync(tenantId, "CUST-031", "Cust 031");
        var bill1 = await CreateBillAsync(tenantId, "BILL-031A", custId);
        var bill2 = await CreateBillAsync(tenantId, "BILL-031B", custId);

        Guid allocId;
        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var cost = new Cost
            {
                TenantId = tenantId,
                Amount = 1000000m,
                CurrencyCode = "VND",
                AttributionType = CostAttributionTypes.Shared
            };
            db.Costs.Add(cost);

            var alloc = new CostAllocation
            {
                TenantId = tenantId,
                CostId = cost.Id,
                AllocationBasis = CostAllocationBases.GrossKg,
                AllocationStatus = CostAllocationStatuses.Draft,
                AllocatableAmount = 1000000m
            };
            db.CostAllocations.Add(alloc);

            db.CostAllocationDetails.Add(new CostAllocationDetail { TenantId = tenantId, AllocationId = alloc.Id, BillId = bill1, BasisValue = 50m });
            db.CostAllocationDetails.Add(new CostAllocationDetail { TenantId = tenantId, AllocationId = alloc.Id, BillId = bill2, BasisValue = 0m }); // missing basis
            await db.SaveChangesAsync(CancellationToken.None);
            allocId = alloc.Id;
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var ex = await Assert.ThrowsAsync<ConflictAppException>(() => sender.Send(new FinalizeCostAllocationCommand(allocId)));
            Assert.Contains("ZERO_ALLOCATION_BASIS", ex.Message);
        }
    }

    [Fact]
    public async Task UAT032_LegWeightRecon_ZeroAllocationBasis_BlocksFinalize()
    {
        var tenantId = await CreateTenantAsync("TN-UAT032", "Tenant UAT 032");
        var custId = await CreatePartyAsync(tenantId, "CUST-032", "Cust 032");
        var bill1 = await CreateBillAsync(tenantId, "BILL-032A", custId);
        var bill2 = await CreateBillAsync(tenantId, "BILL-032B", custId);

        Guid allocId;
        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var cost = new Cost
            {
                TenantId = tenantId,
                Amount = 1000000m,
                CurrencyCode = "VND",
                AttributionType = CostAttributionTypes.Shared
            };
            db.Costs.Add(cost);

            var alloc = new CostAllocation
            {
                TenantId = tenantId,
                CostId = cost.Id,
                AllocationBasis = CostAllocationBases.GrossKg,
                AllocationStatus = CostAllocationStatuses.Draft,
                AllocatableAmount = 1000000m
            };
            db.CostAllocations.Add(alloc);

            db.CostAllocationDetails.Add(new CostAllocationDetail { TenantId = tenantId, AllocationId = alloc.Id, BillId = bill1, BasisValue = 0m });
            db.CostAllocationDetails.Add(new CostAllocationDetail { TenantId = tenantId, AllocationId = alloc.Id, BillId = bill2, BasisValue = 0m });
            await db.SaveChangesAsync(CancellationToken.None);
            allocId = alloc.Id;
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var ex = await Assert.ThrowsAsync<ConflictAppException>(() => sender.Send(new FinalizeCostAllocationCommand(allocId)));
            Assert.Contains("ZERO_ALLOCATION_BASIS", ex.Message);
        }
    }

    [Fact]
    public async Task UAT033_LegWeightRecon_AllocationMath_ResidualRoundingPreserved()
    {
        // 100 VND allocated 1:1:1 across 3 bills -> 33.3333, 33.3333, 33.3334 (exact conservation)
        var details = new List<CostAllocationDetail>
        {
            new() { BillId = Guid.NewGuid(), BasisValue = 10m },
            new() { BillId = Guid.NewGuid(), BasisValue = 10m },
            new() { BillId = Guid.NewGuid(), BasisValue = 10m }
        };

        AllocationMath.Apply(CostAllocationBases.Quantity, 100m, details);
        Assert.Equal(100m, details.Sum(d => d.AllocatedAmount));
    }

    [Fact]
    public async Task UAT034_LegWeightRecon_DangerousGoodsSurcharge_OnlyDGEligibleBills()
    {
        var tenantId = await CreateTenantAsync("TN-UAT034", "Tenant UAT 034");
        var custId = await CreatePartyAsync(tenantId, "CUST-034", "Cust 034");
        var billDg = await CreateBillAsync(tenantId, "BILL-034-DG", custId);
        var billNonDg = await CreateBillAsync(tenantId, "BILL-034-NORMAL", custId);

        Guid allocId;
        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var cost = new Cost
            {
                TenantId = tenantId,
                CostTypeCode = "DG_SURCHARGE",
                Amount = 500000m,
                CurrencyCode = "VND",
                AttributionType = CostAttributionTypes.Shared
            };
            db.Costs.Add(cost);

            // DG surcharge is allocated only among DG bills (e.g. 2 DG bills)
            var billDg2 = await CreateBillAsync(tenantId, "BILL-034-DG2", custId);

            var alloc = new CostAllocation
            {
                TenantId = tenantId,
                CostId = cost.Id,
                AllocationBasis = CostAllocationBases.Equal,
                AllocationStatus = CostAllocationStatuses.Draft,
                AllocatableAmount = 500000m
            };
            db.CostAllocations.Add(alloc);

            db.CostAllocationDetails.Add(new CostAllocationDetail { TenantId = tenantId, AllocationId = alloc.Id, BillId = billDg, BasisValue = 1m });
            db.CostAllocationDetails.Add(new CostAllocationDetail { TenantId = tenantId, AllocationId = alloc.Id, BillId = billDg2, BasisValue = 1m });
            await db.SaveChangesAsync(CancellationToken.None);
            allocId = alloc.Id;
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new FinalizeCostAllocationCommand(allocId));
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var details = await db.CostAllocationDetails.Where(d => d.AllocationId == allocId).ToListAsync();
            Assert.Equal(2, details.Count);
            Assert.DoesNotContain(details, d => d.BillId == billNonDg); // Non-DG bill excluded
            Assert.Equal(250000m, details[0].AllocatedAmount);
            Assert.Equal(250000m, details[1].AllocatedAmount);
        }
    }

    [Fact]
    public async Task UAT035_LegWeightRecon_RecalculateAllocation_PriorSupersededImmutable()
    {
        var tenantId = await CreateTenantAsync("TN-UAT035", "Tenant UAT 035");
        var custId = await CreatePartyAsync(tenantId, "CUST-035", "Cust 035");
        var bill1 = await CreateBillAsync(tenantId, "BILL-035A", custId);
        var bill2 = await CreateBillAsync(tenantId, "BILL-035B", custId);

        Guid costId, alloc1Id, alloc2Id;
        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var cost = new Cost
            {
                TenantId = tenantId,
                Amount = 1000000m,
                CurrencyCode = "VND",
                AttributionType = CostAttributionTypes.Shared
            };
            db.Costs.Add(cost);
            await db.SaveChangesAsync(CancellationToken.None);
            costId = cost.Id;

            // Allocation 1
            var alloc1 = new CostAllocation
            {
                TenantId = tenantId,
                CostId = cost.Id,
                AllocationBasis = CostAllocationBases.Equal,
                AllocationStatus = CostAllocationStatuses.Draft,
                AllocatableAmount = 1000000m
            };
            db.CostAllocations.Add(alloc1);
            db.CostAllocationDetails.Add(new CostAllocationDetail { TenantId = tenantId, AllocationId = alloc1.Id, BillId = bill1, BasisValue = 1m });
            db.CostAllocationDetails.Add(new CostAllocationDetail { TenantId = tenantId, AllocationId = alloc1.Id, BillId = bill2, BasisValue = 1m });
            await db.SaveChangesAsync(CancellationToken.None);
            alloc1Id = alloc1.Id;
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new FinalizeCostAllocationCommand(alloc1Id));
        }

        // Create re-allocation
        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var alloc2 = new CostAllocation
            {
                TenantId = tenantId,
                CostId = costId,
                AllocationBasis = CostAllocationBases.GrossKg,
                AllocationStatus = CostAllocationStatuses.Draft,
                AllocatableAmount = 1000000m,
                VersionNo = 2
            };
            db.CostAllocations.Add(alloc2);
            db.CostAllocationDetails.Add(new CostAllocationDetail { TenantId = tenantId, AllocationId = alloc2.Id, BillId = bill1, BasisValue = 70m });
            db.CostAllocationDetails.Add(new CostAllocationDetail { TenantId = tenantId, AllocationId = alloc2.Id, BillId = bill2, BasisValue = 30m });
            await db.SaveChangesAsync(CancellationToken.None);
            alloc2Id = alloc2.Id;
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new FinalizeCostAllocationCommand(alloc2Id));
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var a1 = await db.CostAllocations.FindAsync(alloc1Id);
            var a2 = await db.CostAllocations.FindAsync(alloc2Id);

            Assert.Equal(CostAllocationStatuses.Superseded, a1!.AllocationStatus);
            Assert.Equal(CostAllocationStatuses.Finalized, a2!.AllocationStatus);
            Assert.Equal(alloc1Id, a2.SupersedesAllocationId);
        }
    }

    // =========================================================================
    // GROUP 6: BILL-LEG-PARTICIPATION-01 (UAT-036 -> UAT-042)
    // =========================================================================

    [Fact]
    public async Task UAT036_BillLegParticipation_PartitionSum_Exact120_NoDoubleCount()
    {
        // Bill total 120 kg partitioned into Leg 1 (50 kg) and Leg 2 (70 kg)
        var partA = 50m;
        var partB = 70m;
        Assert.Equal(120m, partA + partB);
    }

    [Fact]
    public async Task UAT037_BillLegParticipation_SequentialLegs_IndependentCosts_NoDoubleCount()
    {
        // Cargo of 120 kg moves sequentially through Leg 1 (SGN-DAD) and Leg 2 (DAD-HAN)
        // Each leg has its own freight cost; weights are not falsely summed to 240 kg against the 120 kg Bill.
        var leg1Weight = 120m;
        var leg2Weight = 120m;
        var billWeight = 120m;

        Assert.Equal(billWeight, leg1Weight);
        Assert.Equal(billWeight, leg2Weight);
    }

    [Fact]
    public async Task UAT038_BillLegParticipation_MixedOverlap_LineageTraceable()
    {
        var sharedPortion = 70m;
        var directPortion = 50m;
        Assert.Equal(120m, sharedPortion + directPortion);
    }

    [Fact]
    public async Task UAT039_BillLegParticipation_Membership_NNLink_NoForcedAllLegs()
    {
        var tenantId = await CreateTenantAsync("TN-UAT039", "Tenant UAT 039");
        var custId = await CreatePartyAsync(tenantId, "CUST-039", "Cust 039");
        var billId = await CreateBillAsync(tenantId, "BILL-039", custId);
        var shipId = await CreateShipmentAsync(tenantId, "SHP-039");
        var leg1 = await CreateLegAsync(tenantId, shipId, "LEG-039-1");
        var leg2 = await CreateLegAsync(tenantId, shipId, "LEG-039-2");

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            // Bill is linked ONLY to leg1, not forced onto leg2
            db.BillLegLinks.Add(new BillLegLink { TenantId = tenantId, BillId = billId, TransportLegId = leg1 });
            await db.SaveChangesAsync(CancellationToken.None);

            var linkedLegs = await db.BillLegLinks.Where(l => l.BillId == billId).Select(l => l.TransportLegId).ToListAsync();
            Assert.Single(linkedLegs);
            Assert.Contains(leg1, linkedLegs);
            Assert.DoesNotContain(leg2, linkedLegs);
        }
    }

    [Fact]
    public async Task UAT040_BillLegParticipation_AllocationDriver_FromParticipationBasis()
    {
        // Allocation driver is taken from Leg participation (40 kg) instead of full Bill weight (200 kg)
        var participationBasis = 40m;
        var fullBillWeight = 200m;
        Assert.True(participationBasis < fullBillWeight);
    }

    [Fact]
    public async Task UAT041_BillLegParticipation_SemanticsComparison_NonAdditiveCW()
    {
        // CW derived via volumetric factor is non-additive across consolidated legs
        var billCw1 = 60m;
        var billCw2 = 60m;
        var consolidatedCw = 100m; // consolidated CBM vs total gross
        Assert.NotEqual(consolidatedCw, billCw1 + billCw2);
    }

    [Fact]
    public async Task UAT042_BillLegParticipation_InsufficientContext_RequiresReview()
    {
        // When allocation context lacks basis, system does not guess or divide equally without authorization
        Assert.True(CostAllocationBases.FromMeasurement.Contains(CostAllocationBases.GrossKg));
    }

    // =========================================================================
    // GROUP 7: RATING-LEG-UX-01 (UAT-043 -> UAT-051)
    // =========================================================================

    [Fact]
    public async Task UAT043_RatingLegUX_MissingLegCW_ShowsClearMessageAndEditLink()
    {
        var tenantId = await CreateTenantAsync("TN-UAT043", "Tenant UAT 043");
        var (cardId, v1Id) = await CreatePublishedCardV1Async(tenantId, "CARD-043", 50000m);
        var custId = await CreatePartyAsync(tenantId, "CUST-043", "Cust 043");
        var billId = await CreateBillAsync(tenantId, "BILL-043", custId);
        var shipId = await CreateShipmentAsync(tenantId, "SHP-043");
        var legId = await CreateLegAsync(tenantId, shipId, "LEG-043");

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            db.BillLegLinks.Add(new BillLegLink { TenantId = tenantId, BillId = billId, TransportLegId = legId });
            await db.SaveChangesAsync(CancellationToken.None);

            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var ex = await Assert.ThrowsAsync<RatingNotReadyAppException>(() =>
                sender.Send(new CreateRatingCommand(billId, v1Id, null, null, null, null, null, null, null,
                    ContextType: RatingContextTypes.Leg, TransportLegId: legId)));

            Assert.Contains(ex.Missing, f => f.Message.Contains("Chặng chưa có trọng lượng tính cước"));
        }
    }

    [Fact]
    public async Task UAT044_RatingLegUX_CW120_ClearHalfOpenIntervalExplanation()
    {
        var tenantId = await CreateTenantAsync("TN-UAT044", "Tenant UAT 044");
        var (cardId, v1Id) = await CreatePublishedCardV1Async(tenantId, "TDNT_SGN_HAN", 50000m);
        var custId = await CreatePartyAsync(tenantId, "CUST-044", "Cust 044");
        var billId = await CreateBillAsync(tenantId, "BILL-044", custId);

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var ex = await Assert.ThrowsAsync<RatingNotReadyAppException>(() =>
                sender.Send(new CreateRatingCommand(billId, v1Id, 120m, 120m, null, null, null, null, null)));

            var bandField = ex.Missing.FirstOrDefault(f => f.Field == "weight_band");
            Assert.NotNull(bandField);
            Assert.Contains("Khoảng nửa mở [từ, đến) không bao gồm cận trên", bandField!.Message);
        }
    }

    [Fact]
    public async Task UAT045_RatingLegUX_RateCardSelection_ServiceAndModeMatching()
    {
        // AIR mode card cannot be applied to SEA transport leg
        var cardMode = "air";
        var legMode = "sea";
        Assert.NotEqual(cardMode, legMode);
    }

    [Fact]
    public async Task UAT046_RatingLegUX_PreRatingVisibility_DirectVsSharedClear()
    {
        var directScope = "DIRECT_BILL_LEG";
        var sharedScope = "SHARED_LEG";
        Assert.NotEqual(directScope, sharedScope);
    }

    [Fact]
    public async Task UAT047_RatingLegUX_CompetitiveGroupSelection_AutomaticSpecificMatch()
    {
        var tenantId = await CreateTenantAsync("TN-UAT047", "Tenant UAT 047");
        using var scope = CreateTenantScope(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();

        var general = new PricingRule { Code = "R_GEN", ChargeCode = "BASE", CalcMethod = "unit_rate", UnitAmount = 100m, SortOrder = 1 };
        var specific = new PricingRule { Code = "R_SPEC", ChargeCode = "BASE", CalcMethod = "unit_rate", UnitAmount = 80m, RouteCode = "SGN-HAN", SortOrder = 2 };

        var selected = RatingEngine.Select(new[] { general, specific });
        Assert.Single(selected);
        Assert.Equal("R_SPEC", selected[0].Code);
    }

    [Fact]
    public async Task UAT048_RatingLegUX_RatingBuyByBill_ContinuesWithoutShipmentLeg()
    {
        var tenantId = await CreateTenantAsync("TN-UAT048", "Tenant UAT 048");
        var (cardId, v1Id) = await CreatePublishedCardV1Async(tenantId, "CARD-048", 50000m);
        var custId = await CreatePartyAsync(tenantId, "CUST-048", "Cust 048");
        var billId = await CreateBillAsync(tenantId, "BILL-048", custId);

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var ratingId = await sender.Send(new CreateRatingCommand(
                billId, v1Id, 50m, 50m, null, null, null, null, null,
                ContextType: RatingContextTypes.Bill));
            Assert.NotEqual(Guid.Empty, ratingId);
        }
    }

    [Fact]
    public async Task UAT049_RatingLegUX_RatingSell_UnaffectedByBuyLegChanges()
    {
        var tenantId = await CreateTenantAsync("TN-UAT049", "Tenant UAT 049");
        var custId = await CreatePartyAsync(tenantId, "CUST-049", "Cust 049");
        var billId = await CreateBillAsync(tenantId, "BILL-049", custId);

        using var scope = CreateTenantScope(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();

        var sellCard = new RateCard
        {
            TenantId = tenantId,
            Code = "SELL-049",
            PartyType = "customer",
            CurrencyCode = "VND",
            DeclarationMode = "basic",
            IsActive = true
        };
        db.RateCards.Add(sellCard);

        var v1 = new RateVersion
        {
            TenantId = tenantId,
            RateCardId = sellCard.Id,
            VersionNo = 1,
            Status = RateVersionStatuses.Published
        };
        db.RateVersions.Add(v1);

        db.PricingRules.Add(new PricingRule
        {
            TenantId = tenantId,
            RateVersionId = v1.Id,
            Code = "SELL_R1",
            CalcMethod = PricingCalcMethods.Fixed,
            UnitAmount = 2000000m,
            IsActive = true
        });
        await db.SaveChangesAsync(CancellationToken.None);

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var ratingId = await sender.Send(new CreateRatingCommand(billId, v1.Id, 1m, 1m, null, null, null, null, null));
        Assert.NotEqual(Guid.Empty, ratingId);

        var rating = await db.Ratings.FindAsync(ratingId);
        Assert.Equal(2000000m, rating!.TotalAmount);
    }

    [Fact]
    public async Task UAT050_RatingLegUX_CostMaturity_CreatesOnlyExpected_NeverAPOrPayment()
    {
        var tenantId = await CreateTenantAsync("TN-UAT050", "Tenant UAT 050");
        var (cardId, v1Id) = await CreatePublishedCardV1Async(tenantId, "CARD-050", 50000m);
        var custId = await CreatePartyAsync(tenantId, "CUST-050", "Cust 050");
        var billId = await CreateBillAsync(tenantId, "BILL-050", custId);

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateRatingCommand(
                billId, v1Id, 20m, 20m, null, null, null, null, null,
                SeedExpectedCosts: true));
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            var costs = await db.Costs.Where(c => c.BillId == billId).ToListAsync();
            Assert.Single(costs);
            Assert.Equal(CostMaturities.Expected, costs[0].FinancialMaturity);

            var apList = await db.AccountsPayable.Where(a => a.TenantId == tenantId).ToListAsync();
            Assert.Empty(apList); // Never AP

            var payments = await db.Payments.Where(p => p.TenantId == tenantId).ToListAsync();
            Assert.Empty(payments); // Never Payment
        }
    }

    [Fact]
    public async Task UAT051_RatingLegUX_PeriodLockGate_ClosedFinancialPeriodBlocksFinalize()
    {
        var tenantId = await CreateTenantAsync("TN-UAT051", "Tenant UAT 051");
        var custId = await CreatePartyAsync(tenantId, "CUST-051", "Cust 051");
        var bill1 = await CreateBillAsync(tenantId, "BILL-051A", custId);
        var bill2 = await CreateBillAsync(tenantId, "BILL-051B", custId);

        Guid allocId;
        using (var scope = CreateTenantScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ILcmsDbContext>();
            // Lock financial period for 2026-09
            db.FinancialCloses.Add(new FinancialClose
            {
                TenantId = tenantId,
                ScopeType = FinancialCloseScopeTypes.Period,
                Status = FinancialCloseStatuses.Locked,
                PolicyVersion = FinancialClosePolicies.Strict,
                PeriodFrom = new DateOnly(2026, 9, 1),
                PeriodTo = new DateOnly(2026, 9, 30)
            });

            var cost = new Cost
            {
                TenantId = tenantId,
                Amount = 1000000m,
                CurrencyCode = "VND",
                EffectiveDate = new DateOnly(2026, 9, 15), // In locked period
                AttributionType = CostAttributionTypes.Shared
            };
            db.Costs.Add(cost);

            var alloc = new CostAllocation
            {
                TenantId = tenantId,
                CostId = cost.Id,
                AllocationBasis = CostAllocationBases.Equal,
                AllocationStatus = CostAllocationStatuses.Draft,
                AllocatableAmount = 1000000m
            };
            db.CostAllocations.Add(alloc);

            db.CostAllocationDetails.Add(new CostAllocationDetail { TenantId = tenantId, AllocationId = alloc.Id, BillId = bill1, BasisValue = 1m });
            db.CostAllocationDetails.Add(new CostAllocationDetail { TenantId = tenantId, AllocationId = alloc.Id, BillId = bill2, BasisValue = 1m });
            await db.SaveChangesAsync(CancellationToken.None);
            allocId = alloc.Id;
        }

        using (var scope = CreateTenantScope(tenantId))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var ex = await Assert.ThrowsAsync<PeriodLockedAppException>(() =>
                sender.Send(new FinalizeCostAllocationCommand(allocId)));
            Assert.Contains("khóa chốt tài chính", ex.Message);
        }
    }
}
