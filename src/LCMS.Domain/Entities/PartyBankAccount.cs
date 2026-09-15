using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: party_bank_accounts — partner settlement bank accounts.</summary>
public sealed class PartyBankAccount : TenantEntityBase
{
    public Guid PartyId { get; set; }

    public string BankName { get; set; } = string.Empty;
    public string? BankBranch { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string? AccountName { get; set; }

    /// <summary>ISO 4217 account currency.</summary>
    public string CurrencyCode { get; set; } = "VND";

    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Note { get; set; }
}
