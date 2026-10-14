using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Trace of header route/carrier copied onto rules. Does not rewrite rating history.</summary>
public sealed class RateScopeMigrationLog : TenantEntityBase
{
    public Guid RateCardId { get; set; }
    public Guid? PricingRuleId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Detail { get; set; }
}
