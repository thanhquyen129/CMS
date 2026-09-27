using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Identity;
using LCMS.Application.Settlements;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Exposures.Queries;

/// <summary>
/// AR/AP transaction ledger (ADR-0037): derived read model over immutable source rows —
/// recognition, adjustment rows (adjustment / write_off / write_off_reversal / reverse_recognize),
/// finalized and reversed settlement allocations. Balance is a running sum in AR/AP currency and is
/// reconciled against <c>DeriveOutstanding()</c>; a mismatch is surfaced, never hidden.
/// </summary>
public sealed record ApArLedgerEntryDto(
    string Id,
    string EntryType,
    decimal Amount,
    string CurrencyCode,
    decimal BalanceBefore,
    decimal BalanceAfter,
    DateOnly BusinessDate,
    DateTimeOffset OccurredAt,
    Guid? ActorId,
    string? ActorName,
    string? Reason,
    string SourceType,
    Guid SourceId,
    string? SourceLabel,
    Guid? CashId,
    decimal? CashAmount,
    string? CashCurrencyCode,
    string? ReversesEntryId,
    string? ReversedByEntryId,
    string Status);

public sealed record ApArLedgerDto(
    Guid AccountId,
    string Kind,
    Guid? BillId,
    string? BillNo,
    string CurrencyCode,
    decimal CurrentOutstanding,
    decimal LedgerBalance,
    bool Reconciled,
    IReadOnlyList<ApArLedgerEntryDto> Entries);

public static class ApArLedgerEntryTypes
{
    public const string Recognition = "recognition";
    public const string Adjustment = ApArAdjustmentTypes.Adjustment;
    public const string WriteOff = ApArAdjustmentTypes.WriteOff;
    public const string WriteOffReversal = ApArAdjustmentTypes.WriteOffReversal;
    public const string ReverseRecognize = ApArAdjustmentTypes.ReverseRecognize;
    public const string Allocation = "allocation";
    public const string AllocationReversal = "allocation_reversal";
}

public static class ApArLedgerStatuses
{
    public const string Posted = "posted";
    public const string Reversed = "reversed";
}

public sealed record GetAccountsReceivableLedgerQuery(Guid AccountsReceivableId) : IRequest<ApArLedgerDto>;

public sealed record GetAccountsPayableLedgerQuery(Guid AccountsPayableId) : IRequest<ApArLedgerDto>;

/// <summary>Bill-level financial history: AR ledgers (needs revenue.read) + AP ledgers (needs cost.read).</summary>
public sealed record GetBillFinancialHistoryQuery(Guid BillId) : IRequest<IReadOnlyList<ApArLedgerDto>>;

internal sealed record LedgerRow(
    string Id,
    string EntryType,
    decimal Amount,
    DateOnly BusinessDate,
    DateTimeOffset OccurredAt,
    Guid? ActorId,
    string? Reason,
    string SourceType,
    Guid SourceId,
    string? SourceLabel,
    Guid? CashId,
    decimal? CashAmount,
    string? CashCurrencyCode,
    string? ReversesEntryId,
    string? ReversedByEntryId,
    string Status,
    int Order);

internal static class ApArLedgerBuilder
{
    private const decimal Tolerance = 0.0001m;

    public static async Task<ApArLedgerDto> BuildReceivableAsync(
        ILcmsDbContext db,
        AccountsReceivable ar,
        string? billNo,
        CancellationToken cancellationToken)
    {
        var rows = new List<LedgerRow>
        {
            new(
                $"recognition:{ar.Id}",
                ApArLedgerEntryTypes.Recognition,
                ar.RecognizedAmount,
                DateOnly.FromDateTime(ar.RecognizedAt.UtcDateTime),
                ar.RecognizedAt,
                ar.RecognizedBy ?? ar.CreatedBy,
                null,
                AuditObjectTypes.AccountsReceivable,
                ar.Id,
                null, null, null, null, null, null,
                ApArLedgerStatuses.Posted,
                0)
        };

        var adjustments = await db.AccountsReceivableAdjustments.AsNoTracking()
            .Where(x => x.AccountsReceivableId == ar.Id)
            .Select(x => new AdjustmentView(
                x.Id, x.AdjustmentType, x.DeltaAmount, x.Reason, x.EffectiveDate,
                x.CreatedAt, x.CreatedBy, x.ReversesAdjustmentId))
            .ToListAsync(cancellationToken);
        rows.AddRange(MapAdjustments(adjustments, "ar_adjustment"));

        var allocations = await db.CollectionAllocations.AsNoTracking()
            .Where(a => a.AccountsReceivableId == ar.Id && a.FinalizedAt != null)
            .Join(
                db.Collections.AsNoTracking(),
                a => a.CollectionId,
                c => c.Id,
                (a, c) => new AllocationView(
                    a.Id, a.CollectionId, a.Amount, c.CurrencyCode, a.SettledAmount,
                    a.FinalizedAt, a.FinalizedBy, a.ReversedAt, a.ReversedBy, a.ReverseReason,
                    c.ReferenceNo, c.ValueDate))
            .ToListAsync(cancellationToken);
        rows.AddRange(MapAllocations(allocations, AuditObjectTypes.CollectionAllocation));

        return await ComposeAsync(
            db, ar.Id, "ar", ar.BillId, billNo, ar.CurrencyCode, ar.DeriveOutstanding(), rows, cancellationToken);
    }

    public static async Task<ApArLedgerDto> BuildPayableAsync(
        ILcmsDbContext db,
        AccountsPayable ap,
        string? billNo,
        CancellationToken cancellationToken)
    {
        var rows = new List<LedgerRow>
        {
            new(
                $"recognition:{ap.Id}",
                ApArLedgerEntryTypes.Recognition,
                ap.RecognizedAmount,
                DateOnly.FromDateTime(ap.RecognizedAt.UtcDateTime),
                ap.RecognizedAt,
                ap.RecognizedBy ?? ap.CreatedBy,
                null,
                AuditObjectTypes.AccountsPayable,
                ap.Id,
                null, null, null, null, null, null,
                ApArLedgerStatuses.Posted,
                0)
        };

        var adjustments = await db.AccountsPayableAdjustments.AsNoTracking()
            .Where(x => x.AccountsPayableId == ap.Id)
            .Select(x => new AdjustmentView(
                x.Id, x.AdjustmentType, x.DeltaAmount, x.Reason, x.EffectiveDate,
                x.CreatedAt, x.CreatedBy, x.ReversesAdjustmentId))
            .ToListAsync(cancellationToken);
        rows.AddRange(MapAdjustments(adjustments, "ap_adjustment"));

        var allocations = await db.PaymentAllocations.AsNoTracking()
            .Where(a => a.AccountsPayableId == ap.Id && a.FinalizedAt != null)
            .Join(
                db.Payments.AsNoTracking(),
                a => a.PaymentId,
                p => p.Id,
                (a, p) => new AllocationView(
                    a.Id, a.PaymentId, a.Amount, p.CurrencyCode, a.SettledAmount,
                    a.FinalizedAt, a.FinalizedBy, a.ReversedAt, a.ReversedBy, a.ReverseReason,
                    p.ReferenceNo, p.ValueDate))
            .ToListAsync(cancellationToken);
        rows.AddRange(MapAllocations(allocations, AuditObjectTypes.PaymentAllocation));

        return await ComposeAsync(
            db, ap.Id, "ap", ap.BillId, billNo, ap.CurrencyCode, ap.DeriveOutstanding(), rows, cancellationToken);
    }

    private sealed record AdjustmentView(
        Guid Id,
        string AdjustmentType,
        decimal DeltaAmount,
        string Reason,
        DateOnly EffectiveDate,
        DateTimeOffset CreatedAt,
        Guid? CreatedBy,
        Guid? ReversesAdjustmentId);

    private sealed record AllocationView(
        Guid Id,
        Guid CashId,
        decimal CashAmount,
        string CashCurrencyCode,
        decimal? SettledAmount,
        DateTimeOffset? FinalizedAt,
        Guid? FinalizedBy,
        DateTimeOffset? ReversedAt,
        Guid? ReversedBy,
        string? ReverseReason,
        string? ReferenceNo,
        DateOnly ValueDate);

    private static IEnumerable<LedgerRow> MapAdjustments(List<AdjustmentView> adjustments, string sourceType)
    {
        var reversedBy = adjustments
            .Where(x => x.ReversesAdjustmentId.HasValue)
            .ToDictionary(x => x.ReversesAdjustmentId!.Value, x => x.Id);

        foreach (var x in adjustments)
        {
            var isReversed = reversedBy.TryGetValue(x.Id, out var reversalId);
            yield return new LedgerRow(
                x.Id.ToString(),
                x.AdjustmentType,
                x.DeltaAmount,
                x.EffectiveDate,
                x.CreatedAt,
                x.CreatedBy,
                x.Reason,
                sourceType,
                x.Id,
                null, null, null, null,
                x.ReversesAdjustmentId?.ToString(),
                isReversed ? reversalId.ToString() : null,
                isReversed ? ApArLedgerStatuses.Reversed : ApArLedgerStatuses.Posted,
                1);
        }
    }

    private static IEnumerable<LedgerRow> MapAllocations(List<AllocationView> allocations, string sourceType)
    {
        foreach (var a in allocations)
        {
            var target = a.SettledAmount ?? a.CashAmount;
            var finalizeId = $"{a.Id}:finalize";
            var reverseId = $"{a.Id}:reverse";
            var reversed = a.ReversedAt.HasValue;
            yield return new LedgerRow(
                finalizeId,
                ApArLedgerEntryTypes.Allocation,
                -target,
                a.ValueDate,
                a.FinalizedAt!.Value,
                a.FinalizedBy,
                null,
                sourceType,
                a.Id,
                a.ReferenceNo,
                a.CashId,
                a.CashAmount,
                a.CashCurrencyCode,
                null,
                reversed ? reverseId : null,
                reversed ? ApArLedgerStatuses.Reversed : ApArLedgerStatuses.Posted,
                1);

            if (reversed)
            {
                yield return new LedgerRow(
                    reverseId,
                    ApArLedgerEntryTypes.AllocationReversal,
                    target,
                    DateOnly.FromDateTime(a.ReversedAt!.Value.UtcDateTime),
                    a.ReversedAt.Value,
                    a.ReversedBy,
                    a.ReverseReason,
                    sourceType,
                    a.Id,
                    a.ReferenceNo,
                    a.CashId,
                    a.CashAmount,
                    a.CashCurrencyCode,
                    finalizeId,
                    null,
                    ApArLedgerStatuses.Posted,
                    1);
            }
        }
    }

    private static async Task<ApArLedgerDto> ComposeAsync(
        ILcmsDbContext db,
        Guid accountId,
        string kind,
        Guid? billId,
        string? billNo,
        string currencyCode,
        decimal currentOutstanding,
        List<LedgerRow> rows,
        CancellationToken cancellationToken)
    {
        var actorIds = rows.Where(r => r.ActorId.HasValue).Select(r => r.ActorId!.Value).Distinct().ToList();
        var actorNames = actorIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Users.AsNoTracking()
                .Where(u => actorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        var ordered = rows
            .OrderBy(r => r.OccurredAt)
            .ThenBy(r => r.Order)
            .ThenBy(r => r.Id, StringComparer.Ordinal)
            .ToList();

        var balance = 0m;
        var entries = new List<ApArLedgerEntryDto>(ordered.Count);
        foreach (var r in ordered)
        {
            var before = balance;
            balance = decimal.Round(balance + r.Amount, 4, MidpointRounding.AwayFromZero);
            entries.Add(new ApArLedgerEntryDto(
                r.Id,
                r.EntryType,
                r.Amount,
                currencyCode,
                before,
                balance,
                r.BusinessDate,
                r.OccurredAt,
                r.ActorId,
                r.ActorId is Guid id && actorNames.TryGetValue(id, out var name) ? name : null,
                r.Reason,
                r.SourceType,
                r.SourceId,
                r.SourceLabel,
                r.CashId,
                r.CashAmount,
                r.CashCurrencyCode,
                r.ReversesEntryId,
                r.ReversedByEntryId,
                r.Status));
        }

        return new ApArLedgerDto(
            accountId,
            kind,
            billId,
            billNo,
            currencyCode,
            currentOutstanding,
            balance,
            Math.Abs(balance - currentOutstanding) <= Tolerance,
            entries);
    }
}

public sealed class GetAccountsReceivableLedgerQueryHandler
    : IRequestHandler<GetAccountsReceivableLedgerQuery, ApArLedgerDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _userContext;
    private readonly IPermissionService _permissions;
    private readonly IOrganizationHierarchyService _orgHierarchy;

    public GetAccountsReceivableLedgerQueryHandler(
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

    public async Task<ApArLedgerDto> Handle(GetAccountsReceivableLedgerQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var ar = await ApArLedgerAccess.LoadReceivableAsync(
            _db, _userContext, _permissions, _orgHierarchy, request.AccountsReceivableId, cancellationToken);
        var billNos = await DataScopeFilter.LoadBillNosAsync(_db, [ar.BillId], cancellationToken);
        var billNo = ar.BillId is Guid b && billNos.TryGetValue(b, out var no) ? no : null;
        return await ApArLedgerBuilder.BuildReceivableAsync(_db, ar, billNo, cancellationToken);
    }
}

public sealed class GetAccountsPayableLedgerQueryHandler
    : IRequestHandler<GetAccountsPayableLedgerQuery, ApArLedgerDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _userContext;
    private readonly IPermissionService _permissions;
    private readonly IOrganizationHierarchyService _orgHierarchy;

    public GetAccountsPayableLedgerQueryHandler(
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

    public async Task<ApArLedgerDto> Handle(GetAccountsPayableLedgerQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var ap = await ApArLedgerAccess.LoadPayableAsync(
            _db, _userContext, _permissions, _orgHierarchy, request.AccountsPayableId, cancellationToken);
        var billNos = await DataScopeFilter.LoadBillNosAsync(_db, [ap.BillId], cancellationToken);
        var billNo = ap.BillId is Guid b && billNos.TryGetValue(b, out var no) ? no : null;
        return await ApArLedgerBuilder.BuildPayableAsync(_db, ap, billNo, cancellationToken);
    }
}

public sealed class GetBillFinancialHistoryQueryHandler
    : IRequestHandler<GetBillFinancialHistoryQuery, IReadOnlyList<ApArLedgerDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _userContext;
    private readonly IPermissionService _permissions;
    private readonly IOrganizationHierarchyService _orgHierarchy;

    public GetBillFinancialHistoryQueryHandler(
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

    public async Task<IReadOnlyList<ApArLedgerDto>> Handle(
        GetBillFinancialHistoryQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var bill = await _db.Bills.AsNoTracking()
            .Where(b => b.Id == request.BillId)
            .Select(b => new { b.Id, b.BillNo, b.OrganizationId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy Bill.");

        var canRevenue = await _permissions.HasPermissionAsync(PermissionCodes.RevenueRead, cancellationToken);
        var canCost = await _permissions.HasPermissionAsync(PermissionCodes.CostRead, cancellationToken);
        if (!canRevenue && !canCost)
        {
            throw new ForbiddenAppException("Bạn không có quyền xem lịch sử công nợ của Bill.");
        }

        var result = new List<ApArLedgerDto>();
        if (canRevenue)
        {
            var (scope, subtree) = await DataScopeFilter.ResolveAsync(
                _permissions, _userContext, _db, _orgHierarchy,
                PermissionCodes.RevenueRead, "Bạn không có quyền xem khoản phải thu.", cancellationToken);
            var ars = await _db.AccountsReceivable.AsNoTracking()
                .Where(a => a.BillId == bill.Id)
                .ToListAsync(cancellationToken);
            foreach (var ar in ars.OrderBy(a => a.RecognizedAt).Where(a => DataScopeFilter.AllowsViaBillOrg(
                         scope, _userContext.UserId, subtree, a.CreatedBy, bill.OrganizationId)))
            {
                result.Add(await ApArLedgerBuilder.BuildReceivableAsync(_db, ar, bill.BillNo, cancellationToken));
            }
        }

        if (canCost)
        {
            var (scope, subtree) = await DataScopeFilter.ResolveAsync(
                _permissions, _userContext, _db, _orgHierarchy,
                PermissionCodes.CostRead, "Bạn không có quyền xem khoản phải trả.", cancellationToken);
            var aps = await _db.AccountsPayable.AsNoTracking()
                .Where(a => a.BillId == bill.Id)
                .ToListAsync(cancellationToken);
            foreach (var ap in aps.OrderBy(a => a.RecognizedAt).Where(a => DataScopeFilter.AllowsViaBillOrg(
                         scope, _userContext.UserId, subtree, a.CreatedBy, bill.OrganizationId)))
            {
                result.Add(await ApArLedgerBuilder.BuildPayableAsync(_db, ap, bill.BillNo, cancellationToken));
            }
        }

        return result;
    }
}

/// <summary>Same permission + data scope as AR/AP get-by-id; out-of-scope ⇒ 404 (no existence leak).</summary>
internal static class ApArLedgerAccess
{
    public static async Task<AccountsReceivable> LoadReceivableAsync(
        ILcmsDbContext db,
        ICurrentUserContext user,
        IPermissionService permissions,
        IOrganizationHierarchyService orgHierarchy,
        Guid id,
        CancellationToken cancellationToken)
    {
        var (scope, subtree) = await DataScopeFilter.ResolveAsync(
            permissions, user, db, orgHierarchy,
            PermissionCodes.RevenueRead, "Bạn không có quyền xem khoản phải thu.", cancellationToken);
        var ar = await db.AccountsReceivable.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy khoản phải thu.");
        var billOrgId = await DataScopeFilter.BillOrganizationIdAsync(db, ar.BillId, cancellationToken);
        if (!DataScopeFilter.AllowsViaBillOrg(scope, user.UserId, subtree, ar.CreatedBy, billOrgId))
        {
            throw new NotFoundAppException("Không tìm thấy khoản phải thu.");
        }

        return ar;
    }

    public static async Task<AccountsPayable> LoadPayableAsync(
        ILcmsDbContext db,
        ICurrentUserContext user,
        IPermissionService permissions,
        IOrganizationHierarchyService orgHierarchy,
        Guid id,
        CancellationToken cancellationToken)
    {
        var (scope, subtree) = await DataScopeFilter.ResolveAsync(
            permissions, user, db, orgHierarchy,
            PermissionCodes.CostRead, "Bạn không có quyền xem khoản phải trả.", cancellationToken);
        var ap = await db.AccountsPayable.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy khoản phải trả.");
        var billOrgId = await DataScopeFilter.BillOrganizationIdAsync(db, ap.BillId, cancellationToken);
        if (!DataScopeFilter.AllowsViaBillOrg(scope, user.UserId, subtree, ap.CreatedBy, billOrgId))
        {
            throw new NotFoundAppException("Không tìm thấy khoản phải trả.");
        }

        return ap;
    }
}
