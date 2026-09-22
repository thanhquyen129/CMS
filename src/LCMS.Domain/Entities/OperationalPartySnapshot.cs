using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: operational_party_snapshots — role-based party facts frozen on a transaction.
/// Master edits do not rewrite a captured row. A later capture supersedes it.
/// </summary>
public sealed class OperationalPartySnapshot : TenantEntityBase
{
    /// <summary>bill | order</summary>
    public string ObjectType { get; set; } = string.Empty;

    public Guid ObjectId { get; set; }
    public string RoleCode { get; set; } = string.Empty;

    /// <summary>Null when the row is a walk-in capture with no master party.</summary>
    public Guid? PartyId { get; set; }

    public bool IsWalkIn { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string? TaxId { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? AddressLine1 { get; set; }
    public string? City { get; set; }
    public string? CountryCode { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }

    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;
    public string SourceChannel { get; set; } = "manual";

    /// <summary>Set when a newer snapshot for the same object and role is captured.</summary>
    public DateTimeOffset? SupersededAt { get; set; }
}

public static class PartySnapshotObjectTypes
{
    public const string Bill = "bill";
    public const string Order = "order";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Bill, Order
    };
}
