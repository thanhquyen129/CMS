namespace LCMS.Domain.Common;

/// <summary>
/// Shared audit + optimistic concurrency (C-012 row_version) + soft-delete (C-013).
/// Column names map to snake_case in Infrastructure Fluent API / naming conventions.
/// </summary>
public abstract class EntityBase : ISoftDeletable
{
    public Guid Id { get; set; } = UuidV7.NewId();

    /// <summary>Optimistic concurrency token (PostgreSQL bytea).</summary>
    public byte[] RowVersion { get; set; } = Guid.NewGuid().ToByteArray();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public bool IsDeleted => DeletedAt.HasValue;

    public void SoftDelete(Guid? actorId, DateTimeOffset? at = null)
    {
        if (IsDeleted)
        {
            return;
        }

        DeletedAt = at ?? DateTimeOffset.UtcNow;
        DeletedBy = actorId;
        UpdatedAt = DeletedAt;
        UpdatedBy = actorId;
        TouchRowVersion();
    }

    public void TouchRowVersion() => RowVersion = Guid.NewGuid().ToByteArray();
}
