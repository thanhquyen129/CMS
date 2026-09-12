namespace LCMS.Application.Settlements;

/// <summary>
/// Settlement module options — FX stub + small remainder write-off cap (Pass 2 Sprint 8 FULL).
/// </summary>
public sealed class SettlementOptions
{
    public const string SectionName = "Settlement";

    /// <summary>Tenant reporting/base currency for stub FX (default VND).</summary>
    public string BaseCurrency { get; set; } = "VND";

    /// <summary>
    /// Stub FX rates: 1 unit of key currency → BaseCurrency.
    /// Missing key ⇒ reject cross-currency convert (C-014 stub).
    /// </summary>
    public Dictionary<string, decimal> StubFxRatesToBase { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = 25_000m,
        ["EUR"] = 27_000m
    };

    /// <summary>
    /// Max write-off amount applied immediately without Approval (txn currency).
    /// Above this → create pending Approval; apply when approved (P03 / ADR-0008).
    /// </summary>
    public decimal MaxWriteOffAmount { get; set; } = 1_000m;
}
