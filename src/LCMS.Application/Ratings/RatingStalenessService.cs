using System.Text.Json;
using LCMS.Application.Abstractions;
using LCMS.Application.OperationalReferences.Edit;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Ratings;

/// <summary>
/// Marks current ratings "Cần tính giá lại" when an input they used changed (ADR-0039 D05).
/// Never rewrites amounts, details, or seeded Expected Cost/Revenue.
/// </summary>
public interface IRatingStalenessService
{
    /// <summary>Bill field edit: stale only ratings whose rule/basis used a changed field.</summary>
    Task<int> MarkForBillChangeAsync(
        Guid billId,
        IReadOnlyCollection<string> changedFields,
        string reason,
        CancellationToken cancellationToken);

    /// <summary>Order / Shipment / Chặng / Chuyến edit: stale current ratings of linked Bills only.</summary>
    Task<int> MarkLinkedBillsAsync(
        string objectType,
        Guid objectId,
        IReadOnlyCollection<string> changedFields,
        string reason,
        CancellationToken cancellationToken);
}

public sealed class RatingStalenessService : IRatingStalenessService
{
    private static readonly HashSet<string> SystemBases = new(StringComparer.OrdinalIgnoreCase) { "air_volumetric", "sea_wm" };
    private static readonly HashSet<string> BillCwBases = new(StringComparer.OrdinalIgnoreCase)
    {
        "measured", "confirmed", "air_volumetric", "sea_wm"
    };

    private readonly ILcmsDbContext _db;
    private readonly IAuditWriter _audit;

    public RatingStalenessService(ILcmsDbContext db, IAuditWriter audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<int> MarkForBillChangeAsync(
        Guid billId,
        IReadOnlyCollection<string> changedFields,
        string reason,
        CancellationToken cancellationToken)
    {
        var relevant = changedFields
            .Where(f => OperationalFieldCatalog.Find(OperationalObjectTypes.Bill, f)?.RatingRelevant == true)
            .ToList();
        if (relevant.Count == 0)
        {
            return 0;
        }

        var ratings = await CurrentRatingsAsync([billId], cancellationToken);
        if (ratings.Count == 0)
        {
            return 0;
        }

        var versionIds = ratings.Select(r => r.RateVersionId).Distinct().ToList();
        var rules = await _db.PricingRules.AsNoTracking()
            .Where(r => versionIds.Contains(r.RateVersionId) && r.IsActive)
            .ToListAsync(cancellationToken);

        var marked = 0;
        foreach (var rating in ratings)
        {
            var versionRules = rules.Where(r => r.RateVersionId == rating.RateVersionId).ToList();
            var (basis, usedRuleCodes) = ReadSnapshot(rating);
            var used = relevant.Where(f => UsesField(f, basis, usedRuleCodes, versionRules)).ToList();
            if (used.Count == 0)
            {
                continue;
            }

            Mark(rating, reason, used);
            marked++;
        }

        return marked;
    }

    public async Task<int> MarkLinkedBillsAsync(
        string objectType,
        Guid objectId,
        IReadOnlyCollection<string> changedFields,
        string reason,
        CancellationToken cancellationToken)
    {
        var relevant = changedFields
            .Where(f => OperationalFieldCatalog.Find(objectType, f)?.RatingRelevant == true)
            .ToList();
        if (relevant.Count == 0)
        {
            return 0;
        }

        var billIds = await OperationalReferenceAccess.LinkedBillIdsAsync(_db, objectType, objectId, cancellationToken);
        if (billIds.Count == 0)
        {
            return 0;
        }

        var ratings = await CurrentRatingsAsync(billIds, cancellationToken);
        foreach (var rating in ratings)
        {
            Mark(rating, reason, relevant);
        }

        return ratings.Count;
    }

    private async Task<List<Rating>> CurrentRatingsAsync(IReadOnlyCollection<Guid> billIds, CancellationToken cancellationToken)
    {
        var ids = billIds.ToList();
        return await _db.Ratings
            .Where(r => ids.Contains(r.BillId) && r.Status == RatingStatuses.Completed && r.StaleAt == null)
            .ToListAsync(cancellationToken);
    }

    private void Mark(Rating rating, string reason, IReadOnlyCollection<string> fields)
    {
        var text = reason.Length > 1000 ? reason[..1000] : reason;
        rating.StaleAt = DateTimeOffset.UtcNow;
        rating.StaleReason = text;
        _audit.Append(
            AuditActions.RatingMarkStale,
            "rating",
            rating.Id,
            afterJson: JsonSerializer.Serialize(new { billId = rating.BillId, fields, reason = text }),
            reason: text);
    }

    private static bool UsesField(string field, string? basis, IReadOnlyCollection<string> ruleCodes, IReadOnlyList<PricingRule> versionRules)
    {
        var selected = versionRules.Where(r => ruleCodes.Contains(r.Code, StringComparer.OrdinalIgnoreCase)).ToList();
        switch (field)
        {
            case OperationalFieldCodes.GrossWeightKg:
                return (basis is not null && SystemBases.Contains(basis))
                    || selected.Any(r => string.Equals(r.Applicability, RatingEngine.PerGrossKg, StringComparison.OrdinalIgnoreCase));
            case OperationalFieldCodes.VolumeCbm:
                return basis is not null && SystemBases.Contains(basis);
            case OperationalFieldCodes.ChargeableWeightKg:
                return basis is not null && BillCwBases.Contains(basis);
            case OperationalFieldCodes.TransportMode:
                return (basis is not null && SystemBases.Contains(basis))
                    || versionRules.Any(r => !string.IsNullOrWhiteSpace(r.TransportMode));
            case OperationalFieldCodes.ServiceTypeCode:
                return versionRules.Any(r => !string.IsNullOrWhiteSpace(r.ServiceTypeCode));
            case OperationalFieldCodes.OriginCode:
                return versionRules.Any(r => !string.IsNullOrWhiteSpace(r.OriginCode));
            case OperationalFieldCodes.DestinationCode:
                return versionRules.Any(r => !string.IsNullOrWhiteSpace(r.DestinationCode));
            case OperationalFieldCodes.RouteCode:
                return versionRules.Any(r => !string.IsNullOrWhiteSpace(r.RouteCode));
            case OperationalFieldCodes.CommodityTypeId:
                return versionRules.Any(r => !string.IsNullOrWhiteSpace(r.CommodityCode));
            default:
                return false;
        }
    }

    private static (string? Basis, IReadOnlyCollection<string> Rules) ReadSnapshot(Rating rating)
    {
        var basis = rating.ChargeableBasis;
        var rules = new List<string>();
        if (string.IsNullOrWhiteSpace(rating.ContextJson))
        {
            return (basis, rules);
        }

        try
        {
            using var doc = JsonDocument.Parse(rating.ContextJson);
            if (doc.RootElement.TryGetProperty("rules", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                rules.AddRange(arr.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString()!));
            }

            if (basis is null && doc.RootElement.TryGetProperty("basis", out var b) && b.ValueKind == JsonValueKind.String)
            {
                basis = b.GetString();
            }
        }
        catch (JsonException)
        {
            // Legacy snapshot: fall back to rule-set checks only.
        }

        return (basis, rules);
    }
}
