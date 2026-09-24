using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Settlements.Queries;

public sealed record PaymentAllocationDto(
    Guid Id,
    Guid PaymentId,
    Guid AccountsPayableId,
    decimal Amount,
    string CurrencyCode,
    decimal? BaseAmount,
    Guid? FxRateId,
    string AllocationStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FinalizedAt,
    DateTimeOffset? ReversedAt,
    string? ReverseReason,
    string? Notes,
    decimal? OriginalAmount,
    decimal? SettledAmount,
    decimal? FxRate,
    string? FxSource,
    DateOnly? FxRateDate,
    byte[]? RowVersion = null);

public sealed record PaymentDto(
    Guid Id,
    decimal Amount,
    decimal? BaseAmount,
    Guid? FxRateId,
    decimal AppliedAmount,
    decimal AllocatedAmount,
    decimal UnappliedAmount,
    decimal AvailableToAllocate,
    string CurrencyCode,
    DateOnly ValueDate,
    Guid? CounterpartyId,
    Guid? BillId,
    string? BillNo,
    string? ReferenceNo,
    string? Notes,
    string Status,
    string RecordStatus,
    IReadOnlyList<PaymentAllocationDto> Allocations,
    byte[]? RowVersion = null);

public sealed record CollectionAllocationDto(
    Guid Id,
    Guid CollectionId,
    Guid AccountsReceivableId,
    decimal Amount,
    string CurrencyCode,
    decimal? BaseAmount,
    Guid? FxRateId,
    string AllocationStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FinalizedAt,
    DateTimeOffset? ReversedAt,
    string? ReverseReason,
    string? Notes,
    decimal? OriginalAmount,
    decimal? SettledAmount,
    decimal? FxRate,
    string? FxSource,
    DateOnly? FxRateDate,
    byte[]? RowVersion = null);

public sealed record CollectionDto(
    Guid Id,
    decimal Amount,
    decimal? BaseAmount,
    Guid? FxRateId,
    decimal AppliedAmount,
    decimal AllocatedAmount,
    decimal UnappliedAmount,
    decimal AvailableToAllocate,
    string CurrencyCode,
    DateOnly ValueDate,
    Guid? CounterpartyId,
    Guid? BillId,
    string? BillNo,
    string? ReferenceNo,
    string? Notes,
    string Status,
    string RecordStatus,
    IReadOnlyList<CollectionAllocationDto> Allocations,
    byte[]? RowVersion = null);

public sealed record ListPaymentsQuery : IRequest<IReadOnlyList<PaymentDto>>;
public sealed record GetPaymentByIdQuery(Guid Id) : IRequest<PaymentDto>;
public sealed record ListCollectionsQuery : IRequest<IReadOnlyList<CollectionDto>>;
public sealed record GetCollectionByIdQuery(Guid Id) : IRequest<CollectionDto>;

public sealed class ListPaymentsQueryHandler : IRequestHandler<ListPaymentsQuery, IReadOnlyList<PaymentDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListPaymentsQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<PaymentDto>> Handle(ListPaymentsQuery request, CancellationToken cancellationToken)
    {
        EnsureTenant(_tenantContext);
        var payments = await _db.Payments.AsNoTracking()
            .OrderByDescending(p => p.Id)
            .ToListAsync(cancellationToken);
        var ids = payments.Select(p => p.Id).ToList();
        var allocations = await _db.PaymentAllocations.AsNoTracking()
            .Where(a => ids.Contains(a.PaymentId))
            .ToListAsync(cancellationToken);
        var billNos = await LoadBillNosAsync(
            _db,
            payments.Where(p => p.BillId.HasValue).Select(p => p.BillId!.Value),
            cancellationToken);
        return payments
            .Select(p => MapPayment(
                p,
                allocations.Where(a => a.PaymentId == p.Id).ToList(),
                ResolveBillNo(p.BillId, billNos)))
            .ToList();
    }

    internal static void EnsureTenant(ITenantContext tenantContext)
    {
        if (!tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }
    }

    internal static async Task<IReadOnlyDictionary<Guid, string>> LoadBillNosAsync(
        ILcmsDbContext db,
        IEnumerable<Guid> billIds,
        CancellationToken cancellationToken)
    {
        var ids = billIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await db.Bills.AsNoTracking()
            .Where(b => ids.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.BillNo, cancellationToken);
    }

    internal static string? ResolveBillNo(Guid? billId, IReadOnlyDictionary<Guid, string> billNos) =>
        billId.HasValue && billNos.TryGetValue(billId.Value, out var billNo) ? billNo : null;

    internal static PaymentDto MapPayment(
        Payment p,
        IReadOnlyList<PaymentAllocation> allocations,
        string? billNo = null)
    {
        var applied = allocations.Where(a => SettlementHelpers.IsFinalized(a.AllocationStatus)).Sum(a => a.Amount);
        var allocated = allocations.Where(a => SettlementHelpers.IsActiveAllocation(a.AllocationStatus)).Sum(a => a.Amount);
        return new PaymentDto(
            p.Id,
            p.Amount,
            p.BaseAmount,
            p.FxRateId,
            applied,
            allocated,
            p.Amount - applied,
            p.Amount - allocated,
            p.CurrencyCode,
            p.ValueDate,
            p.CounterpartyId,
            p.BillId,
            billNo,
            p.ReferenceNo,
            p.Notes,
            p.Status,
            p.RecordStatus,
            allocations.Select(a => new PaymentAllocationDto(
                a.Id, a.PaymentId, a.AccountsPayableId, a.Amount,
                a.CurrencyCode, a.BaseAmount, a.FxRateId, a.AllocationStatus,
                a.CreatedAt, a.FinalizedAt, a.ReversedAt, a.ReverseReason, a.Notes,
                a.OriginalAmount, a.SettledAmount, a.FxRate, a.FxSource, a.FxRateDate,
                a.RowVersion)).ToList(),
            p.RowVersion);
    }
}

public sealed class GetPaymentByIdQueryHandler : IRequestHandler<GetPaymentByIdQuery, PaymentDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetPaymentByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<PaymentDto> Handle(GetPaymentByIdQuery request, CancellationToken cancellationToken)
    {
        ListPaymentsQueryHandler.EnsureTenant(_tenantContext);
        var payment = await _db.Payments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy thanh toán.");
        var allocations = await _db.PaymentAllocations.AsNoTracking()
            .Where(a => a.PaymentId == payment.Id)
            .ToListAsync(cancellationToken);
        var billNos = await ListPaymentsQueryHandler.LoadBillNosAsync(
            _db,
            payment.BillId.HasValue ? [payment.BillId.Value] : [],
            cancellationToken);
        return ListPaymentsQueryHandler.MapPayment(
            payment,
            allocations,
            ListPaymentsQueryHandler.ResolveBillNo(payment.BillId, billNos));
    }
}

public sealed class ListCollectionsQueryHandler : IRequestHandler<ListCollectionsQuery, IReadOnlyList<CollectionDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListCollectionsQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<CollectionDto>> Handle(ListCollectionsQuery request, CancellationToken cancellationToken)
    {
        ListPaymentsQueryHandler.EnsureTenant(_tenantContext);
        var collections = await _db.Collections.AsNoTracking()
            .OrderByDescending(c => c.Id)
            .ToListAsync(cancellationToken);
        var ids = collections.Select(c => c.Id).ToList();
        var allocations = await _db.CollectionAllocations.AsNoTracking()
            .Where(a => ids.Contains(a.CollectionId))
            .ToListAsync(cancellationToken);
        var billNos = await ListPaymentsQueryHandler.LoadBillNosAsync(
            _db,
            collections.Where(c => c.BillId.HasValue).Select(c => c.BillId!.Value),
            cancellationToken);
        return collections
            .Select(c => MapCollection(
                c,
                allocations.Where(a => a.CollectionId == c.Id).ToList(),
                ListPaymentsQueryHandler.ResolveBillNo(c.BillId, billNos)))
            .ToList();
    }

    internal static CollectionDto MapCollection(
        Collection c,
        IReadOnlyList<CollectionAllocation> allocations,
        string? billNo = null)
    {
        var applied = allocations.Where(a => SettlementHelpers.IsFinalized(a.AllocationStatus)).Sum(a => a.Amount);
        var allocated = allocations.Where(a => SettlementHelpers.IsActiveAllocation(a.AllocationStatus)).Sum(a => a.Amount);
        return new CollectionDto(
            c.Id,
            c.Amount,
            c.BaseAmount,
            c.FxRateId,
            applied,
            allocated,
            c.Amount - applied,
            c.Amount - allocated,
            c.CurrencyCode,
            c.ValueDate,
            c.CounterpartyId,
            c.BillId,
            billNo,
            c.ReferenceNo,
            c.Notes,
            c.Status,
            c.RecordStatus,
            allocations.Select(a => new CollectionAllocationDto(
                a.Id, a.CollectionId, a.AccountsReceivableId, a.Amount,
                a.CurrencyCode, a.BaseAmount, a.FxRateId, a.AllocationStatus,
                a.CreatedAt, a.FinalizedAt, a.ReversedAt, a.ReverseReason, a.Notes,
                a.OriginalAmount, a.SettledAmount, a.FxRate, a.FxSource, a.FxRateDate,
                a.RowVersion)).ToList(),
            c.RowVersion);
    }
}

public sealed class GetCollectionByIdQueryHandler : IRequestHandler<GetCollectionByIdQuery, CollectionDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetCollectionByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<CollectionDto> Handle(GetCollectionByIdQuery request, CancellationToken cancellationToken)
    {
        ListPaymentsQueryHandler.EnsureTenant(_tenantContext);
        var collection = await _db.Collections.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy thu tiền.");
        var allocations = await _db.CollectionAllocations.AsNoTracking()
            .Where(a => a.CollectionId == collection.Id)
            .ToListAsync(cancellationToken);
        var billNos = await ListPaymentsQueryHandler.LoadBillNosAsync(
            _db,
            collection.BillId.HasValue ? [collection.BillId.Value] : [],
            cancellationToken);
        return ListCollectionsQueryHandler.MapCollection(
            collection,
            allocations,
            ListPaymentsQueryHandler.ResolveBillNo(collection.BillId, billNos));
    }
}
