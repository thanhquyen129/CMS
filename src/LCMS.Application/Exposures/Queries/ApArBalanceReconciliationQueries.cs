using System.Text.Json;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Identity;
using LCMS.Application.Settlements;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Exposures.Queries;

public sealed record ApArLegacyAllocationDto(
    Guid AllocationId,
    Guid CashId,
    string? CashReference,
    decimal CashAmount,
    string CashCurrencyCode,
    decimal SettledAmount,
    DateTimeOffset ReversedAt);

public sealed record ApArBalanceMismatchDto(
    string Kind,
    Guid AccountId,
    Guid? BillId,
    string? BillNo,
    string CurrencyCode,
    string RecordStatus,
    decimal CurrentOutstanding,
    decimal LedgerBalance,
    decimal Difference,
    decimal StoredAdjustmentAmount,
    decimal DerivedAdjustmentAmount,
    decimal StoredSettledAmount,
    decimal DerivedSettledAmount,
    string Cause,
    bool Correctable,
    byte[]? RowVersion,
    IReadOnlyList<ApArLegacyAllocationDto> LegacyAllocations);

public sealed record ApArBalanceCorrectionDto(
    Guid AuditEventId,
    string Kind,
    Guid AccountId,
    DateTimeOffset OccurredAt,
    Guid? ActorId,
    string? ActorName,
    string? Reason,
    decimal? OutstandingBefore,
    decimal? OutstandingAfter,
    string? CurrencyCode);

public sealed record ApArBalanceReconciliationDto(
    DateTimeOffset GeneratedAt,
    bool IncludesReceivables,
    bool IncludesPayables,
    int CheckedReceivables,
    int CheckedPayables,
    IReadOnlyList<ApArBalanceMismatchDto> Items,
    IReadOnlyList<ApArBalanceCorrectionDto> RecentCorrections);

/// <summary>
/// FIN-DATA-01 inventory: every AR/AP whose stored balance differs from the ledger derived from source rows.
/// Needs apar.reconcile; AR rows additionally need revenue.read scope, AP rows cost.read scope (Cost ≠ Revenue).
/// </summary>
public sealed record GetApArBalanceReconciliationQuery : IRequest<ApArBalanceReconciliationDto>;

public sealed class GetApArBalanceReconciliationQueryHandler
    : IRequestHandler<GetApArBalanceReconciliationQuery, ApArBalanceReconciliationDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _userContext;
    private readonly IPermissionService _permissions;
    private readonly IOrganizationHierarchyService _orgHierarchy;

    public GetApArBalanceReconciliationQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext userContext,
        IPermissionService permissions,
        IOrganizationHierarchyService orgHierarchy)
    {
        _db = db;
        _tenantContext = tenantContext;
        _userContext = userContext;
        _permissions = permissions;
        _orgHierarchy = orgHierarchy;
    }

    public async Task<ApArBalanceReconciliationDto> Handle(
        GetApArBalanceReconciliationQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.ApArReconcile,
            "Bạn không có quyền đối soát số dư công nợ.",
            cancellationToken);

        var canRevenue = await _permissions.HasPermissionAsync(PermissionCodes.RevenueRead, cancellationToken);
        var canCost = await _permissions.HasPermissionAsync(PermissionCodes.CostRead, cancellationToken);
        if (!canRevenue && !canCost)
        {
            throw new ForbiddenAppException("Cần quyền xem doanh thu hoặc chi phí để đối soát công nợ.");
        }

        var items = new List<ApArBalanceMismatchDto>();
        var checkedAr = 0;
        var checkedAp = 0;

        if (canRevenue)
        {
            var (scope, subtree) = await DataScopeFilter.ResolveAsync(
                _permissions, _userContext, _db, _orgHierarchy,
                PermissionCodes.RevenueRead, "Bạn không có quyền xem khoản phải thu.", cancellationToken);
            var ars = await _db.AccountsReceivable.AsNoTracking().ToListAsync(cancellationToken);
            checkedAr = ars.Count;
            var adjustments = (await _db.AccountsReceivableAdjustments.AsNoTracking()
                    .Select(x => new { x.AccountsReceivableId, x.DeltaAmount })
                    .ToListAsync(cancellationToken))
                .ToLookup(x => x.AccountsReceivableId, x => x.DeltaAmount);
            var allocations = (await _db.CollectionAllocations.AsNoTracking()
                    .Where(a => a.FinalizedAt != null)
                    .Select(a => new
                    {
                        a.AccountsReceivableId,
                        Snapshot = new ApArBalanceAnalyzer.AllocationSnapshot(
                            a.Id, a.CollectionId, a.Amount, a.CurrencyCode, a.SettledAmount, a.FinalizedAt, a.ReversedAt)
                    })
                    .ToListAsync(cancellationToken))
                .ToLookup(x => x.AccountsReceivableId, x => x.Snapshot);

            var mismatches = ars
                .Select(ar => (ar, result: ApArBalanceAnalyzer.Analyze(
                    new(ar.RecognizedAmount, ar.AdjustmentAmount, ar.FinalizedSettledAmount, ar.CurrencyCode),
                    adjustments[ar.Id],
                    allocations[ar.Id])))
                .Where(x => !x.result.Reconciled)
                .ToList();
            if (mismatches.Count > 0)
            {
                var bills = await LoadBillsAsync(mismatches.Select(m => m.ar.BillId), cancellationToken);
                var cashIds = LegacyCashIds(mismatches.Select(m => m.result));
                var cashRefs = await _db.Collections.AsNoTracking()
                    .Where(c => cashIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id, c => c.ReferenceNo, cancellationToken);
                foreach (var (ar, result) in mismatches)
                {
                    var bill = ar.BillId is Guid b && bills.TryGetValue(b, out var info) ? info : null;
                    if (!DataScopeFilter.AllowsViaBillOrg(scope, _userContext.UserId, subtree, ar.CreatedBy, bill?.OrganizationId))
                    {
                        continue;
                    }

                    items.Add(ToDto("ar", ar.Id, ar.BillId, bill?.BillNo, ar.CurrencyCode, ar.RecordStatus,
                        ar.AdjustmentAmount, ar.FinalizedSettledAmount, ar.RowVersion, result, cashRefs));
                }
            }
        }

        if (canCost)
        {
            var (scope, subtree) = await DataScopeFilter.ResolveAsync(
                _permissions, _userContext, _db, _orgHierarchy,
                PermissionCodes.CostRead, "Bạn không có quyền xem khoản phải trả.", cancellationToken);
            var aps = await _db.AccountsPayable.AsNoTracking().ToListAsync(cancellationToken);
            checkedAp = aps.Count;
            var adjustments = (await _db.AccountsPayableAdjustments.AsNoTracking()
                    .Select(x => new { x.AccountsPayableId, x.DeltaAmount })
                    .ToListAsync(cancellationToken))
                .ToLookup(x => x.AccountsPayableId, x => x.DeltaAmount);
            var allocations = (await _db.PaymentAllocations.AsNoTracking()
                    .Where(a => a.FinalizedAt != null)
                    .Select(a => new
                    {
                        a.AccountsPayableId,
                        Snapshot = new ApArBalanceAnalyzer.AllocationSnapshot(
                            a.Id, a.PaymentId, a.Amount, a.CurrencyCode, a.SettledAmount, a.FinalizedAt, a.ReversedAt)
                    })
                    .ToListAsync(cancellationToken))
                .ToLookup(x => x.AccountsPayableId, x => x.Snapshot);

            var mismatches = aps
                .Select(ap => (ap, result: ApArBalanceAnalyzer.Analyze(
                    new(ap.RecognizedAmount, ap.AdjustmentAmount, ap.FinalizedSettledAmount, ap.CurrencyCode),
                    adjustments[ap.Id],
                    allocations[ap.Id])))
                .Where(x => !x.result.Reconciled)
                .ToList();
            if (mismatches.Count > 0)
            {
                var bills = await LoadBillsAsync(mismatches.Select(m => m.ap.BillId), cancellationToken);
                var cashIds = LegacyCashIds(mismatches.Select(m => m.result));
                var cashRefs = await _db.Payments.AsNoTracking()
                    .Where(p => cashIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p.ReferenceNo, cancellationToken);
                foreach (var (ap, result) in mismatches)
                {
                    var bill = ap.BillId is Guid b && bills.TryGetValue(b, out var info) ? info : null;
                    if (!DataScopeFilter.AllowsViaBillOrg(scope, _userContext.UserId, subtree, ap.CreatedBy, bill?.OrganizationId))
                    {
                        continue;
                    }

                    items.Add(ToDto("ap", ap.Id, ap.BillId, bill?.BillNo, ap.CurrencyCode, ap.RecordStatus,
                        ap.AdjustmentAmount, ap.FinalizedSettledAmount, ap.RowVersion, result, cashRefs));
                }
            }
        }

        var corrections = await LoadCorrectionsAsync(canRevenue, canCost, cancellationToken);

        return new ApArBalanceReconciliationDto(
            DateTimeOffset.UtcNow,
            canRevenue,
            canCost,
            checkedAr,
            checkedAp,
            items.OrderByDescending(i => Math.Abs(i.Difference)).ToList(),
            corrections);
    }

    private sealed record BillInfo(string BillNo, Guid? OrganizationId);

    private static List<Guid> LegacyCashIds(IEnumerable<ApArBalanceAnalyzer.Result> results) =>
        results.SelectMany(r => r.LegacyAllocations).Select(l => l.CashId).Distinct().ToList();

    private async Task<Dictionary<Guid, BillInfo>> LoadBillsAsync(
        IEnumerable<Guid?> billIds,
        CancellationToken cancellationToken)
    {
        var ids = billIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await _db.Bills.AsNoTracking()
            .Where(b => ids.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => new BillInfo(b.BillNo, b.OrganizationId), cancellationToken);
    }

    private static ApArBalanceMismatchDto ToDto(
        string kind,
        Guid accountId,
        Guid? billId,
        string? billNo,
        string currencyCode,
        string recordStatus,
        decimal storedAdjustment,
        decimal storedSettled,
        byte[]? rowVersion,
        ApArBalanceAnalyzer.Result result,
        IReadOnlyDictionary<Guid, string?> cashRefs) =>
        new(
            kind,
            accountId,
            billId,
            billNo,
            currencyCode,
            recordStatus,
            result.CurrentOutstanding,
            result.LedgerBalance,
            result.Difference,
            storedAdjustment,
            result.DerivedAdjustmentAmount,
            storedSettled,
            result.DerivedSettledAmount,
            result.Cause ?? ApArBalanceAnalyzer.CauseUnexplained,
            result.Correctable,
            rowVersion,
            result.LegacyAllocations
                .Select(l => new ApArLegacyAllocationDto(
                    l.AllocationId,
                    l.CashId,
                    cashRefs.TryGetValue(l.CashId, out var reference) ? reference : null,
                    l.CashAmount,
                    l.CashCurrencyCode,
                    l.SettledAmount,
                    l.ReversedAt))
                .ToList());

    private async Task<IReadOnlyList<ApArBalanceCorrectionDto>> LoadCorrectionsAsync(
        bool canRevenue,
        bool canCost,
        CancellationToken cancellationToken)
    {
        var actions = new List<string>();
        if (canRevenue)
        {
            actions.Add(AuditActions.AccountsReceivableSettlementCorrection);
        }

        if (canCost)
        {
            actions.Add(AuditActions.AccountsPayableSettlementCorrection);
        }

        var events = await _db.AuditEvents.AsNoTracking()
            .Where(e => actions.Contains(e.Action))
            .ToListAsync(cancellationToken);
        var recent = events.OrderByDescending(e => e.OccurredAt).Take(50).ToList();
        var actorIds = recent.Where(e => e.ActorId.HasValue).Select(e => e.ActorId!.Value).Distinct().ToList();
        var names = actorIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users.AsNoTracking()
                .Where(u => actorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        return recent
            .Select(e => new ApArBalanceCorrectionDto(
                e.Id,
                e.Action == AuditActions.AccountsReceivableSettlementCorrection ? "ar" : "ap",
                e.ObjectId,
                e.OccurredAt,
                e.ActorId,
                e.ActorId is Guid id && names.TryGetValue(id, out var name) ? name : null,
                e.Reason,
                ReadDecimal(e.BeforeJson, "outstanding"),
                ReadDecimal(e.AfterJson, "outstanding"),
                ReadString(e.AfterJson, "currency")))
            .ToList();
    }

    private static decimal? ReadDecimal(string? json, string property)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
                ? value.GetDecimal()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(string? json, string property)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
