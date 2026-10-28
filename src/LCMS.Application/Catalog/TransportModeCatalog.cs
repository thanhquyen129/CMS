using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Pricing;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Catalog;

/// <summary>
/// Tenant transport-mode gate. A new write stores UPPERCASE only when the tenant
/// has an active catalog row and the code is one the platform recognizes.
/// An empty catalog does not invent rows. The same mode already stored is kept
/// byte-for-byte. Historical rows are not updated here.
/// </summary>
public static class TransportModeCatalog
{
    public const string CatalogMissingMessage =
        "Thuê bao chưa có danh mục phương thức vận chuyển đang dùng. Không lưu mã tùy ý.";

    public const string NotAllowedMessage =
        "Phương thức không thuộc bộ mã của hệ thống. Không lưu mã tùy ý.";

    public const string TooLongMessage = "Phương thức vận chuyển tối đa 32 ký tự.";

    public const string MultimodalScopeMessage =
        "Đa phương thức chỉ là phân loại của Shipment, không gán cho chặng.";

    public static string? Canonical(string? value) => RateFieldPolicy.ToStoredMode(value);

    public static async Task<IReadOnlyList<string>> ActiveCodesAsync(
        ILcmsDbContext db,
        CancellationToken cancellationToken) =>
        await db.MasterCatalogItems.AsNoTracking()
            .Where(m => m.Kind == MasterCatalogKinds.TransportMode && m.IsActive)
            .Select(m => m.Code)
            .ToListAsync(cancellationToken);

    public static async Task<string?> ResolveAsync(
        ILcmsDbContext db,
        string? value,
        bool allowMultimodal,
        CancellationToken cancellationToken,
        string? currentStored = null,
        bool forRateRule = false)
    {
        var decision = Decide(
            value,
            await ActiveCodesAsync(db, cancellationToken),
            allowMultimodal,
            currentStored,
            forRateRule);
        return decision.Require();
    }

    public static ModeDecision Decide(
        string? value,
        IReadOnlyList<string> activeCodes,
        bool allowMultimodal,
        string? currentStored = null,
        bool forRateRule = false)
    {
        var code = Canonical(value);
        if (code is null)
        {
            return ModeDecision.Ok(null);
        }

        if (code.Length > 32)
        {
            return ModeDecision.Reject("transport_mode_too_long", TooLongMessage);
        }

        if (currentStored is not null && RateFieldPolicy.SameMode(currentStored, code))
        {
            return ModeDecision.Ok(currentStored);
        }

        if (ShipmentModePolicy.IsMultimodal(code) && !allowMultimodal)
        {
            return ModeDecision.Reject("transport_mode_multimodal_scope", MultimodalScopeMessage);
        }

        if (code == "EXPRESS")
        {
            return ModeDecision.Reject("transport_mode_express_blocked", RateFieldPolicy.ExpressBlocked);
        }

        if (forRateRule && code == "WATER")
        {
            return ModeDecision.Reject("transport_mode_water_blocked", RateFieldPolicy.WaterBlocked);
        }

        if (code is not ("AIR" or "SEA" or "ROAD" or "RAIL" or "OCEAN" or "MULTIMODAL" or "WATER"))
        {
            return ModeDecision.Reject("transport_mode_not_allowed", NotAllowedMessage);
        }

        if (activeCodes.Count == 0)
        {
            return ModeDecision.Reject("transport_mode_catalog_empty", CatalogMissingMessage);
        }

        if (!activeCodes.Any(item => RateFieldPolicy.SameMode(item, code)))
        {
            return ModeDecision.Reject("transport_mode_not_in_catalog", RateFieldPolicy.ModeNotInCatalog);
        }

        return ModeDecision.Ok(code);
    }

    /// <summary>
    /// Catalog admin may add a recognized mode. WATER and EXPRESS stay blocked on new rows.
    /// An existing row can still be renamed or deactivated.
    /// </summary>
    public static ModeDecision CatalogItem(string? code, bool alreadyStored)
    {
        var canonical = Canonical(code);
        if (canonical is null)
        {
            return ModeDecision.Reject("transport_mode_not_allowed", NotAllowedMessage);
        }

        if (alreadyStored)
        {
            return ModeDecision.Ok(canonical);
        }

        if (canonical == "EXPRESS")
        {
            return ModeDecision.Reject("transport_mode_express_blocked", RateFieldPolicy.ExpressBlocked);
        }

        if (canonical is not ("AIR" or "SEA" or "ROAD" or "RAIL" or "OCEAN" or "MULTIMODAL" or "WATER"))
        {
            return ModeDecision.Reject("transport_mode_not_allowed", NotAllowedMessage);
        }

        return ModeDecision.Ok(canonical);
    }

    public static readonly (string Code, string Name, int Sort)[] Baseline =
    [
        ("AIR", "Đường hàng không", 10),
        ("SEA", "Đường biển", 20),
        ("ROAD", "Đường bộ", 30),
        ("RAIL", "Đường sắt", 40),
        ("WATER", "Đường thủy nội địa", 50),
        ("OCEAN", "Đường biển đại dương", 60),
        ("MULTIMODAL", "Đa phương thức", 70)
    ];

    /// <summary>New tenant baseline. Does not update or delete rows already stored.</summary>
    public static async Task EnsureBaselineAsync(ILcmsDbContext db, Guid tenantId, CancellationToken cancellationToken)
    {
        var existing = await db.MasterCatalogItems
            .IgnoreQueryFilters()
            .Where(i => i.TenantId == tenantId && i.Kind == MasterCatalogKinds.TransportMode && i.DeletedAt == null)
            .Select(i => i.Code)
            .ToListAsync(cancellationToken);
        var have = new HashSet<string>(
            existing.Select(code => Canonical(code)).Where(code => code is not null)!,
            StringComparer.Ordinal);

        var added = false;
        foreach (var (code, name, sort) in Baseline)
        {
            if (!have.Add(code))
            {
                continue;
            }

            db.MasterCatalogItems.Add(new MasterCatalogItem
            {
                TenantId = tenantId,
                Kind = MasterCatalogKinds.TransportMode,
                Code = code,
                Name = name,
                IsActive = true,
                SortOrder = sort
            });
            added = true;
        }

        if (added)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}

public readonly record struct ModeDecision(bool Accepted, string? Code, string? ErrorCode, string? Message)
{
    public static ModeDecision Ok(string? code) => new(true, code, null, null);

    public static ModeDecision Reject(string errorCode, string message) => new(false, null, errorCode, message);

    public string? Require()
    {
        if (!Accepted)
        {
            throw ValidationAppException.Coded(Message!, ErrorCode!);
        }

        return Code;
    }
}
