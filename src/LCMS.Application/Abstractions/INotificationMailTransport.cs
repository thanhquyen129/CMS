namespace LCMS.Application.Abstractions;

/// <summary>SMTP/mail transport for notification.email outbox. Unconfigured is a valid production state.</summary>
public interface INotificationMailTransport
{
    bool IsConfigured { get; }
}
