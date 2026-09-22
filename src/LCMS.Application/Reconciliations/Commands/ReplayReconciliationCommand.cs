using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Reconciliations.Commands;

public sealed record ReplayReconciliationCommand(Guid Id) : IRequest<Guid>;

/// <summary>Same context, new session. A completed session is not rewritten.</summary>
public sealed class ReplayReconciliationCommandHandler : IRequestHandler<ReplayReconciliationCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public ReplayReconciliationCommandHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(ReplayReconciliationCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var source = await _db.Reconciliations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phiên đối soát.");
        if (string.Equals(source.Status, ReconciliationStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Không chạy lại phiên đối soát đã hủy.");
        }

        var max = await _db.Reconciliations
            .Where(r => r.BillId == source.BillId && r.ReconciliationType == source.ReconciliationType)
            .Select(r => (int?)r.VersionNo)
            .MaxAsync(cancellationToken) ?? source.VersionNo;

        var next = new Reconciliation
        {
            TenantId = _tenant.TenantId!.Value,
            ReconciliationType = source.ReconciliationType,
            RuleCode = source.RuleCode,
            VersionNo = max + 1,
            Status = ReconciliationStatuses.Draft,
            BillId = source.BillId,
            Notes = source.Notes
        };
        _db.Reconciliations.Add(next);
        await _db.SaveChangesAsync(cancellationToken);
        return next.Id;
    }
}
