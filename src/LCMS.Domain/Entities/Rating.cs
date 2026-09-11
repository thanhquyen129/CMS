using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: ratings (D04) — Một lần tính giá (context + version snapshot).</summary>
public sealed class Rating : TenantEntityBase
{
    public Guid BillId { get; set; }
    public Guid RateVersionId { get; set; }
    public DateTimeOffset RatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CurrencyCode { get; set; } = "VND";
    public decimal TotalAmount { get; set; }
    public decimal Quantity { get; set; } = 1m;

    /// <summary>Optional weight input used as qty fallback when quantity omitted.</summary>
    public decimal? Weight { get; set; }

    public string? ServiceTypeCode { get; set; }
    public string? PartyTypeCode { get; set; }
    public string? RouteCode { get; set; }

    /// <summary>Optional explicit base for percent_of_base; else running total.</summary>
    public decimal? BaseAmount { get; set; }

    /// <summary>completed | superseded — prior rows stay immutable (C-011 history).</summary>
    public string Status { get; set; } = RatingStatuses.Completed;

    /// <summary>Prior rating this row supersedes (re-rate chain).</summary>
    public Guid? SupersedesRatingId { get; set; }

    public Bill? Bill { get; set; }
    public RateVersion? RateVersion { get; set; }
    public Rating? SupersedesRating { get; set; }
}

public static class RatingStatuses
{
    public const string Completed = "completed";
    public const string Superseded = "superseded";
}
