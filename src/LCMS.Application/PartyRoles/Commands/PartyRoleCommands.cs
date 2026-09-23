using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.PartyRoles.Commands;

public sealed record AssignPartyRoleCommand(Guid PartyId, string RoleCode) : IRequest<Guid>;

public sealed class AssignPartyRoleCommandValidator : AbstractValidator<AssignPartyRoleCommand>
{
    public AssignPartyRoleCommandValidator()
    {
        RuleFor(x => x.PartyId).NotEmpty().WithMessage("Đối tác không hợp lệ.");
        RuleFor(x => x.RoleCode)
            .NotEmpty().WithMessage("Mã vai trò đối tác không được để trống.")
            .MaximumLength(64).WithMessage("Mã vai trò đối tác không được vượt quá 64 ký tự.")
            .Must(c => PartyRoleCodes.IsKnown(c))
            .WithMessage("Mã vai trò đối tác phải là customer, vendor, payer hoặc payee.");
    }
}

public sealed class AssignPartyRoleCommandHandler : IRequestHandler<AssignPartyRoleCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public AssignPartyRoleCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<Guid> Handle(AssignPartyRoleCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.MasterPartyManage,
            "Bạn không có quyền quản lý đối tác.",
            cancellationToken);

        var tenantId = _tenantContext.TenantId!.Value;
        var roleCode = request.RoleCode.Trim().ToLowerInvariant();

        var party = await _db.BusinessParties.FirstOrDefaultAsync(p => p.Id == request.PartyId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy đối tác.");

        var existing = await _db.PartyRoles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                r => r.TenantId == tenantId && r.PartyId == party.Id && r.RoleCode == roleCode,
                cancellationToken);
        if (existing is not null)
        {
            if (existing.IsActive && existing.DeletedAt is null)
            {
                return existing.Id;
            }

            existing.IsActive = true;
            existing.DeletedAt = null;
            existing.DeletedBy = null;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.TouchRowVersion();
            await _db.SaveChangesAsync(cancellationToken);
            return existing.Id;
        }

        var row = new PartyRole
        {
            TenantId = tenantId,
            PartyId = party.Id,
            RoleCode = roleCode,
            IsActive = true
        };
        _db.PartyRoles.Add(row);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictAppException("Vai trò đối tác đã tồn tại.");
        }

        return row.Id;
    }
}

public sealed record RevokePartyRoleCommand(Guid PartyId, string RoleCode) : IRequest;

public sealed class RevokePartyRoleCommandHandler : IRequestHandler<RevokePartyRoleCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly ICurrentUserContext _userContext;

    public RevokePartyRoleCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions,
        ICurrentUserContext userContext)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
        _userContext = userContext;
    }

    public async Task Handle(RevokePartyRoleCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.MasterPartyManage,
            "Bạn không có quyền quản lý đối tác.",
            cancellationToken);

        var roleCode = request.RoleCode.Trim().ToLowerInvariant();
        var row = await _db.PartyRoles.FirstOrDefaultAsync(
            r => r.PartyId == request.PartyId && r.RoleCode == roleCode,
            cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy vai trò đối tác.");

        row.SoftDelete(_userContext.UserId);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
