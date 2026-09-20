using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.BusinessParties.Commands;

public sealed record BlockBusinessPartyCommand(Guid Id, string Reason) : IRequest;

public sealed class BlockBusinessPartyCommandHandler : IRequestHandler<BlockBusinessPartyCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly IAuditWriter _audit;

    public BlockBusinessPartyCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions,
        IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
        _audit = audit;
    }

    public async Task Handle(BlockBusinessPartyCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.MasterPartyManage,
            "Bạn không có quyền quản lý đối tác.",
            cancellationToken);

        var reason = request.Reason.Trim();
        if (reason.Length is < 3 or > 512)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["reason"] = ["Lý do chặn phải từ 3 đến 512 ký tự."]
            });
        }

        var party = await _db.BusinessParties.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy đối tác.");

        var before = AuditJson.Serialize(new { party.IsBlocked, party.BlockedReason, party.IsActive });
        party.IsBlocked = true;
        party.BlockedReason = reason;
        party.BlockedAt = DateTimeOffset.UtcNow;

        _audit.Append(
            AuditActions.BusinessPartyBlock,
            AuditObjectTypes.BusinessParty,
            party.Id,
            beforeJson: before,
            afterJson: AuditJson.Serialize(new { party.IsBlocked, party.BlockedReason, party.BlockedAt }),
            reason: reason);

        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record UnblockBusinessPartyCommand(Guid Id, string? Reason = null) : IRequest;

public sealed class UnblockBusinessPartyCommandHandler : IRequestHandler<UnblockBusinessPartyCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly IAuditWriter _audit;

    public UnblockBusinessPartyCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions,
        IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
        _audit = audit;
    }

    public async Task Handle(UnblockBusinessPartyCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.MasterPartyManage,
            "Bạn không có quyền quản lý đối tác.",
            cancellationToken);

        var party = await _db.BusinessParties.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy đối tác.");

        var before = AuditJson.Serialize(new { party.IsBlocked, party.BlockedReason, party.BlockedAt });
        party.IsBlocked = false;
        party.BlockedReason = null;
        party.BlockedAt = null;

        _audit.Append(
            AuditActions.BusinessPartyUnblock,
            AuditObjectTypes.BusinessParty,
            party.Id,
            beforeJson: before,
            afterJson: AuditJson.Serialize(new { party.IsBlocked }),
            reason: string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim());

        await _db.SaveChangesAsync(cancellationToken);
    }
}
