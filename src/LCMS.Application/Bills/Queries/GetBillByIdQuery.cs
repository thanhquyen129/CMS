using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Bills.Queries;

public sealed record BillDto(
    Guid Id,
    Guid TenantId,
    string BillNo,
    string BillType,
    string? SourceSystem,
    string? ExternalId,
    string OperationalStatus,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record GetBillByIdQuery(Guid Id) : IRequest<BillDto>;

public sealed class GetBillByIdQueryHandler : IRequestHandler<GetBillByIdQuery, BillDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetBillByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<BillDto> Handle(GetBillByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        // Global query filter enforces tenant_id; missing row ⇒ not found (no cross-tenant leak).
        var bill = await _db.Bills
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);

        if (bill is null)
        {
            throw new NotFoundAppException("Không tìm thấy Bill.");
        }

        return new BillDto(
            bill.Id,
            bill.TenantId,
            bill.BillNo,
            bill.BillType,
            bill.SourceSystem,
            bill.ExternalId,
            bill.OperationalStatus,
            bill.IsActive,
            bill.CreatedAt);
    }
}
