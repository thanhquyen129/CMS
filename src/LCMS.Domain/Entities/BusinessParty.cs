using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: business_parties (D02) — canonical business partner master.</summary>
public sealed class BusinessParty : TenantEntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>Legal / registered name (tên pháp lý trên MST).</summary>
    public string? LegalName { get; set; }

    /// <summary>Tax identification number (MST Việt Nam hoặc mã thuế nước ngoài).</summary>
    public string? TaxId { get; set; }

    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }

    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? Ward { get; set; }
    public string? District { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public string? CountryCode { get; set; }
    public string? PostalCode { get; set; }

    /// <summary>ISO 4217 default transaction / settlement currency.</summary>
    public string? DefaultCurrencyCode { get; set; }

    /// <summary>Default net payment term in days (e.g. 30 = Net 30).</summary>
    public int? PaymentTermDays { get; set; }

    /// <summary>Advisory credit ceiling — display/control UI; hard block needs ADR.</summary>
    public decimal? CreditLimit { get; set; }

    public string? CreditLimitCurrencyCode { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
}
