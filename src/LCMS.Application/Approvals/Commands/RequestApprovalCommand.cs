using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Approvals.Commands;

public sealed record RequestApprovalCommand(
    string ObjectType,
    Guid ObjectId,
    string? RequestReason,
    string? Notes) : IRequest<Guid>;

public sealed class RequestApprovalCommandValidator : AbstractValidator<RequestApprovalCommand>
{
    private static readonly HashSet<string> ObjectTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ApprovalObjectTypes.Cost,
        ApprovalObjectTypes.Revenue,
        ApprovalObjectTypes.Document,
        ApprovalObjectTypes.Payment,
        ApprovalObjectTypes.Collection,
        ApprovalObjectTypes.Settlement,
        ApprovalObjectTypes.Variance,
        ApprovalObjectTypes.Exception,
        ApprovalObjectTypes.Other
    };

    public RequestApprovalCommandValidator()
    {
        RuleFor(x => x.ObjectType)
            .NotEmpty().WithMessage("Loại đối tượng phê duyệt không được để trống.")
            .Must(t => ObjectTypes.Contains(t.Trim()))
            .WithMessage("Loại đối tượng phê duyệt không hợp lệ.");
        RuleFor(x => x.ObjectId).NotEmpty().WithMessage("Đối tượng phê duyệt không hợp lệ.");
        RuleFor(x => x.RequestReason).MaximumLength(2048).When(x => x.RequestReason is not null);
        RuleFor(x => x.Notes).MaximumLength(2048).When(x => x.Notes is not null);
    }
}

/// <summary>
/// Requests approval on a financial object ref.
/// Permission ≠ Approval: does NOT call IPermissionService / mutate RolePermission.
/// </summary>
public sealed class RequestApprovalCommandHandler : IRequestHandler<RequestApprovalCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;

    public RequestApprovalCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
    }

    public async Task<Guid> Handle(RequestApprovalCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var objectType = request.ObjectType.Trim().ToLowerInvariant();
        await EnsureObjectExistsAsync(objectType, request.ObjectId, cancellationToken);

        var permissionCountBefore = await _db.Permissions.CountAsync(cancellationToken);
        var rolePermissionCountBefore = await _db.RolePermissions.CountAsync(cancellationToken);

        var pendingExists = await _db.Approvals.AsNoTracking()
            .AnyAsync(
                a => a.ObjectType == objectType
                     && a.ObjectId == request.ObjectId
                     && a.Status == ApprovalStatuses.Pending,
                cancellationToken);
        if (pendingExists)
        {
            throw new ConflictAppException("Đối tượng đã có yêu cầu phê duyệt đang chờ.");
        }

        var approval = new Approval
        {
            TenantId = tenantId,
            ObjectType = objectType,
            ObjectId = request.ObjectId,
            Status = ApprovalStatuses.Pending,
            RequestedBy = _user.UserId,
            RequestedAt = DateTimeOffset.UtcNow,
            RequestReason = string.IsNullOrWhiteSpace(request.RequestReason) ? null : request.RequestReason.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        _db.Approvals.Add(approval);

        if (objectType == ApprovalObjectTypes.Cost)
        {
            var cost = await _db.Costs.FirstOrDefaultAsync(c => c.Id == request.ObjectId, cancellationToken);
            if (cost is not null)
            {
                cost.ApprovalStatus = "pending";
            }
        }
        else if (objectType == ApprovalObjectTypes.Revenue)
        {
            var revenue = await _db.Revenues.FirstOrDefaultAsync(r => r.Id == request.ObjectId, cancellationToken);
            if (revenue is not null)
            {
                revenue.ApprovalStatus = "pending";
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

        return approval.Id;
    }

    private async Task EnsureObjectExistsAsync(string objectType, Guid objectId, CancellationToken cancellationToken)
    {
        var exists = objectType switch
        {
            ApprovalObjectTypes.Cost => await _db.Costs.AsNoTracking().AnyAsync(x => x.Id == objectId, cancellationToken),
            ApprovalObjectTypes.Revenue => await _db.Revenues.AsNoTracking().AnyAsync(x => x.Id == objectId, cancellationToken),
            ApprovalObjectTypes.Document => await _db.FinancialDocuments.AsNoTracking().AnyAsync(x => x.Id == objectId, cancellationToken),
            ApprovalObjectTypes.Payment => await _db.Payments.AsNoTracking().AnyAsync(x => x.Id == objectId, cancellationToken),
            ApprovalObjectTypes.Collection => await _db.Collections.AsNoTracking().AnyAsync(x => x.Id == objectId, cancellationToken),
            ApprovalObjectTypes.Variance => await _db.Variances.AsNoTracking().AnyAsync(x => x.Id == objectId, cancellationToken),
            ApprovalObjectTypes.Exception => await _db.Exceptions.AsNoTracking().AnyAsync(x => x.Id == objectId, cancellationToken),
            ApprovalObjectTypes.Settlement or ApprovalObjectTypes.Other => true,
            _ => false
        };

        if (!exists)
        {
            throw new NotFoundAppException("Không tìm thấy đối tượng để phê duyệt.");
        }
    }
}
