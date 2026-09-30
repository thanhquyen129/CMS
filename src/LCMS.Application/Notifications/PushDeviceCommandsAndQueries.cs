using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Notifications;

public sealed record PushDeviceDto(
    Guid Id,
    string DeviceToken,
    string Platform,
    string? DeviceName,
    string? AppVersion,
    bool IsActive,
    DateTimeOffset LastSeenAt);

public sealed record RegisterPushDeviceCommand(
    string DeviceToken,
    string Platform,
    string? DeviceName = null,
    string? AppVersion = null) : IRequest<PushDeviceDto>;

public sealed record UnregisterPushDeviceCommand(string DeviceToken) : IRequest;

public sealed record ListUserPushDevicesQuery : IRequest<IReadOnlyList<PushDeviceDto>>;

public sealed class RegisterPushDeviceCommandHandler
    : IRequestHandler<RegisterPushDeviceCommand, PushDeviceDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _userContext;
    private readonly IAuditWriter _audit;

    public RegisterPushDeviceCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext userContext,
        IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _userContext = userContext;
        _audit = audit;
    }

    public async Task<PushDeviceDto> Handle(
        RegisterPushDeviceCommand request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var token = request.DeviceToken?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["deviceToken"] = ["DeviceToken là bắt buộc."]
            });
        }

        var platform = (request.Platform ?? PushDevicePlatforms.Android).Trim().ToLowerInvariant();
        if (!PushDevicePlatforms.IsValid(platform))
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["platform"] = ["Nền tảng thiết bị không hợp lệ (chỉ chấp nhận ios, android, web)."]
            });
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var userId = _userContext.HasUser ? _userContext.UserId!.Value : Guid.Empty;
        var utc = DateTimeOffset.UtcNow;

        var existing = await _db.UserPushDevices
            .FirstOrDefaultAsync(
                d => d.TenantId == tenantId && d.UserId == userId && d.DeviceToken == token,
                cancellationToken);

        if (existing is null)
        {
            existing = new UserPushDevice
            {
                TenantId = tenantId,
                UserId = userId,
                DeviceToken = token,
                Platform = platform,
                DeviceName = request.DeviceName?.Trim(),
                AppVersion = request.AppVersion?.Trim(),
                IsActive = true,
                LastSeenAt = utc
            };
            _db.UserPushDevices.Add(existing);
        }
        else
        {
            existing.Platform = platform;
            existing.DeviceName = request.DeviceName?.Trim() ?? existing.DeviceName;
            existing.AppVersion = request.AppVersion?.Trim() ?? existing.AppVersion;
            existing.IsActive = true;
            existing.LastSeenAt = utc;
        }

        _audit.Append(
            AuditActions.PushDeviceRegister,
            AuditObjectTypes.PushDevice,
            existing.Id,
            afterJson: AuditJson.Serialize(new
            {
                existing.Platform,
                existing.DeviceName,
                existing.AppVersion,
                existing.IsActive
            }));

        await _db.SaveChangesAsync(cancellationToken);

        return new PushDeviceDto(
            existing.Id,
            existing.DeviceToken,
            existing.Platform,
            existing.DeviceName,
            existing.AppVersion,
            existing.IsActive,
            existing.LastSeenAt);
    }
}

public sealed class UnregisterPushDeviceCommandHandler : IRequestHandler<UnregisterPushDeviceCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _userContext;
    private readonly IAuditWriter _audit;

    public UnregisterPushDeviceCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext userContext,
        IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _userContext = userContext;
        _audit = audit;
    }

    public async Task Handle(
        UnregisterPushDeviceCommand request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var token = request.DeviceToken?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var userId = _userContext.HasUser ? _userContext.UserId!.Value : Guid.Empty;

        var devices = await _db.UserPushDevices
            .Where(d => d.TenantId == tenantId && d.UserId == userId && d.DeviceToken == token && d.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var device in devices)
        {
            device.IsActive = false;
            _audit.Append(
                AuditActions.PushDeviceUnregister,
                AuditObjectTypes.PushDevice,
                device.Id,
                beforeJson: AuditJson.Serialize(new { IsActive = true }),
                afterJson: AuditJson.Serialize(new { IsActive = false }),
                reason: "Người dùng đăng xuất hoặc tắt thông báo đẩy trên thiết bị.");
        }

        if (devices.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}

public sealed class ListUserPushDevicesQueryHandler
    : IRequestHandler<ListUserPushDevicesQuery, IReadOnlyList<PushDeviceDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _userContext;

    public ListUserPushDevicesQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext userContext)
    {
        _db = db;
        _tenantContext = tenantContext;
        _userContext = userContext;
    }

    public async Task<IReadOnlyList<PushDeviceDto>> Handle(
        ListUserPushDevicesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var userId = _userContext.HasUser ? _userContext.UserId!.Value : Guid.Empty;
        var rows = await _db.UserPushDevices.AsNoTracking()
            .Where(d => d.UserId == userId && d.IsActive)
            .ToListAsync(cancellationToken);

        return rows
            .OrderByDescending(d => d.LastSeenAt)
            .Select(d => new PushDeviceDto(
                d.Id,
                d.DeviceToken,
                d.Platform,
                d.DeviceName,
                d.AppVersion,
                d.IsActive,
                d.LastSeenAt))
            .ToList();
    }
}
