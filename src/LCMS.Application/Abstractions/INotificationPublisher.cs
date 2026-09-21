namespace LCMS.Application.Abstractions;

public sealed record NotificationPublishRequest(
    string EventType,
    string Title,
    string Body,
    string? Href = null,
    string? ObjectType = null,
    Guid? ObjectId = null);

public interface IOperatorNotificationPublisher
{
    Task PublishAsync(NotificationPublishRequest request, CancellationToken cancellationToken);
}
