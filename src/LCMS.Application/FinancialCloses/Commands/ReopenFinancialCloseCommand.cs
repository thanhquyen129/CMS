using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.FinancialCloses.Commands;

public sealed record ReopenFinancialCloseCommand(Guid FinancialCloseId, string? Reason, string? IfMatch = null) : IRequest;

public sealed class ReopenFinancialCloseCommandValidator : AbstractValidator<ReopenFinancialCloseCommand>
{
    public ReopenFinancialCloseCommandValidator()
    {
        RuleFor(x => x.FinancialCloseId).NotEmpty().WithMessage("Lần chốt tài chính không hợp lệ.");
        RuleFor(x => x.Reason).MaximumLength(2048).When(x => x.Reason is not null);
    }
}

/// <summary>
/// Reopens a locked close without mutating any snapshot (C-010 / AC-008).
/// Subsequent snapshot creates a new SnapshotVersion.
/// </summary>
public sealed class ReopenFinancialCloseCommandHandler : IRequestHandler<ReopenFinancialCloseCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly IRowVersionGuard _versions;

    public ReopenFinancialCloseCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        IRowVersionGuard versions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _versions = versions;
    }

    public async Task Handle(ReopenFinancialCloseCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var close = await _db.FinancialCloses
            .FirstOrDefaultAsync(c => c.Id == request.FinancialCloseId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy lần chốt tài chính.");
        _versions.EnsureCurrent(close, request.IfMatch);

        if (close.Status != FinancialCloseStatuses.Locked)
        {
            throw new ConflictAppException("Chỉ được mở lại lần chốt đang khóa.");
        }

        // Snapshot history is untouched — verify count preserved for audit clarity in logs via unchanged rows.
        close.Status = FinancialCloseStatuses.Reopened;
        close.ReopenedAt = DateTimeOffset.UtcNow;
        close.ReopenedBy = _user.UserId;
        close.ReopenReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        await _db.SaveChangesAsync(cancellationToken);
    }
}
