using System.Security.Cryptography;
using System.Text;
using LCMS.Application.Abstractions;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LCMS.Application.Demo;

/// <summary>
/// Idempotent demo catalog for UI click-testing (all operable states on money paths).
/// Marker bill <c>DEMO-SEED-MARKER</c> — re-run skips when present.
/// </summary>
public sealed class DemoDataSeeder
{
    public const string MarkerBillNo = "DEMO-SEED-MARKER";

    private readonly ILcmsDbContext _db;
    private readonly DemoOptions _options;
    private readonly DemoVolumeCatalogSeeder _volume;
    private readonly ILogger<DemoDataSeeder> _logger;

    public DemoDataSeeder(
        ILcmsDbContext db,
        IOptions<DemoOptions> options,
        DemoVolumeCatalogSeeder volume,
        ILogger<DemoDataSeeder> logger)
    {
        _db = db;
        _options = options.Value;
        _volume = volume;
        _logger = logger;
    }

    public async Task<DemoSeedResult> EnsureAsync(CancellationToken cancellationToken = default)
    {
        var code = string.IsNullOrWhiteSpace(_options.TenantCode) ? "ops" : _options.TenantCode.Trim();
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Code == code, cancellationToken);
        if (tenant is null)
        {
            tenant = new Tenant
            {
                Code = code,
                Name = "Vận hành (demo)",
                IsActive = true
            };
            _db.Tenants.Add(tenant);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Demo seed created tenant {Code} ({TenantId}).", code, tenant.Id);
        }

        return await EnsureForTenantAsync(tenant, cancellationToken);
    }

    public async Task<DemoSeedResult> EnsureForTenantAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        var exists = await _db.Bills.IgnoreQueryFilters()
            .AnyAsync(b => b.TenantId == tenant.Id && b.BillNo == MarkerBillNo && b.DeletedAt == null, cancellationToken);

        await EnsureDefaultCatalogAsync(tenant.Id, cancellationToken);

        string scenarioSummary;
        if (exists)
        {
            _logger.LogInformation("Demo scenario skipped (marker {Marker} already present for tenant {Code}).", MarkerBillNo, tenant.Code);
            scenarioSummary = "Đã có kịch bản demo — bỏ qua.";
        }
        else
        {
            scenarioSummary = await SeedScenarioAsync(tenant.Id, cancellationToken);
        }

        var volume = await _volume.EnsureAsync(tenant.Id, cancellationToken);
        var tariffs = await EnsureReferenceTariffsAsync(tenant.Id, cancellationToken);
        var summary = $"{scenarioSummary} {volume.Summary} {tariffs}";
        _logger.LogInformation("Demo seed completed for tenant {Code}: {Summary}", tenant.Code, summary);
        return new DemoSeedResult(tenant.Id, Skipped: exists && volume.Skipped, Summary: summary, Counts: volume.Counts);
    }

    private async Task<string> SeedScenarioAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTimeOffset.UtcNow;
        var tid = tenantId;

        // --- Master ---
        var org = new Organization { TenantId = tid, Code = "DEMO-ORG", Name = "Chi nhánh Demo HCM", IsActive = true };
        _db.Organizations.Add(org);

        var vendor = Party(
            tid,
            "DEMO-VND",
            "Nhà xe Demo Vận Tải",
            legalName: "Công ty TNHH Demo Vận Tải",
            taxId: "0312345678",
            phone: "02812345678",
            email: "vendor@demo.local",
            defaultCurrency: "VND",
            paymentTermDays: 15,
            creditLimit: 500_000_000m);
        var customer = Party(
            tid,
            "DEMO-CUS",
            "Khách hàng Demo Logistics",
            legalName: "Công ty CP Demo Logistics",
            taxId: "0109876543",
            phone: "02498765432",
            email: "customer@demo.local",
            defaultCurrency: "VND",
            paymentTermDays: 30,
            creditLimit: 1_000_000_000m);
        var inactive = Party(tid, "DEMO-OLD", "Đối tác ngừng (demo)", active: false);
        _db.BusinessParties.AddRange(vendor, customer, inactive);
        await _db.SaveChangesAsync(cancellationToken);

        _db.PartyRoles.AddRange(
            Role(tid, vendor.Id, PartyRoleCodes.Vendor),
            Role(tid, vendor.Id, PartyRoleCodes.Payee),
            Role(tid, customer.Id, PartyRoleCodes.Customer),
            Role(tid, customer.Id, PartyRoleCodes.Payer));

        _db.PartyBankAccounts.AddRange(
            new PartyBankAccount
            {
                TenantId = tid,
                PartyId = vendor.Id,
                BankName = "Vietcombank",
                BankBranch = "HCM",
                AccountNumber = "0071000123456",
                AccountName = "CTY TNHH DEMO VAN TAI",
                CurrencyCode = "VND",
                IsDefault = true,
                IsActive = true
            },
            new PartyBankAccount
            {
                TenantId = tid,
                PartyId = customer.Id,
                BankName = "Techcombank",
                BankBranch = "HN",
                AccountNumber = "19001234567890",
                AccountName = "CTY CP DEMO LOGISTICS",
                CurrencyCode = "VND",
                IsDefault = true,
                IsActive = true
            });

        _db.PartyContacts.AddRange(
            new PartyContact
            {
                TenantId = tid,
                PartyId = vendor.Id,
                FullName = "Nguyễn Văn A",
                Title = "Kế toán công nợ",
                Phone = "0901000001",
                Email = "ap@demo.local",
                IsPrimary = true,
                IsActive = true
            },
            new PartyContact
            {
                TenantId = tid,
                PartyId = customer.Id,
                FullName = "Trần Thị B",
                Title = "Thanh toán",
                Phone = "0902000002",
                Email = "ar@demo.local",
                IsPrimary = true,
                IsActive = true
            });

        // --- Bills (anchors) ---
        var marker = Bill(tid, MarkerBillNo, org.Id, "Đánh dấu seed demo — không xóa");
        var billHub = Bill(tid, "DEMO-01-HUB", org.Id, "Bill hub: cost/revenue maturity");
        var billShareA = Bill(tid, "DEMO-02-SHARE-A", org.Id, "Shared cost leg A");
        var billShareB = Bill(tid, "DEMO-02-SHARE-B", org.Id, "Shared cost leg B");
        var billAp = Bill(tid, "DEMO-03-AP", org.Id, "AP → thanh toán → write-off");
        var billAr = Bill(tid, "DEMO-04-AR", org.Id, "AR → thu tiền");
        var billDocs = Bill(tid, "DEMO-05-DOCS", org.Id, "Chứng từ Received≠Accepted≠Matched");
        var billCloseOpen = Bill(tid, "DEMO-06-CLOSE-OPEN", org.Id, "Chốt đang mở");
        var billCloseLocked = Bill(tid, "DEMO-07-CLOSE-LOCKED", org.Id, "Chốt đã khóa + snapshot");
        var billCloseReopen = Bill(tid, "DEMO-08-CLOSE-REOPEN", org.Id, "Chốt đã mở lại");
        var billControl = Bill(tid, "DEMO-09-CONTROL", org.Id, "Ngoại lệ / phê duyệt / đối soát");
        var billEmpty = Bill(tid, "DEMO-10-EMPTY", org.Id, "Bill trống — empty state");

        _db.Bills.AddRange(
            marker, billHub, billShareA, billShareB, billAp, billAr, billDocs,
            billCloseOpen, billCloseLocked, billCloseReopen, billControl, billEmpty);
        await _db.SaveChangesAsync(cancellationToken);

        // --- Costs on HUB (maturity matrix) + Shared ---
        var costExpected = Cost(tid, billHub.Id, CostMaturities.Expected, 1_500_000m, today, "FREIGHT", vendor.Id);
        var costConfirmed = Cost(tid, billHub.Id, CostMaturities.Confirmed, 2_000_000m, today, "HANDLING", vendor.Id);
        costConfirmed.ConfirmedAmount = 2_000_000m;
        costConfirmed.ConfirmedAt = now;
        var costActual = Cost(tid, billHub.Id, CostMaturities.Actual, 2_100_000m, today, "THC", vendor.Id);
        costActual.ConfirmedAmount = 2_050_000m;
        costActual.ActualAmount = 2_100_000m;
        costActual.ConfirmedAt = now.AddDays(-2);
        costActual.ActualizedAt = now.AddDays(-1);

        var costShared = Cost(tid, null, CostMaturities.Confirmed, 900_000m, today, "SHARED-FUEL", vendor.Id);
        costShared.AttributionType = CostAttributionTypes.Shared;
        costShared.ConfirmedAmount = 900_000m;
        costShared.ConfirmedAt = now;

        _db.Costs.AddRange(costExpected, costConfirmed, costActual, costShared);
        await _db.SaveChangesAsync(cancellationToken);

        var allocDraft = new CostAllocation
        {
            TenantId = tid,
            CostId = costShared.Id,
            VersionNo = 1,
            AllocationBasis = CostAllocationBases.Equal,
            AllocatableAmount = 900_000m,
            AllocatedAmount = 900_000m,
            AllocationStatus = CostAllocationStatuses.Draft
        };
        var allocFinal = new CostAllocation
        {
            TenantId = tid,
            CostId = costShared.Id,
            VersionNo = 2,
            AllocationBasis = CostAllocationBases.Equal,
            AllocatableAmount = 900_000m,
            AllocatedAmount = 900_000m,
            AllocationStatus = CostAllocationStatuses.Finalized,
            FinalizedAt = now,
            SupersedesAllocationId = null
        };
        _db.CostAllocations.AddRange(allocDraft, allocFinal);
        await _db.SaveChangesAsync(cancellationToken);

        // Mark draft as superseded conceptually — keep one draft for UI allocate path on a second shared cost
        var costSharedDraftOnly = Cost(tid, null, CostMaturities.Expected, 400_000m, today, "SHARED-ADMIN", vendor.Id);
        costSharedDraftOnly.AttributionType = CostAttributionTypes.Shared;
        _db.Costs.Add(costSharedDraftOnly);
        await _db.SaveChangesAsync(cancellationToken);

        var allocDraftOnly = new CostAllocation
        {
            TenantId = tid,
            CostId = costSharedDraftOnly.Id,
            VersionNo = 1,
            AllocationBasis = CostAllocationBases.Equal,
            AllocatableAmount = 400_000m,
            AllocatedAmount = 400_000m,
            AllocationStatus = CostAllocationStatuses.Draft
        };
        _db.CostAllocations.Add(allocDraftOnly);
        await _db.SaveChangesAsync(cancellationToken);

        _db.CostAllocationDetails.AddRange(
            AllocDetail(tid, allocFinal.Id, billShareA.Id, 0.5m, 450_000m),
            AllocDetail(tid, allocFinal.Id, billShareB.Id, 0.5m, 450_000m),
            AllocDetail(tid, allocDraftOnly.Id, billShareA.Id, 0.5m, 200_000m),
            AllocDetail(tid, allocDraftOnly.Id, billShareB.Id, 0.5m, 200_000m));

        // --- Revenues on HUB ---
        var revExpected = Revenue(tid, billHub.Id, RevenueMaturities.Expected, 3_000_000m, today, "FREIGHT-REV", customer.Id);
        var revConfirmed = Revenue(tid, billHub.Id, RevenueMaturities.Confirmed, 3_200_000m, today, "SURCHARGE", customer.Id);
        revConfirmed.ConfirmedAmount = 3_200_000m;
        revConfirmed.ConfirmedAt = now;
        var revActual = Revenue(tid, billHub.Id, RevenueMaturities.Actual, 3_250_000m, today, "DETENTION", customer.Id);
        revActual.ConfirmedAmount = 3_200_000m;
        revActual.ActualAmount = 3_250_000m;
        revActual.ConfirmedAt = now.AddDays(-2);
        revActual.ActualizedAt = now.AddDays(-1);
        _db.Revenues.AddRange(revExpected, revConfirmed, revActual);

        // Costs/revenues on close bills (for snapshot P&L)
        var closeCost = Cost(tid, billCloseLocked.Id, CostMaturities.Actual, 500_000m, today, "CLOSE-COST", vendor.Id);
        closeCost.ConfirmedAmount = 500_000m;
        closeCost.ActualAmount = 500_000m;
        var closeRev = Revenue(tid, billCloseLocked.Id, RevenueMaturities.Actual, 800_000m, today, "CLOSE-REV", customer.Id);
        closeRev.ConfirmedAmount = 800_000m;
        closeRev.ActualAmount = 800_000m;
        var reopenCost = Cost(tid, billCloseReopen.Id, CostMaturities.Confirmed, 120_000m, today, "REOPEN-COST", vendor.Id);
        reopenCost.ConfirmedAmount = 120_000m;
        _db.Costs.AddRange(closeCost, reopenCost);
        _db.Revenues.Add(closeRev);
        await _db.SaveChangesAsync(cancellationToken);

        // --- Documents (triad states) on DEMO-05-DOCS ---
        var docNotAccepted = Doc(tid, billDocs.Id, vendor.Id, "DEMO-DN-WAIT", FinancialDocumentDirections.Payable,
            FinancialDocumentTypes.Dn, 1_000_000m, today, now,
            FinancialDocumentReceiptStatuses.Received,
            FinancialDocumentAcceptanceStatuses.NotAccepted,
            FinancialDocumentMatchingStatuses.Unmatched);
        var docAcceptedUnmatched = Doc(tid, billDocs.Id, vendor.Id, "DEMO-INV-ACC-UM", FinancialDocumentDirections.Payable,
            FinancialDocumentTypes.Invoice, 1_200_000m, today, now,
            FinancialDocumentReceiptStatuses.Received,
            FinancialDocumentAcceptanceStatuses.Accepted,
            FinancialDocumentMatchingStatuses.Unmatched);
        docAcceptedUnmatched.AcceptedAt = now;
        var docPartial = Doc(tid, billDocs.Id, vendor.Id, "DEMO-INV-PARTIAL", FinancialDocumentDirections.Payable,
            FinancialDocumentTypes.Invoice, 2_000_000m, today, now,
            FinancialDocumentReceiptStatuses.Received,
            FinancialDocumentAcceptanceStatuses.Accepted,
            FinancialDocumentMatchingStatuses.PartiallyMatched);
        docPartial.AcceptedAt = now;
        var docMatched = Doc(tid, billDocs.Id, vendor.Id, "DEMO-INV-MATCHED", FinancialDocumentDirections.Payable,
            FinancialDocumentTypes.Invoice, 2_100_000m, today, now,
            FinancialDocumentReceiptStatuses.Received,
            FinancialDocumentAcceptanceStatuses.Accepted,
            FinancialDocumentMatchingStatuses.Matched);
        docMatched.AcceptedAt = now;
        var docRejected = Doc(tid, billDocs.Id, vendor.Id, "DEMO-INV-REJ", FinancialDocumentDirections.Payable,
            FinancialDocumentTypes.Invoice, 500_000m, today, now,
            FinancialDocumentReceiptStatuses.Received,
            FinancialDocumentAcceptanceStatuses.Rejected,
            FinancialDocumentMatchingStatuses.Unmatched);
        var docCancelled = Doc(tid, billDocs.Id, vendor.Id, "DEMO-INV-CANCEL", FinancialDocumentDirections.Payable,
            FinancialDocumentTypes.Invoice, 300_000m, today, now,
            FinancialDocumentReceiptStatuses.Received,
            FinancialDocumentAcceptanceStatuses.NotAccepted,
            FinancialDocumentMatchingStatuses.Unmatched);
        docCancelled.RecordStatus = FinancialDocumentRecordStatuses.Cancelled;
        docCancelled.CancelledAt = now;
        docCancelled.CancelReason = "Demo: hủy chứng từ";
        var docAr = Doc(tid, billDocs.Id, customer.Id, "DEMO-AR-INV", FinancialDocumentDirections.Receivable,
            FinancialDocumentTypes.Invoice, 3_250_000m, today, now,
            FinancialDocumentReceiptStatuses.Received,
            FinancialDocumentAcceptanceStatuses.Accepted,
            FinancialDocumentMatchingStatuses.Unmatched);
        docAr.AcceptedAt = now;
        var docNotReceived = Doc(tid, billDocs.Id, vendor.Id, "DEMO-DRAFT-NR", FinancialDocumentDirections.Payable,
            FinancialDocumentTypes.Other, 100_000m, today, null,
            FinancialDocumentReceiptStatuses.NotReceived,
            FinancialDocumentAcceptanceStatuses.NotAccepted,
            FinancialDocumentMatchingStatuses.Unmatched);

        _db.FinancialDocuments.AddRange(
            docNotAccepted, docAcceptedUnmatched, docPartial, docMatched,
            docRejected, docCancelled, docAr, docNotReceived);
        await _db.SaveChangesAsync(cancellationToken);

        var linePartial = Line(tid, docPartial.Id, 1, 2_000_000m, billDocs.Id, matched: 800_000m);
        var lineMatched = Line(tid, docMatched.Id, 1, 2_100_000m, billDocs.Id, matched: 2_100_000m);
        var lineAr = Line(tid, docAr.Id, 1, 3_250_000m, billDocs.Id);
        var lineWait = Line(tid, docNotAccepted.Id, 1, 1_000_000m, billDocs.Id);
        var lineAcc = Line(tid, docAcceptedUnmatched.Id, 1, 1_200_000m, billDocs.Id);
        _db.FinancialDocumentLines.AddRange(linePartial, lineMatched, lineAr, lineWait, lineAcc);
        await _db.SaveChangesAsync(cancellationToken);

        var matchPartial = new DocumentMatch
        {
            TenantId = tid,
            MatchMethod = DocumentMatchMethods.LineToCost,
            MatchStatus = DocumentMatchStatuses.Draft,
            PrimaryDocumentId = docPartial.Id,
            Notes = "Demo: khớp một phần"
        };
        var matchFull = new DocumentMatch
        {
            TenantId = tid,
            MatchMethod = DocumentMatchMethods.LineToCost,
            MatchStatus = DocumentMatchStatuses.Draft,
            PrimaryDocumentId = docMatched.Id,
            Notes = "Demo: khớp đủ"
        };
        _db.DocumentMatches.AddRange(matchPartial, matchFull);
        await _db.SaveChangesAsync(cancellationToken);

        _db.DocumentMatchDetails.AddRange(
            new DocumentMatchDetail
            {
                TenantId = tid,
                MatchId = matchPartial.Id,
                SourceLineId = linePartial.Id,
                TargetCostId = costActual.Id,
                MatchedAmount = 800_000m,
                DetailStatus = DocumentMatchDetailStatuses.Active
            },
            new DocumentMatchDetail
            {
                TenantId = tid,
                MatchId = matchFull.Id,
                SourceLineId = lineMatched.Id,
                TargetCostId = costActual.Id,
                MatchedAmount = 2_100_000m,
                DetailStatus = DocumentMatchDetailStatuses.Active
            });

        // --- AP flow (DEMO-03-AP) ---
        var apCost = Cost(tid, billAp.Id, CostMaturities.Actual, 5_000_000m, today, "AP-FREIGHT", vendor.Id);
        apCost.ConfirmedAmount = 5_000_000m;
        apCost.ActualAmount = 5_000_000m;
        _db.Costs.Add(apCost);
        await _db.SaveChangesAsync(cancellationToken);

        var expOpen = ExposurePay(tid, billAp.Id, vendor.Id, apCost.Id, 1_000_000m, 0m, ExposureStatuses.Open, today);
        var expPartial = ExposurePay(tid, billAp.Id, vendor.Id, apCost.Id, 2_000_000m, 800_000m, ExposureStatuses.PartiallyRecognized, today);
        var expFull = ExposurePay(tid, billAp.Id, vendor.Id, apCost.Id, 2_000_000m, 2_000_000m, ExposureStatuses.FullyRecognized, today);
        _db.PayableExposures.AddRange(expOpen, expPartial, expFull);
        await _db.SaveChangesAsync(cancellationToken);

        var apOpen = Ap(tid, expPartial.Id, billAp.Id, vendor.Id, 800_000m, 0m, 0m, ApArSettlementStatuses.Open, today.AddDays(15), now);
        var apPartialSettle = Ap(tid, expFull.Id, billAp.Id, vendor.Id, 1_200_000m, 0m, 400_000m, ApArSettlementStatuses.PartiallySettled, today.AddDays(7), now);
        var apSettled = Ap(tid, expFull.Id, billAp.Id, vendor.Id, 800_000m, 0m, 800_000m, ApArSettlementStatuses.Settled, today.AddDays(-3), now);
        var apWriteOff = Ap(tid, expFull.Id, billAp.Id, vendor.Id, 500_000m, -50_000m, 450_000m, ApArSettlementStatuses.Settled, today, now);
        apWriteOff.Notes = "Demo: đã write-off phần dư";
        _db.AccountsPayable.AddRange(apOpen, apPartialSettle, apSettled, apWriteOff);
        await _db.SaveChangesAsync(cancellationToken);

        var payDraft = Pay(tid, billAp.Id, vendor.Id, 800_000m, today, "DEMO-PAY-DRAFT", "Chờ phân bổ / chốt");
        var payFinal = Pay(tid, billAp.Id, vendor.Id, 400_000m, today, "DEMO-PAY-FINAL", "Đã chốt phân bổ");
        var payReversed = Pay(tid, billAp.Id, vendor.Id, 200_000m, today, "DEMO-PAY-REV", "Phân bổ đã đảo");
        var payCancel = Pay(tid, billAp.Id, vendor.Id, 50_000m, today, "DEMO-PAY-CANCEL", "Giao dịch hủy");
        payCancel.Status = PaymentStatuses.Cancelled;
        _db.Payments.AddRange(payDraft, payFinal, payReversed, payCancel);
        await _db.SaveChangesAsync(cancellationToken);

        _db.PaymentAllocations.AddRange(
            PayAlloc(tid, payDraft.Id, apOpen.Id, 500_000m, SettlementAllocationStatuses.Draft),
            PayAlloc(tid, payFinal.Id, apPartialSettle.Id, 400_000m, SettlementAllocationStatuses.Finalized, now),
            PayAlloc(tid, payReversed.Id, apPartialSettle.Id, 200_000m, SettlementAllocationStatuses.Reversed, now, now, "Demo đảo phân bổ"));

        // --- AR flow (DEMO-04-AR) ---
        var arRev = Revenue(tid, billAr.Id, RevenueMaturities.Actual, 4_000_000m, today, "AR-FREIGHT", customer.Id);
        arRev.ConfirmedAmount = 4_000_000m;
        arRev.ActualAmount = 4_000_000m;
        _db.Revenues.Add(arRev);
        await _db.SaveChangesAsync(cancellationToken);

        var rxOpen = ExposureRec(tid, billAr.Id, customer.Id, arRev.Id, 1_000_000m, 0m, ExposureStatuses.Open, today);
        var rxFull = ExposureRec(tid, billAr.Id, customer.Id, arRev.Id, 3_000_000m, 3_000_000m, ExposureStatuses.FullyRecognized, today);
        _db.ReceivableExposures.AddRange(rxOpen, rxFull);
        await _db.SaveChangesAsync(cancellationToken);

        var arOpen = Ar(tid, rxFull.Id, billAr.Id, customer.Id, 1_500_000m, 0m, 0m, ApArSettlementStatuses.Open, today.AddDays(20), now);
        var arPartial = Ar(tid, rxFull.Id, billAr.Id, customer.Id, 1_000_000m, 0m, 400_000m, ApArSettlementStatuses.PartiallySettled, today.AddDays(10), now);
        var arSettled = Ar(tid, rxFull.Id, billAr.Id, customer.Id, 500_000m, 0m, 500_000m, ApArSettlementStatuses.Settled, today, now);
        _db.AccountsReceivable.AddRange(arOpen, arPartial, arSettled);
        await _db.SaveChangesAsync(cancellationToken);

        var colDraft = Col(tid, billAr.Id, customer.Id, 1_500_000m, today, "DEMO-COL-DRAFT", "Chờ phân bổ thu");
        var colFinal = Col(tid, billAr.Id, customer.Id, 400_000m, today, "DEMO-COL-FINAL", "Đã chốt phân bổ thu");
        _db.Collections.AddRange(colDraft, colFinal);
        await _db.SaveChangesAsync(cancellationToken);

        _db.CollectionAllocations.AddRange(
            ColAlloc(tid, colDraft.Id, arOpen.Id, 800_000m, SettlementAllocationStatuses.Draft),
            ColAlloc(tid, colFinal.Id, arPartial.Id, 400_000m, SettlementAllocationStatuses.Finalized, now));

        // --- Financial closes ---
        var closeOpen = Close(tid, FinancialCloseScopeTypes.Bill, billCloseOpen.Id, FinancialCloseStatuses.Open, today.AddDays(-7), today);
        closeOpen.Notes = "Demo: lần chốt đang mở — bấm tạo snapshot";
        closeOpen.StartedAt = now.AddDays(-1);

        var closeLocked = Close(tid, FinancialCloseScopeTypes.Bill, billCloseLocked.Id, FinancialCloseStatuses.Locked, today.AddDays(-30), today.AddDays(-1));
        closeLocked.Notes = "Demo: đã khóa + snapshot bất biến";
        closeLocked.StartedAt = now.AddDays(-5);
        closeLocked.LockedAt = now.AddDays(-1);

        var closeReopened = Close(tid, FinancialCloseScopeTypes.Bill, billCloseReopen.Id, FinancialCloseStatuses.Reopened, today.AddDays(-14), today);
        closeReopened.Notes = "Demo: đã mở lại sau khóa";
        closeReopened.StartedAt = now.AddDays(-10);
        closeReopened.LockedAt = now.AddDays(-3);
        closeReopened.ReopenedAt = now.AddHours(-6);
        closeReopened.ReopenReason = "Demo: bổ sung chứng từ muộn";

        var closePeriod = Close(tid, FinancialCloseScopeTypes.Period, null, FinancialCloseStatuses.Open, today.AddDays(-today.Day + 1), today);
        closePeriod.Notes = "Demo: chốt theo kỳ tháng";
        closePeriod.StartedAt = now;

        _db.FinancialCloses.AddRange(closeOpen, closeLocked, closeReopened, closePeriod);
        await _db.SaveChangesAsync(cancellationToken);

        // Snapshot for locked + historical for reopened
        await AddSnapshotAsync(tid, closeLocked, 1, now.AddDays(-1),
            costTotal: 500_000m, revenueTotal: 800_000m, apOut: 0m, arOut: 0m, cancellationToken);
        await AddSnapshotAsync(tid, closeReopened, 1, now.AddDays(-3),
            costTotal: 120_000m, revenueTotal: 0m, apOut: 0m, arOut: 0m, cancellationToken);

        // --- Control desk ---
        var recon = new Reconciliation
        {
            TenantId = tid,
            ReconciliationType = ReconciliationTypes.PaymentAp,
            RuleCode = "DEMO_RECON",
            Status = ReconciliationStatuses.InProgress,
            BillId = billControl.Id,
            Notes = "Demo đối soát AP",
            StartedAt = now
        };
        _db.Reconciliations.Add(recon);
        await _db.SaveChangesAsync(cancellationToken);

        var varianceOpen = new Variance
        {
            TenantId = tid,
            ReconciliationId = recon.Id,
            VarianceType = VarianceTypes.Amount,
            Amount = 150_000m,
            CurrencyCode = "VND",
            SourceType = "accounts_payable",
            SourceId = apOpen.Id,
            Status = VarianceStatuses.Open,
            Severity = VarianceSeverities.Medium,
            Explanation = "Demo chênh lệch chưa xử lý"
        };
        var varianceCleared = new Variance
        {
            TenantId = tid,
            ReconciliationId = recon.Id,
            VarianceType = VarianceTypes.Amount,
            Amount = 20_000m,
            CurrencyCode = "VND",
            SourceType = "payment",
            SourceId = payFinal.Id,
            Status = VarianceStatuses.Cleared,
            Severity = VarianceSeverities.Low,
            Explanation = "Demo đã clear"
        };
        _db.Variances.AddRange(varianceOpen, varianceCleared);
        await _db.SaveChangesAsync(cancellationToken);

        _db.Exceptions.AddRange(
            new FinancialException
            {
                TenantId = tid,
                RuleCode = "DEMO_OPEN",
                Severity = ExceptionSeverities.High,
                Status = ExceptionStatuses.Open,
                Title = "Chứng từ chờ chấp nhận quá hạn",
                Description = "Demo: ngoại lệ mở",
                BillId = billControl.Id,
                ObjectType = "document",
                ObjectId = docNotAccepted.Id,
                DueAt = now.AddHours(12)
            },
            new FinancialException
            {
                TenantId = tid,
                RuleCode = "DEMO_ESCALATED",
                Severity = ExceptionSeverities.Critical,
                Status = ExceptionStatuses.Escalated,
                Title = "Chênh lệch AP vượt ngưỡng",
                Description = "Demo: đã escalate",
                BillId = billControl.Id,
                VarianceId = varianceOpen.Id,
                ObjectType = "accounts_payable",
                ObjectId = apOpen.Id,
                EscalatedAt = now,
                EscalationReason = "Demo escalate",
                DueAt = now.AddHours(4)
            },
            new FinancialException
            {
                TenantId = tid,
                RuleCode = "DEMO_RESOLVED",
                Severity = ExceptionSeverities.Medium,
                Status = ExceptionStatuses.Resolved,
                Title = "Sai số nhỏ đã xử lý",
                BillId = billControl.Id,
                ResolvedAt = now,
                ResolutionNotes = "Demo resolved"
            });

        _db.Approvals.AddRange(
            new Approval
            {
                TenantId = tid,
                ObjectType = ApprovalObjectTypes.Document,
                ObjectId = docAcceptedUnmatched.Id,
                Status = ApprovalStatuses.Pending,
                RequiredLevel = 1,
                RequestReason = "Demo: chờ phê duyệt chứng từ",
                RequestedAt = now
            },
            new Approval
            {
                TenantId = tid,
                ObjectType = ApprovalObjectTypes.Cost,
                ObjectId = costConfirmed.Id,
                Status = ApprovalStatuses.Approved,
                RequiredLevel = 1,
                CurrentLevel = 1,
                RequestReason = "Demo đã duyệt",
                RequestedAt = now.AddDays(-1),
                DecidedAt = now.AddHours(-2),
                DecisionReason = "OK"
            },
            new Approval
            {
                TenantId = tid,
                ObjectType = ApprovalObjectTypes.Payment,
                ObjectId = payDraft.Id,
                Status = ApprovalStatuses.Rejected,
                RequiredLevel = 2,
                CurrentLevel = 1,
                RequestReason = "Demo từ chối",
                RequestedAt = now.AddDays(-2),
                DecidedAt = now.AddDays(-1),
                DecisionReason = "Thiếu chứng từ gốc"
            });

        // Rate card draft + published (master)
        var card = new RateCard
        {
            TenantId = tid,
            Code = "DEMO-RC-VENDOR",
            Name = "Bảng giá nhà xe demo",
            PartyType = "vendor",
            IsActive = true
        };
        _db.RateCards.Add(card);
        await _db.SaveChangesAsync(cancellationToken);

        var verDraft = new RateVersion
        {
            TenantId = tid,
            RateCardId = card.Id,
            VersionNo = 1,
            Status = RateVersionStatuses.Draft,
            EffectiveFrom = now
        };
        var verPub = new RateVersion
        {
            TenantId = tid,
            RateCardId = card.Id,
            VersionNo = 2,
            Status = RateVersionStatuses.Published,
            EffectiveFrom = now.AddDays(-30),
            PublishedAt = now.AddDays(-30)
        };
        _db.RateVersions.AddRange(verDraft, verPub);

        await _db.SaveChangesAsync(cancellationToken);

        var summary =
            "Seeded: 12 bills, costs/revenues maturity, shared alloc, docs triad, AP/AR+settlement, " +
            "closes open/locked/reopened, exceptions/approvals/recon, parties+rate card.";
        _logger.LogInformation("Demo scenario seeded: {Summary}", summary);
        return summary;
    }

    private async Task AddSnapshotAsync(
        Guid tid,
        FinancialClose close,
        int version,
        DateTimeOffset closedAt,
        decimal costTotal,
        decimal revenueTotal,
        decimal apOut,
        decimal arOut,
        CancellationToken cancellationToken)
    {
        var metrics = new (string Key, decimal Value, string? Ccy, string? Src)[]
        {
            ("cost_count", 1m, null, "cost"),
            ("cost_total", costTotal, "VND", "cost"),
            ("revenue_count", revenueTotal > 0 ? 1m : 0m, null, "revenue"),
            ("revenue_total", revenueTotal, "VND", "revenue"),
            ("ap_count", 0m, null, "accounts_payable"),
            ("ap_outstanding_total", apOut, "VND", "accounts_payable"),
            ("ar_count", 0m, null, "accounts_receivable"),
            ("ar_outstanding_total", arOut, "VND", "accounts_receivable")
        };

        var hash = ComputeHash(close.Id, version, close.PolicyVersion, close.BaseCurrency, metrics);
        var snap = new FinancialCloseSnapshot
        {
            TenantId = tid,
            FinancialCloseId = close.Id,
            ScopeType = close.ScopeType,
            ScopeId = close.ScopeId,
            SnapshotVersion = version,
            ClosedAt = closedAt,
            PolicyVersion = close.PolicyVersion,
            BaseCurrency = close.BaseCurrency,
            ImmutableHash = hash
        };
        _db.FinancialCloseSnapshots.Add(snap);
        await _db.SaveChangesAsync(cancellationToken);

        var line = 1;
        foreach (var m in metrics)
        {
            _db.FinancialCloseSnapshotDetails.Add(new FinancialCloseSnapshotDetail
            {
                TenantId = tid,
                SnapshotId = snap.Id,
                LineNo = line++,
                MetricKey = m.Key,
                MetricValue = m.Value,
                CurrencyCode = m.Ccy,
                SourceType = m.Src
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string ComputeHash(
        Guid closeId,
        int snapshotVersion,
        string policyVersion,
        string baseCurrency,
        IEnumerable<(string Key, decimal Value, string? Ccy, string? Src)> metrics)
    {
        var sb = new StringBuilder();
        sb.Append(closeId).Append('|')
            .Append(snapshotVersion).Append('|')
            .Append(policyVersion).Append('|')
            .Append(baseCurrency).Append('|');
        foreach (var m in metrics.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            sb.Append(m.Key).Append('=')
                .Append(m.Value.ToString("0.####"))
                .Append(';')
                .Append(m.Ccy ?? "")
                .Append('|');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }

    /// <summary>Seeds baseline Cost/Revenue/Service types so standalone tenants can pick from a catalog.</summary>
    private async Task EnsureDefaultCatalogAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var defaults = new (string Kind, string Code, string Name, int Sort)[]
        {
            (MasterCatalogKinds.CostType, "FREIGHT", "Cước vận chuyển", 10),
            (MasterCatalogKinds.CostType, "AP-FREIGHT", "Cước phải trả", 20),
            (MasterCatalogKinds.CostType, "FUEL", "Phụ phí nhiên liệu", 30),
            (MasterCatalogKinds.RevenueType, "FREIGHT-REV", "Doanh thu cước", 10),
            (MasterCatalogKinds.RevenueType, "AR-FREIGHT", "Doanh thu phải thu", 20),
            (MasterCatalogKinds.ServiceType, "FREIGHT", "Vận tải hàng", 10),
            (MasterCatalogKinds.ServiceType, "AIR", "Hàng không", 20),
            (MasterCatalogKinds.PricingComponent, "FREIGHT", "Cước chính", 10),
            (MasterCatalogKinds.DocumentType, "invoice", "Hóa đơn", 10),
            (MasterCatalogKinds.DocumentType, "debit_note", "Debit note", 20),
            (MasterCatalogKinds.PaymentTerm, "NET15", "Net 15 ngày", 10),
            (MasterCatalogKinds.PaymentTerm, "NET30", "Net 30 ngày", 20)
        };

        var existing = await _db.MasterCatalogItems
            .IgnoreQueryFilters()
            .Where(i => i.TenantId == tenantId && i.DeletedAt == null)
            .Select(i => i.Kind + ":" + i.Code)
            .ToListAsync(cancellationToken);
        var set = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var (kind, code, name, sort) in defaults)
        {
            var key = kind + ":" + code;
            if (set.Contains(key))
            {
                continue;
            }

            _db.MasterCatalogItems.Add(new MasterCatalogItem
            {
                TenantId = tenantId,
                Kind = kind,
                Code = code,
                Name = name,
                IsActive = true,
                SortOrder = sort
            });
            added++;
        }

        if (added > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private static Bill Bill(Guid tid, string no, Guid? orgId, string? _) => new()
    {
        TenantId = tid,
        BillNo = no,
        BillType = "freight",
        SourceSystem = "demo",
        ExternalId = no,
        OperationalStatus = "active",
        IsActive = true,
        OrganizationId = orgId
    };

    private static BusinessParty Party(
        Guid tid,
        string code,
        string name,
        bool active = true,
        string? legalName = null,
        string? taxId = null,
        string? phone = null,
        string? email = null,
        string? defaultCurrency = null,
        int? paymentTermDays = null,
        decimal? creditLimit = null) => new()
    {
        TenantId = tid,
        Code = code,
        Name = name,
        LegalName = legalName,
        TaxId = taxId,
        Phone = phone,
        Email = email,
        CountryCode = "VN",
        DefaultCurrencyCode = defaultCurrency,
        PaymentTermDays = paymentTermDays,
        CreditLimit = creditLimit,
        CreditLimitCurrencyCode = creditLimit.HasValue ? "VND" : null,
        IsActive = active,
        PartyKind = PartyKinds.Organization,
        CreditControlMode = PartyCreditControlModes.Advisory
    };

    private static PartyRole Role(Guid tid, Guid partyId, string code) => new()
    {
        TenantId = tid,
        PartyId = partyId,
        RoleCode = code,
        IsActive = true
    };

    private static Cost Cost(Guid tid, Guid? billId, string maturity, decimal amount, DateOnly date, string type, Guid? vendorId) => new()
    {
        TenantId = tid,
        BillId = billId,
        CostTypeCode = type,
        VendorPartyId = vendorId,
        FinancialMaturity = maturity,
        AttributionType = CostAttributionTypes.Direct,
        ExpectedAmount = amount,
        Amount = amount,
        CurrencyCode = "VND",
        BaseAmount = amount,
        SourceType = CostSourceTypes.Manual,
        EffectiveDate = date,
        RecordStatus = "active"
    };

    private static Revenue Revenue(Guid tid, Guid billId, string maturity, decimal amount, DateOnly date, string type, Guid? customerId) => new()
    {
        TenantId = tid,
        BillId = billId,
        RevenueTypeCode = type,
        CustomerPartyId = customerId,
        FinancialMaturity = maturity,
        ExpectedAmount = amount,
        Amount = amount,
        CurrencyCode = "VND",
        BaseAmount = amount,
        SourceType = RevenueSourceTypes.Manual,
        EffectiveDate = date,
        RecordStatus = "active"
    };

    private static CostAllocationDetail AllocDetail(Guid tid, Guid allocId, Guid billId, decimal ratio, decimal amount) => new()
    {
        TenantId = tid,
        AllocationId = allocId,
        BillId = billId,
        BasisValue = 1m,
        BasisRatio = ratio,
        AllocatedAmount = amount
    };

    private static FinancialDocument Doc(
        Guid tid, Guid billId, Guid? partyId, string no, string direction, string type,
        decimal amount, DateOnly date, DateTimeOffset? receivedAt,
        string receipt, string acceptance, string matching) => new()
    {
        TenantId = tid,
        BillId = billId,
        CounterpartyId = partyId,
        DocumentNo = no,
        DocumentType = type,
        Direction = direction,
        TotalAmount = amount,
        CurrencyCode = "VND",
        DocumentDate = date,
        ReceiptStatus = receipt,
        AcceptanceStatus = acceptance,
        MatchingStatus = matching,
        ReceivedAt = receivedAt,
        SourceSystem = "demo",
        Notes = $"Demo {no}"
    };

    private static FinancialDocumentLine Line(Guid tid, Guid docId, int no, decimal amount, Guid billId, decimal matched = 0m) => new()
    {
        TenantId = tid,
        DocumentId = docId,
        LineNo = no,
        Description = $"Dòng {no}",
        Amount = amount,
        MatchedAmount = matched,
        CurrencyCode = "VND",
        BillId = billId
    };

    private static PayableExposure ExposurePay(
        Guid tid, Guid billId, Guid? partyId, Guid? costId, decimal amount, decimal recognized, string status, DateOnly date) => new()
    {
        TenantId = tid,
        BillId = billId,
        CounterpartyId = partyId,
        CostId = costId,
        Amount = amount,
        RecognizedAmount = recognized,
        CurrencyCode = "VND",
        Status = status,
        EffectiveDate = date,
        DueDate = date.AddDays(30),
        Notes = $"Demo exposure {status}",
        RecordStatus = "active"
    };

    private static ReceivableExposure ExposureRec(
        Guid tid, Guid billId, Guid? partyId, Guid? revenueId, decimal amount, decimal recognized, string status, DateOnly date) => new()
    {
        TenantId = tid,
        BillId = billId,
        CounterpartyId = partyId,
        RevenueId = revenueId,
        Amount = amount,
        RecognizedAmount = recognized,
        CurrencyCode = "VND",
        Status = status,
        EffectiveDate = date,
        DueDate = date.AddDays(30),
        Notes = $"Demo exposure {status}",
        RecordStatus = "active"
    };

    private static AccountsPayable Ap(
        Guid tid, Guid expId, Guid billId, Guid? partyId,
        decimal recognized, decimal adj, decimal settled, string status, DateOnly? due, DateTimeOffset at) => new()
    {
        TenantId = tid,
        PayableExposureId = expId,
        BillId = billId,
        CounterpartyId = partyId,
        RecognizedAmount = recognized,
        AdjustmentAmount = adj,
        FinalizedSettledAmount = settled,
        CurrencyCode = "VND",
        DueDate = due,
        SettlementStatus = status,
        RecognizedAt = at,
        RecordStatus = "active",
        Notes = $"Demo AP {status}"
    };

    private static AccountsReceivable Ar(
        Guid tid, Guid expId, Guid billId, Guid? partyId,
        decimal recognized, decimal adj, decimal settled, string status, DateOnly? due, DateTimeOffset at) => new()
    {
        TenantId = tid,
        ReceivableExposureId = expId,
        BillId = billId,
        CounterpartyId = partyId,
        RecognizedAmount = recognized,
        AdjustmentAmount = adj,
        FinalizedSettledAmount = settled,
        CurrencyCode = "VND",
        DueDate = due,
        SettlementStatus = status,
        RecognizedAt = at,
        RecordStatus = "active",
        Notes = $"Demo AR {status}"
    };

    private static Payment Pay(Guid tid, Guid billId, Guid? partyId, decimal amount, DateOnly date, string reference, string notes) => new()
    {
        TenantId = tid,
        BillId = billId,
        CounterpartyId = partyId,
        Amount = amount,
        BaseAmount = amount,
        CurrencyCode = "VND",
        ValueDate = date,
        ReferenceNo = reference,
        Notes = notes,
        Status = PaymentStatuses.Open,
        RecordStatus = "active"
    };

    private static Collection Col(Guid tid, Guid billId, Guid? partyId, decimal amount, DateOnly date, string reference, string notes) => new()
    {
        TenantId = tid,
        BillId = billId,
        CounterpartyId = partyId,
        Amount = amount,
        BaseAmount = amount,
        CurrencyCode = "VND",
        ValueDate = date,
        ReferenceNo = reference,
        Notes = notes,
        Status = CollectionStatuses.Open,
        RecordStatus = "active"
    };

    private static PaymentAllocation PayAlloc(
        Guid tid, Guid paymentId, Guid apId, decimal amount, string status,
        DateTimeOffset? finalizedAt = null, DateTimeOffset? reversedAt = null, string? reverseReason = null) => new()
    {
        TenantId = tid,
        PaymentId = paymentId,
        AccountsPayableId = apId,
        Amount = amount,
        BaseAmount = amount,
        CurrencyCode = "VND",
        AllocationStatus = status,
        FinalizedAt = finalizedAt,
        ReversedAt = reversedAt,
        ReverseReason = reverseReason,
        Notes = $"Demo alloc {status}"
    };

    private static CollectionAllocation ColAlloc(
        Guid tid, Guid collectionId, Guid arId, decimal amount, string status,
        DateTimeOffset? finalizedAt = null) => new()
    {
        TenantId = tid,
        CollectionId = collectionId,
        AccountsReceivableId = arId,
        Amount = amount,
        BaseAmount = amount,
        CurrencyCode = "VND",
        AllocationStatus = status,
        FinalizedAt = finalizedAt,
        Notes = $"Demo alloc {status}"
    };

    private static FinancialClose Close(
        Guid tid, string scopeType, Guid? scopeId, string status, DateOnly from, DateOnly to) => new()
    {
        TenantId = tid,
        ScopeType = scopeType,
        ScopeId = scopeId,
        PeriodFrom = from,
        PeriodTo = to,
        VersionNo = 1,
        Status = status,
        PolicyVersion = FinancialClosePolicies.Controlled,
        BaseCurrency = "VND"
    };

    private async Task<string> EnsureReferenceTariffsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var existingCodes = await _db.RateCards.IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId)
            .Select(r => r.Code)
            .ToListAsync(cancellationToken);
        var have = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var commodityCodes = await _db.CommodityTypes.IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId)
            .Select(c => c.Code)
            .ToListAsync(cancellationToken);
        var commodities = commodityCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pending = false;
        foreach (var (code, name) in ReferenceTariffCatalog.Commodities)
        {
            if (!commodities.Add(code))
            {
                continue;
            }

            _db.CommodityTypes.Add(new CommodityType
            {
                TenantId = tenantId,
                Code = code,
                Name = name,
                Category = "reference-tariff",
                IsActive = true
            });
            pending = true;
        }

        var added = new List<string>();
        foreach (var spec in ReferenceTariffCatalog.Cards)
        {
            if (!have.Add(spec.Code))
            {
                continue;
            }

            var card = new RateCard
            {
                TenantId = tenantId,
                Code = spec.Code,
                Name = spec.Name,
                PartyType = "vendor",
                CurrencyCode = spec.CurrencyCode,
                Description = spec.Description,
                TransportMode = spec.TransportMode,
                RouteCode = ReferenceTariffCatalog.Route,
                CarrierName = ReferenceTariffCatalog.Carrier,
                IsActive = true
            };
            var version = new RateVersion
            {
                TenantId = tenantId,
                RateCardId = card.Id,
                VersionNo = 1,
                Status = RateVersionStatuses.Published,
                EffectiveFrom = spec.EffectiveFrom,
                PublishedAt = spec.EffectiveFrom,
                Note = spec.Note
            };
            _db.RateCards.Add(card);
            _db.RateVersions.Add(version);

            foreach (var ruleSpec in spec.Rules)
            {
                var rule = new PricingRule
                {
                    TenantId = tenantId,
                    RateVersionId = version.Id,
                    Code = ruleSpec.Code,
                    Name = ruleSpec.Name,
                    CalcMethod = ruleSpec.CalcMethod,
                    UnitAmount = ruleSpec.UnitAmount,
                    CurrencyCode = ruleSpec.CurrencyCode,
                    ChargeCode = ruleSpec.ChargeCode,
                    CommodityCode = ruleSpec.CommodityCode,
                    DestinationCode = ruleSpec.DestinationCode,
                    Applicability = ruleSpec.Applicability,
                    MinAmount = ruleSpec.MinAmount,
                    VolumetricFactor = ruleSpec.VolumetricFactor,
                    SortOrder = ruleSpec.SortOrder,
                    IsActive = true
                };
                _db.PricingRules.Add(rule);
                foreach (var band in ruleSpec.Breaks)
                {
                    _db.RateBreaks.Add(new RateBreak
                    {
                        TenantId = tenantId,
                        PricingRuleId = rule.Id,
                        SequenceNo = band.SequenceNo,
                        MinQuantity = band.MinQuantity,
                        MaxQuantity = band.MaxQuantity,
                        UnitAmount = band.UnitAmount
                    });
                }
            }

            added.Add(spec.Code);
            pending = true;
        }

        if (pending)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return added.Count == 0
            ? "Bảng giá mẫu VN–MY đã có."
            : "Đã phát hành bảng giá mẫu: " + string.Join(", ", added) + ".";
    }
}

public sealed record DemoSeedResult(
    Guid TenantId,
    bool Skipped,
    string Summary,
    IReadOnlyDictionary<string, int>? Counts = null);
