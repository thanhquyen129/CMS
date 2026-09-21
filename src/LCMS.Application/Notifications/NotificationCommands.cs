using System.Text.Json;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Notifications;

public sealed record NotificationEventPrefDto(string Code, string Name, bool InApp, bool Email);

public sealed record NotificationSettingsDto(
    bool InAppEnabled,
    bool EmailEnabled,
    bool SmtpConfigured,
    IReadOnlyList<NotificationEventPrefDto> Events);

public sealed record InAppNotificationDto(
    Guid Id,
    string EventType,
    string Title,
    string Body,
    string? Href,
    bool IsRead,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

public sealed record GetNotificationSettingsQuery : IRequest<NotificationSettingsDto>;
public sealed record UpdateNotificationSettingsCommand(
    bool InAppEnabled,
    bool EmailEnabled,
    IReadOnlyList<NotificationEventPrefDto> Events) : IRequest<NotificationSettingsDto>;
public sealed record ListInAppNotificationsQuery(bool UnreadOnly, int Take)
    : IRequest<IReadOnlyList<InAppNotificationDto>>;
public sealed record MarkNotificationReadCommand(Guid Id) : IRequest;
public sealed record MarkAllNotificationsReadCommand : IRequest;

public static class NotificationPrefs
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public sealed record EventPref(bool InApp, bool Email);

    public static Dictionary<string, EventPref> Parse(string? json)
    {
        var map = new Dictionary<string, EventPref>(StringComparer.OrdinalIgnoreCase);
        foreach (var (code, _) in NotificationEventTypes.Catalog)
        {
            map[code] = new EventPref(true, false);
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return map;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, EventPref>>(json, Json);
            if (parsed is null)
            {
                return map;
            }

            foreach (var (k, v) in parsed)
            {
                map[k] = v;
            }
        }
        catch (JsonException)
        {
            /* keep defaults */
        }

        return map;
    }

    public static string Serialize(IReadOnlyList<NotificationEventPrefDto> events)
    {
        var map = events.ToDictionary(
            e => e.Code,
            e => new EventPref(e.InApp, e.Email),
            StringComparer.OrdinalIgnoreCase);
        return JsonSerializer.Serialize(map, Json);
    }

    public static NotificationSettingsDto ToDto(TenantNotificationSetting? row, bool smtpConfigured)
    {
        var prefs = Parse(row?.EventsJson);
        var events = NotificationEventTypes.Catalog.Select(c =>
        {
            prefs.TryGetValue(c.Code, out var p);
            p ??= new EventPref(true, false);
            return new NotificationEventPrefDto(c.Code, c.NameVi, p.InApp, p.Email);
        }).ToList();
        return new NotificationSettingsDto(
            row?.InAppEnabled ?? true,
            row?.EmailEnabled ?? false,
            smtpConfigured,
            events);
    }
}

public sealed class GetNotificationSettingsQueryHandler
    : IRequestHandler<GetNotificationSettingsQuery, NotificationSettingsDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly INotificationMailTransport _mail;

    public GetNotificationSettingsQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        INotificationMailTransport mail)
    {
        _db = db;
        _tenantContext = tenantContext;
        _mail = mail;
    }

    public async Task<NotificationSettingsDto> Handle(
        GetNotificationSettingsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var row = await _db.TenantNotificationSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return NotificationPrefs.ToDto(row, _mail.IsConfigured);
    }
}

public sealed class UpdateNotificationSettingsCommandHandler
    : IRequestHandler<UpdateNotificationSettingsCommand, NotificationSettingsDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly IAuditWriter _audit;
    private readonly INotificationMailTransport _mail;

    public UpdateNotificationSettingsCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions,
        IAuditWriter audit,
        INotificationMailTransport mail)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
        _audit = audit;
        _mail = mail;
    }

    public async Task<NotificationSettingsDto> Handle(
        UpdateNotificationSettingsCommand request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.NotificationManage,
            "Bạn không có quyền cài đặt thông báo.",
            cancellationToken);

        var tenantId = _tenantContext.TenantId!.Value;
        var row = await _db.TenantNotificationSettings.FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            row = new TenantNotificationSetting { TenantId = tenantId };
            _db.TenantNotificationSettings.Add(row);
        }

        row.InAppEnabled = request.InAppEnabled;
        row.EmailEnabled = request.EmailEnabled;
        row.EventsJson = NotificationPrefs.Serialize(request.Events);
        _audit.Append(
            AuditActions.NotificationSettingsUpdate,
            AuditObjectTypes.Notification,
            row.Id,
            afterJson: row.EventsJson);
        await _db.SaveChangesAsync(cancellationToken);
        return NotificationPrefs.ToDto(row, _mail.IsConfigured);
    }
}

public sealed class ListInAppNotificationsQueryHandler
    : IRequestHandler<ListInAppNotificationsQuery, IReadOnlyList<InAppNotificationDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;

    public ListInAppNotificationsQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
    }

    public async Task<IReadOnlyList<InAppNotificationDto>> Handle(
        ListInAppNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        if (!_user.HasUser)
        {
            return [];
        }

        var take = Math.Clamp(request.Take, 1, 200);
        var q = _db.InAppNotifications.AsNoTracking().Where(n => n.UserId == _user.UserId);
        if (request.UnreadOnly)
        {
            q = q.Where(n => !n.IsRead);
        }

        return await q.OrderByDescending(n => n.CreatedAt).Take(take)
            .Select(n => new InAppNotificationDto(
                n.Id, n.EventType, n.Title, n.Body, n.Href, n.IsRead, n.CreatedAt, n.ReadAt))
            .ToListAsync(cancellationToken);
    }
}

public sealed class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;

    public MarkNotificationReadCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
    }

    public async Task Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant || !_user.HasUser)
        {
            throw new TenantRequiredAppException();
        }

        var row = await _db.InAppNotifications.FirstOrDefaultAsync(
            n => n.Id == request.Id && n.UserId == _user.UserId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy thông báo.");
        if (!row.IsRead)
        {
            row.IsRead = true;
            row.ReadAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}

public sealed class MarkAllNotificationsReadCommandHandler : IRequestHandler<MarkAllNotificationsReadCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;

    public MarkAllNotificationsReadCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
    }

    public async Task Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant || !_user.HasUser)
        {
            throw new TenantRequiredAppException();
        }

        var unread = await _db.InAppNotifications
            .Where(n => n.UserId == _user.UserId && !n.IsRead)
            .ToListAsync(cancellationToken);
        var utc = DateTimeOffset.UtcNow;
        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = utc;
        }

        if (unread.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
