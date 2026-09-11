using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: currencies — ISO currency master (global catalog).</summary>
public sealed class Currency : EntityBase
{
    /// <summary>ISO 4217 code (e.g. VND, USD).</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public int DecimalPlaces { get; set; } = 2;
    public bool IsActive { get; set; } = true;
}
