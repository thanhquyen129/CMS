using LCMS.Application.Abstractions;
using LCMS.Application.BusinessParties;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.ReferenceMasters;

public sealed record CapturePartySnapshotCommand(
    string ObjectType,
    Guid ObjectId,
    string RoleCode,
    Guid? PartyId,
    string? WalkInName,
    string? Phone,
    string? Email,
    string? Address) : IRequest<Guid>;

/// <summary>Captures a party snapshot for a Bill or Order role. Walk-in requires policy permission.</summary>
public sealed class CapturePartySnapshotCommandHandler : IRequestHandler<CapturePartySnapshotCommand, Guid>
{
    private readonly IPartySnapshotCapture _capture;
    private readonly IBillPartyPolicyStore _policy;
    private readonly ILcmsDbContext _db;
    private readonly IPartyDirectoryService _parties;

    public CapturePartySnapshotCommandHandler(
        IPartySnapshotCapture capture,
        IBillPartyPolicyStore policy,
        ILcmsDbContext db,
        IPartyDirectoryService parties)
    {
        _capture = capture;
        _policy = policy;
        _db = db;
        _parties = parties;
    }

    public async Task<Guid> Handle(CapturePartySnapshotCommand request, CancellationToken cancellationToken)
    {
        var role = request.RoleCode.Trim().ToLowerInvariant();
        if (request.PartyId is Guid partyId)
        {
            await _parties.EnsureUsableAsync(partyId, [role], "gắn vai trò lên chứng từ", cancellationToken);
            await _capture.CapturePartyAsync(request.ObjectType, request.ObjectId, role, partyId, cancellationToken);
        }
        else
        {
            var policy = await _policy.GetAsync(cancellationToken);
            if (!policy.AllowWalkIn)
            {
                throw new ConflictAppException("Thuê bao chưa cho phép đối tác vãng lai. Hãy chọn đối tác trong danh mục.");
            }

            await _capture.CaptureWalkInAsync(
                request.ObjectType,
                request.ObjectId,
                role,
                request.WalkInName ?? "",
                request.Phone,
                request.Email,
                request.Address,
                cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        var current = await _db.OperationalPartySnapshots.AsNoTracking()
            .Where(s => s.ObjectType == request.ObjectType.Trim().ToLowerInvariant()
                && s.ObjectId == request.ObjectId
                && s.RoleCode == role
                && s.SupersededAt == null)
            .Select(s => s.Id)
            .FirstAsync(cancellationToken);
        return current;
    }
}

public sealed record PartySnapshotDto(
    Guid Id,
    string ObjectType,
    Guid ObjectId,
    string RoleCode,
    Guid? PartyId,
    bool IsWalkIn,
    string DisplayName,
    string? LegalName,
    string? TaxId,
    string? Phone,
    string? Email,
    string? AddressLine1,
    string? City,
    string? CountryCode,
    string? ContactName,
    DateTimeOffset CapturedAt,
    DateTimeOffset? SupersededAt);

public sealed record ListPartySnapshotsQuery(string ObjectType, Guid ObjectId, bool CurrentOnly)
    : IRequest<IReadOnlyList<PartySnapshotDto>>;

/// <summary>Lists party snapshots for one Bill or Order.</summary>
public sealed class ListPartySnapshotsQueryHandler : IRequestHandler<ListPartySnapshotsQuery, IReadOnlyList<PartySnapshotDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public ListPartySnapshotsQueryHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<PartySnapshotDto>> Handle(ListPartySnapshotsQuery request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var type = request.ObjectType.Trim().ToLowerInvariant();
        var query = _db.OperationalPartySnapshots.AsNoTracking()
            .Where(s => s.ObjectType == type && s.ObjectId == request.ObjectId);
        if (request.CurrentOnly)
        {
            query = query.Where(s => s.SupersededAt == null);
        }

        var rows = (await query.ToListAsync(cancellationToken))
            .OrderByDescending(s => s.CapturedAt)
            .ToList();
        return rows.Select(s => new PartySnapshotDto(
            s.Id,
            s.ObjectType,
            s.ObjectId,
            s.RoleCode,
            s.PartyId,
            s.IsWalkIn,
            s.DisplayName,
            s.LegalName,
            s.TaxId,
            s.Phone,
            s.Email,
            s.AddressLine1,
            s.City,
            s.CountryCode,
            s.ContactName,
            s.CapturedAt,
            s.SupersededAt)).ToList();
    }
}

public sealed record GetBillPartyPolicyQuery : IRequest<BillPartyPolicy>;

public sealed class GetBillPartyPolicyQueryHandler : IRequestHandler<GetBillPartyPolicyQuery, BillPartyPolicy>
{
    private readonly IBillPartyPolicyStore _store;

    public GetBillPartyPolicyQueryHandler(IBillPartyPolicyStore store)
    {
        _store = store;
    }

    public Task<BillPartyPolicy> Handle(GetBillPartyPolicyQuery request, CancellationToken cancellationToken) =>
        _store.GetAsync(cancellationToken);
}

public sealed record SaveBillPartyPolicyCommand(IReadOnlyList<string> RequiredRoles, bool AllowWalkIn) : IRequest;

/// <summary>Saves which party roles a Bill must have. Does not hard-code three parties.</summary>
public sealed class SaveBillPartyPolicyCommandHandler : IRequestHandler<SaveBillPartyPolicyCommand>
{
    private readonly IBillPartyPolicyStore _store;
    private readonly IPermissionService _permissions;
    private readonly IAuditWriter _audit;
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public SaveBillPartyPolicyCommandHandler(
        IBillPartyPolicyStore store,
        IPermissionService permissions,
        IAuditWriter audit,
        ILcmsDbContext db,
        ITenantContext tenant)
    {
        _store = store;
        _permissions = permissions;
        _audit = audit;
        _db = db;
        _tenant = tenant;
    }

    public async Task Handle(SaveBillPartyPolicyCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(PermissionCodes.SettingsManage, "Bạn không có quyền cấu hình thuê bao.", cancellationToken);
        var roles = request.RequiredRoles
            .Select(r => r.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();
        var unsupported = roles.Where(r => !BillPartyRoles.Assignable.Contains(r)).ToList();
        if (unsupported.Count > 0)
        {
            throw new ConflictAppException("Chỉ bắt buộc được các vai trò gắn trên Bill: khách hàng, bên trả tiền, người gửi, người nhận, bên nhận hóa đơn.");
        }

        await _store.SaveAsync(new BillPartyPolicy { RequiredRoles = roles, AllowWalkIn = request.AllowWalkIn }, cancellationToken);
        _audit.Append(AuditActions.BillPartyPolicyUpdate, AuditObjectTypes.Tenant, _tenant.TenantId!.Value, afterJson: string.Join(",", roles));
        await _db.SaveChangesAsync(cancellationToken);
    }
}
