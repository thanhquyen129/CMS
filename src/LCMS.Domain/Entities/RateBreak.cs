using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: rate_breaks — weight band on a pricing rule. Bands are data, not code.</summary>
public sealed class RateBreak : TenantEntityBase
{
    public Guid PricingRuleId { get; set; }
    public int SequenceNo { get; set; }
    public decimal MinQuantity { get; set; }
    public decimal? MaxQuantity { get; set; }
    public decimal UnitAmount { get; set; }

    public PricingRule? PricingRule { get; set; }
}

/// <summary>Table: container_rates — unit price per container type.</summary>
public sealed class ContainerRatePrice : TenantEntityBase
{
    public Guid PricingRuleId { get; set; }
    public string ContainerType { get; set; } = string.Empty;
    public decimal UnitAmount { get; set; }

    public PricingRule? PricingRule { get; set; }
}
