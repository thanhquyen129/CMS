using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: rate_versions (D04) — Phiên bản bảng giá.
/// Published version is immutable (C-011 / TD1-DB-006).
/// </summary>
public sealed class RateVersion : TenantEntityBase
{
    public Guid RateCardId { get; set; }
    public int VersionNo { get; set; }

    /// <summary>draft | published</summary>
    public string Status { get; set; } = RateVersionStatuses.Draft;

    public DateTimeOffset? EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public string? Note { get; set; }

    public RateCard? RateCard { get; set; }

    public bool IsPublished =>
        string.Equals(Status, RateVersionStatuses.Published, StringComparison.OrdinalIgnoreCase);
}

public static class RateVersionStatuses
{
    public const string Draft = "draft";
    public const string Published = "published";
}
