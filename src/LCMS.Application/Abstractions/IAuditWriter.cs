namespace LCMS.Application.Abstractions;

/// <summary>
/// Appends an audit_events row to the current DbContext (same SaveChanges as the mutation).
/// </summary>
public interface IAuditWriter
{
    void Append(
        string action,
        string objectType,
        Guid objectId,
        string? beforeJson = null,
        string? afterJson = null,
        string? reason = null);
}
