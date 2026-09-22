using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: commodity_types (D02) — cargo taxonomy and rating flags.
/// Flags support surcharge applicability. They are not warehouse execution state.
/// </summary>
public sealed class CommodityType : TenantEntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public Guid? ParentId { get; set; }

    public bool IsDangerousGoods { get; set; }
    public bool IsTemperatureControlled { get; set; }
    public bool IsOversize { get; set; }
    public bool IsOverweight { get; set; }
    public bool IsHighValue { get; set; }
    public string? SpecialHandling { get; set; }
    public bool IsActive { get; set; } = true;

    public CommodityType? Parent { get; set; }
}
