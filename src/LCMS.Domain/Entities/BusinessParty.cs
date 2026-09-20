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

    /// <summary>organization | individual</summary>
    public string PartyKind { get; set; } = PartyKinds.Organization;

    /// <summary>Trade / short name used on bills and typeahead.</summary>
    public string? ShortName { get; set; }

    /// <summary>company | llc | jsc | individual | household | foreign | other</summary>
    public string? LegalType { get; set; }

    /// <summary>Tenant grouping (VIP, tuyến bắc, …).</summary>
    public string? GroupCode { get; set; }

    /// <summary>External TMS/ERP code — unique per tenant when set.</summary>
    public string? ExternalCode { get; set; }

    public string? IndustryCode { get; set; }

    public string? InvoiceEmail { get; set; }

    public bool? VatRegistered { get; set; }

    public Guid? AssignedUserId { get; set; }

    public Guid? ParentPartyId { get; set; }

    /// <summary>advisory | warn | block — ADR-0020.</summary>
    public string CreditControlMode { get; set; } = PartyCreditControlModes.Advisory;

    /// <summary>Credit/compliance hold — cannot attach to new financial facts.</summary>
    public bool IsBlocked { get; set; }

    public string? BlockedReason { get; set; }

    public DateTimeOffset? BlockedAt { get; set; }

    public bool CanTransact() => IsActive && !IsBlocked;
}
