using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.ReferenceMasters;

/// <summary>Writes an immutable party snapshot onto the current DbContext. The caller saves.</summary>
public interface IPartySnapshotCapture
{
    Task CapturePartyAsync(
        string objectType,
        Guid objectId,
        string roleCode,
        Guid partyId,
        CancellationToken cancellationToken);

    Task CaptureWalkInAsync(
        string objectType,
        Guid objectId,
        string roleCode,
        string displayName,
        string? phone,
        string? email,
        string? address,
        CancellationToken cancellationToken);
}

/// <summary>Copies party master facts. A later capture supersedes the previous row without rewriting it.</summary>
public sealed class PartySnapshotCapture : IPartySnapshotCapture
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IAuditWriter _audit;

    public PartySnapshotCapture(ILcmsDbContext db, ITenantContext tenant, IAuditWriter audit)
    {
        _db = db;
        _tenant = tenant;
        _audit = audit;
    }

    public Task CapturePartyAsync(
        string objectType,
        Guid objectId,
        string roleCode,
        Guid partyId,
        CancellationToken cancellationToken) =>
        CaptureAsync(objectType, objectId, roleCode, partyId, null, cancellationToken);

    public Task CaptureWalkInAsync(
        string objectType,
        Guid objectId,
        string roleCode,
        string displayName,
        string? phone,
        string? email,
        string? address,
        CancellationToken cancellationToken)
    {
        var walkIn = new OperationalPartySnapshot
        {
            IsWalkIn = true,
            DisplayName = displayName.Trim(),
            Phone = Trim(phone),
            Email = Trim(email),
            AddressLine1 = Trim(address)
        };
        return CaptureAsync(objectType, objectId, roleCode, null, walkIn, cancellationToken);
    }

    private async Task CaptureAsync(
        string objectType,
        Guid objectId,
        string roleCode,
        Guid? partyId,
        OperationalPartySnapshot? walkIn,
        CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var type = objectType.Trim().ToLowerInvariant();
        if (!PartySnapshotObjectTypes.All.Contains(type))
        {
            throw new ConflictAppException("Snapshot đối tác chỉ gắn trên Bill hoặc đơn hàng.");
        }

        var role = roleCode.Trim().ToLowerInvariant();
        if (!PartyRoleCodes.IsKnown(role))
        {
            throw new ConflictAppException("Vai trò đối tác không hợp lệ.");
        }

        await EnsureParentAsync(type, objectId, cancellationToken);

        OperationalPartySnapshot incoming;
        if (partyId is Guid id)
        {
            var party = await _db.BusinessParties.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
                ?? throw new NotFoundAppException("Không tìm thấy đối tác.");
            var contact = await _db.PartyContacts.AsNoTracking()
                .Where(c => c.PartyId == id && c.IsActive)
                .OrderByDescending(c => c.IsPrimary)
                .FirstOrDefaultAsync(cancellationToken);
            incoming = new OperationalPartySnapshot
            {
                PartyId = party.Id,
                IsWalkIn = false,
                DisplayName = party.Name,
                LegalName = party.LegalName,
                TaxId = party.TaxId,
                Phone = party.Phone,
                Email = party.Email,
                AddressLine1 = party.AddressLine1,
                City = party.City,
                CountryCode = party.CountryCode,
                ContactName = contact?.FullName,
                ContactPhone = contact?.Phone,
                ContactEmail = contact?.Email
            };
        }
        else
        {
            incoming = walkIn ?? throw new ConflictAppException("Thiếu tên đối tác vãng lai.");
            if (string.IsNullOrWhiteSpace(incoming.DisplayName))
            {
                throw new ConflictAppException("Đối tác vãng lai cần tên.");
            }
        }

        var current = await _db.OperationalPartySnapshots
            .Where(s => s.ObjectType == type && s.ObjectId == objectId && s.RoleCode == role && s.SupersededAt == null)
            .ToListAsync(cancellationToken);
        if (current.Count == 1 && SameFacts(current[0], incoming))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var row in current)
        {
            row.SupersededAt = now;
        }

        incoming.TenantId = _tenant.TenantId!.Value;
        incoming.ObjectType = type;
        incoming.ObjectId = objectId;
        incoming.RoleCode = role;
        incoming.CapturedAt = now;
        incoming.SourceChannel = "manual";
        _db.OperationalPartySnapshots.Add(incoming);
        _audit.Append(
            AuditActions.PartySnapshotCapture,
            AuditObjectTypes.PartySnapshot,
            incoming.Id,
            afterJson: incoming.DisplayName,
            reason: role);
    }

    private async Task EnsureParentAsync(string type, Guid objectId, CancellationToken cancellationToken)
    {
        var exists = type == PartySnapshotObjectTypes.Bill
            ? _db.Bills.Local.Any(b => b.Id == objectId)
              || await _db.Bills.AsNoTracking().AnyAsync(b => b.Id == objectId, cancellationToken)
            : _db.Orders.Local.Any(o => o.Id == objectId)
              || await _db.Orders.AsNoTracking().AnyAsync(o => o.Id == objectId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundAppException("Không tìm thấy chứng từ để gắn snapshot đối tác.");
        }
    }

    private static bool SameFacts(OperationalPartySnapshot left, OperationalPartySnapshot right) =>
        left.PartyId == right.PartyId
        && left.IsWalkIn == right.IsWalkIn
        && left.DisplayName == right.DisplayName
        && left.LegalName == right.LegalName
        && left.TaxId == right.TaxId
        && left.Phone == right.Phone
        && left.AddressLine1 == right.AddressLine1
        && left.ContactName == right.ContactName;

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
