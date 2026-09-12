using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Reconciliations.Commands;

public sealed record CompleteReconciliationCommand(Guid Id) : IRequest;

public sealed class CompleteReconciliationCommandValidator : AbstractValidator<CompleteReconciliationCommand>
{
    public CompleteReconciliationCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Phiên đối soát không hợp lệ.");
    }
}

/// <summary>Marks an in-progress (or draft) reconciliation session completed.</summary>
public sealed class CompleteReconciliationCommandHandler : IRequestHandler<CompleteReconciliationCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;

    public CompleteReconciliationCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
    }

    public async Task Handle(CompleteReconciliationCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var session = await _db.Reconciliations
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phiên đối soát.");

        if (string.Equals(session.Status, ReconciliationStatuses.Completed, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.Equals(session.Status, ReconciliationStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Không thể hoàn tất phiên đối soát đã hủy.");
        }

        session.Status = ReconciliationStatuses.Completed;
        session.CompletedAt = DateTimeOffset.UtcNow;
        session.CompletedBy = _user.UserId;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
