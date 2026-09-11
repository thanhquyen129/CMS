namespace LCMS.Domain.Common;

/// <summary>
/// C-013 / TD1-DB-005: no hard delete — soft-delete or cancel/reversal/adjustment.
/// </summary>
public interface ISoftDeletable
{
    DateTimeOffset? DeletedAt { get; set; }
    Guid? DeletedBy { get; set; }
    bool IsDeleted { get; }
}
