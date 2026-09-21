using System.Text.Json;
using LCMS.Application.Abstractions;
using LCMS.Application.Notifications;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LCMS.Infrastructure.Notifications;

public sealed class UnconfiguredNotificationMailTransport : INotificationMailTransport
{
    public bool IsConfigured => false;
}

public sealed class NotificationPublisher : IOperatorNotificationPublisher
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly INotificationMailTransport _mail;
    private readonly ILogger<NotificationPublisher> _logger;

    public NotificationPublisher(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        INotificationMailTransport mail,
        ILogger<NotificationPublisher> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _mail = mail;
        _logger = logger;
    }

    public async Task PublishAsync(NotificationPublishRequest request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            return;
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var settings = await _db.TenantNotificationSettings.AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        var prefs = NotificationPrefs.Parse(settings?.EventsJson);
        prefs.TryGetValue(request.EventType, out var pref);
        pref ??= new NotificationPrefs.EventPref(true, false);

        var inAppOn = (settings?.InAppEnabled ?? true) && pref.InApp;
        var emailOn = (settings?.EmailEnabled ?? false) && pref.Email;

        if (!inAppOn && !emailOn)
        {
            return;
        }

        var recipients = await _db.Users.AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => new { u.Id, u.Email, u.DisplayName })
            .Take(200)
            .ToListAsync(cancellationToken);

        if (inAppOn)
        {
            foreach (var r in recipients)
            {
                if (_user.UserId == r.Id)
                {
                    continue;
                }

                _db.InAppNotifications.Add(new InAppNotification
                {
                    TenantId = tenantId,
                    UserId = r.Id,
                    EventType = request.EventType,
                    Title = request.Title,
                    Body = request.Body,
                    Href = request.Href,
                    ObjectType = request.ObjectType,
                    ObjectId = request.ObjectId
                });
            }
        }

        if (emailOn)
        {
            var payload = JsonSerializer.Serialize(new
            {
                request.EventType,
                request.Title,
                request.Body,
                request.Href,
                smtpConfigured = _mail.IsConfigured,
                recipients = recipients.Select(r => r.Email).ToArray()
            });
            _db.OutboxMessages.Add(new OutboxMessage
            {
                TenantId = tenantId,
                Topic = "notification.email",
                PayloadJson = payload,
                Status = OutboxMessageStatuses.Pending,
                EnqueuedAt = DateTimeOffset.UtcNow
            });
            if (!_mail.IsConfigured)
            {
                _logger.LogInformation(
                    "Notification email queued but SMTP is not configured for tenant {TenantId}.",
                    tenantId);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
