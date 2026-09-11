using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
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
    }
}

/// <summary>
/// Approves or rejects a pending approval.
/// Permission ≠ Approval: never calls IPermissionService; role without action still may decide.
/// </summary>
public sealed class DecideApprovalCommandHandler : IRequestHandler<DecideApprovalCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;

    public DecideApprovalCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
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

        var permissionCountBefore = await _db.Permissions.CountAsync(cancellationToken);
        var rolePermissionCountBefore = await _db.RolePermissions.CountAsync(cancellationToken);

        approval.Status = request.Approve ? ApprovalStatuses.Approved : ApprovalStatuses.Rejected;
        approval.DecidedAt = DateTimeOffset.UtcNow;
        approval.DecidedBy = _user.UserId;
        approval.DecisionReason = string.IsNullOrWhiteSpace(request.DecisionReason)
            ? null
            : request.DecisionReason.Trim();

        var statusOnObject = request.Approve ? "approved" : "rejected";
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

        await _db.SaveChangesAsync(cancellationToken);

        var permissionCountAfter = await _db.Permissions.CountAsync(cancellationToken);
        var rolePermissionCountAfter = await _db.RolePermissions.CountAsync(cancellationToken);
        if (permissionCountAfter != permissionCountBefore || rolePermissionCountAfter != rolePermissionCountBefore)
        {
            throw new ConflictAppException(
                "Phê duyệt không được tạo hoặc sửa Quyền (Permission ≠ Approval).");
        }
    }
}
