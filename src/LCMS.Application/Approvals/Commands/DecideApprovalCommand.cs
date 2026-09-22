using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Settlements;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Approvals.Commands;

public sealed record DecideApprovalCommand(Guid ApprovalId, bool Approve, string? DecisionReason) : IRequest;

public sealed class DecideApprovalCommandValidator : AbstractValidator<DecideApprovalCommand>
{
    public DecideApprovalCommandValidator()
    {
        RuleFor(x => x.ApprovalId).NotEmpty().WithMessage("Yêu cầu phê duyệt không hợp lệ.");
        RuleFor(x => x.DecisionReason).MaximumLength(2048).When(x => x.DecisionReason is not null);
        RuleFor(x => x.DecisionReason)
            .NotEmpty().WithMessage("Lý do từ chối phê duyệt không được để trống.")
            .When(x => !x.Approve);
    }
}

/// <summary>
/// Approves (multi-step stub) or rejects a pending approval.
/// Permission ≠ Approval: never calls IPermissionService; role without action still may decide.
/// Write-off over threshold: on final approve, applies pending AP/AR write-off payload (P03).
/// </summary>
public sealed class DecideApprovalCommandHandler : IRequestHandler<DecideApprovalCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly IAuditWriter _audit;

    public DecideApprovalCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _audit = audit;
    }

    public async Task Handle(DecideApprovalCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var approval = await _db.Approvals
            .FirstOrDefaultAsync(a => a.Id == request.ApprovalId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy yêu cầu phê duyệt.");

        if (!string.Equals(approval.Status, ApprovalStatuses.Pending, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chỉ quyết định yêu cầu phê duyệt đang chờ.");
        }

        if (request.Approve && _user.HasUser && approval.RequestedBy == _user.UserId)
        {
            throw new ConflictAppException("PC-21: Người tạo không được tự duyệt.");
        }

        if (request.Approve && approval.ObjectFingerprint is not null)
        {
            var now = await new RequestApprovalCommandHandler(_db, _tenantContext, _user, null!, Microsoft.Extensions.Options.Options.Create(new ApprovalMatrixOptions()))
                .FingerprintAsync(approval.ObjectType, approval.ObjectId, cancellationToken);
            if (now is not null && now != approval.ObjectFingerprint)
            {
                approval.Status = ApprovalStatuses.NeedsRereview;
                await _db.SaveChangesAsync(cancellationToken);
                throw new ConflictAppException("Đối tượng đã đổi sau khi yêu cầu. Phê duyệt lại.");
            }
        }

        var permissionCountBefore = await _db.Permissions.CountAsync(cancellationToken);
        var rolePermissionCountBefore = await _db.RolePermissions.CountAsync(cancellationToken);

        var reason = string.IsNullOrWhiteSpace(request.DecisionReason)
            ? null
            : request.DecisionReason.Trim();

        if (!request.Approve)
        {
            approval.Status = ApprovalStatuses.Rejected;
            approval.DecidedAt = DateTimeOffset.UtcNow;
            approval.DecidedBy = _user.UserId;
            approval.DecisionReason = reason;
            await ApplyObjectStatusAsync(approval, "rejected", cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await EnsurePermissionUntouchedAsync(permissionCountBefore, rolePermissionCountBefore, cancellationToken);
            return;
        }

        // Multi-step stub: each approve advances CurrentLevel; final when CurrentLevel == RequiredLevel.
        var nextLevel = approval.CurrentLevel + 1;
        if (nextLevel < approval.RequiredLevel)
        {
            approval.CurrentLevel = nextLevel;
            approval.DecidedAt = DateTimeOffset.UtcNow;
            approval.DecidedBy = _user.UserId;
            approval.DecisionReason = reason;
            await _db.SaveChangesAsync(cancellationToken);
            await EnsurePermissionUntouchedAsync(permissionCountBefore, rolePermissionCountBefore, cancellationToken);
            return;
        }

        approval.CurrentLevel = approval.RequiredLevel;
        approval.Status = ApprovalStatuses.Approved;
        approval.DecidedAt = DateTimeOffset.UtcNow;
        approval.DecidedBy = _user.UserId;
        approval.DecisionReason = reason;
        await ApplyObjectStatusAsync(approval, "approved", cancellationToken);
        await ApplyPendingWriteOffIfAnyAsync(approval, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await EnsurePermissionUntouchedAsync(permissionCountBefore, rolePermissionCountBefore, cancellationToken);
    }

    private async Task ApplyPendingWriteOffIfAnyAsync(Approval approval, CancellationToken cancellationToken)
    {
        if (!WriteOffApplier.TryDecodePendingNotes(approval.Notes, out var payload))
        {
            return;
        }

        if (approval.ObjectType == ApprovalObjectTypes.AccountsPayable)
        {
            var ap = await _db.AccountsPayable
                .FirstOrDefaultAsync(a => a.Id == approval.ObjectId, cancellationToken)
                ?? throw new NotFoundAppException("Không tìm thấy khoản phải trả để áp xóa nợ đã duyệt.");
            // Mutate only — outer SaveChanges persists approval + write-off together.
            await WriteOffApplier.ApplyPayableAsync(
                _db, _audit, _user, ap, payload.Amount, payload.Reason, cancellationToken,
                saveChanges: false);
            return;
        }

        if (approval.ObjectType == ApprovalObjectTypes.AccountsReceivable)
        {
            var ar = await _db.AccountsReceivable
                .FirstOrDefaultAsync(a => a.Id == approval.ObjectId, cancellationToken)
                ?? throw new NotFoundAppException("Không tìm thấy khoản phải thu để áp xóa nợ đã duyệt.");
            await WriteOffApplier.ApplyReceivableAsync(
                _db, _audit, _user, ar, payload.Amount, payload.Reason, cancellationToken,
                saveChanges: false);
        }
    }

    private async Task ApplyObjectStatusAsync(Approval approval, string statusOnObject, CancellationToken cancellationToken)
    {
        if (approval.ObjectType == ApprovalObjectTypes.Cost)
        {
            var cost = await _db.Costs.FirstOrDefaultAsync(c => c.Id == approval.ObjectId, cancellationToken);
            if (cost is not null)
            {
                cost.ApprovalStatus = statusOnObject;
            }
        }
        else if (approval.ObjectType == ApprovalObjectTypes.Revenue)
        {
            var revenue = await _db.Revenues.FirstOrDefaultAsync(r => r.Id == approval.ObjectId, cancellationToken);
            if (revenue is not null)
            {
                revenue.ApprovalStatus = statusOnObject;
            }
        }
        else if (approval.ObjectType == ApprovalObjectTypes.Exception
                 && statusOnObject == "approved")
        {
            var exception = await _db.Exceptions.FirstOrDefaultAsync(e => e.Id == approval.ObjectId, cancellationToken);
            if (exception is not null
                && string.Equals(exception.Status, ExceptionStatuses.Waiting, StringComparison.OrdinalIgnoreCase))
            {
                exception.Status = ExceptionStatuses.Waived;
                exception.ResolvedAt = DateTimeOffset.UtcNow;
                exception.ResolvedBy = _user.UserId;
            }
        }
    }

    private async Task EnsurePermissionUntouchedAsync(
        int permissionCountBefore,
        int rolePermissionCountBefore,
        CancellationToken cancellationToken)
    {
        var permissionCountAfter = await _db.Permissions.CountAsync(cancellationToken);
        var rolePermissionCountAfter = await _db.RolePermissions.CountAsync(cancellationToken);
        if (permissionCountAfter != permissionCountBefore || rolePermissionCountAfter != rolePermissionCountBefore)
        {
            throw new ConflictAppException(
                "Phê duyệt không được tạo hoặc sửa Quyền (Permission ≠ Approval).");
        }
    }
}
