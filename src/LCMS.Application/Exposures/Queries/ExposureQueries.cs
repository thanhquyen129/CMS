using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Exposures.Queries;

public sealed record PayableExposureDto(
    Guid Id,
    decimal Amount,
    decimal RecognizedAmount,
    decimal OpenAmount,
    string CurrencyCode,
    string Status,
    DateOnly EffectiveDate,
    DateOnly? DueDate,
    Guid? BillId,
    Guid? CounterpartyId,
    Guid? CostId,
    Guid? FinancialDocumentId,
    string? Notes,
    string RecordStatus);

public sealed record ReceivableExposureDto(
    Guid Id,
    decimal Amount,
    decimal RecognizedAmount,
    decimal OpenAmount,
    string CurrencyCode,
    string Status,
    DateOnly EffectiveDate,
    DateOnly? DueDate,
    Guid? BillId,
    Guid? CounterpartyId,
    Guid? RevenueId,
    Guid? FinancialDocumentId,
    string? Notes,
    string RecordStatus);

public sealed record ListPayableExposuresQuery(string? Status) : IRequest<IReadOnlyList<PayableExposureDto>>;
public sealed record GetPayableExposureByIdQuery(Guid Id) : IRequest<PayableExposureDto>;
public sealed record ListReceivableExposuresQuery(string? Status) : IRequest<IReadOnlyList<ReceivableExposureDto>>;
public sealed record GetReceivableExposureByIdQuery(Guid Id) : IRequest<ReceivableExposureDto>;

public sealed class ListPayableExposuresQueryHandler
    : IRequestHandler<ListPayableExposuresQuery, IReadOnlyList<PayableExposureDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListPayableExposuresQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<PayableExposureDto>> Handle(
        ListPayableExposuresQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var query = _db.PayableExposures.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToLowerInvariant();
            query = query.Where(e => e.Status == status);
        }

        return await query
            .OrderByDescending(e => e.Id)
            .Select(e => new PayableExposureDto(
                e.Id,
                e.Amount,
                e.RecognizedAmount,
                e.Amount - e.RecognizedAmount,
                e.CurrencyCode,
                e.Status,
                e.EffectiveDate,
                e.DueDate,
                e.BillId,
                e.CounterpartyId,
                e.CostId,
                e.FinancialDocumentId,
                e.Notes,
                e.RecordStatus))
            .ToListAsync(cancellationToken);
    }
}

public sealed class GetPayableExposureByIdQueryHandler
    : IRequestHandler<GetPayableExposureByIdQuery, PayableExposureDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetPayableExposureByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<PayableExposureDto> Handle(
        GetPayableExposureByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var e = await _db.PayableExposures.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy nghĩa vụ phải trả (exposure).");

        return new PayableExposureDto(
            e.Id,
            e.Amount,
            e.RecognizedAmount,
            e.Amount - e.RecognizedAmount,
            e.CurrencyCode,
            e.Status,
            e.EffectiveDate,
            e.DueDate,
            e.BillId,
            e.CounterpartyId,
            e.CostId,
            e.FinancialDocumentId,
            e.Notes,
            e.RecordStatus);
    }
}

public sealed class ListReceivableExposuresQueryHandler
    : IRequestHandler<ListReceivableExposuresQuery, IReadOnlyList<ReceivableExposureDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListReceivableExposuresQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<ReceivableExposureDto>> Handle(
        ListReceivableExposuresQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var query = _db.ReceivableExposures.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToLowerInvariant();
            query = query.Where(e => e.Status == status);
        }

        return await query
            .OrderByDescending(e => e.Id)
            .Select(e => new ReceivableExposureDto(
                e.Id,
                e.Amount,
                e.RecognizedAmount,
                e.Amount - e.RecognizedAmount,
                e.CurrencyCode,
                e.Status,
                e.EffectiveDate,
                e.DueDate,
                e.BillId,
                e.CounterpartyId,
                e.RevenueId,
                e.FinancialDocumentId,
                e.Notes,
                e.RecordStatus))
            .ToListAsync(cancellationToken);
    }
}

public sealed class GetReceivableExposureByIdQueryHandler
    : IRequestHandler<GetReceivableExposureByIdQuery, ReceivableExposureDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetReceivableExposureByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<ReceivableExposureDto> Handle(
        GetReceivableExposureByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var e = await _db.ReceivableExposures.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy quyền thu dự kiến (exposure).");

        return new ReceivableExposureDto(
            e.Id,
            e.Amount,
            e.RecognizedAmount,
            e.Amount - e.RecognizedAmount,
            e.CurrencyCode,
            e.Status,
            e.EffectiveDate,
            e.DueDate,
            e.BillId,
            e.CounterpartyId,
            e.RevenueId,
            e.FinancialDocumentId,
            e.Notes,
            e.RecordStatus);
    }
}
