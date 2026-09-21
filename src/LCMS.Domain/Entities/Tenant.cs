using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: tenants (D01). Root of C-001 isolation — <see cref="EntityBase.Id"/> is the tenant identity.
/// TD1: không xóa vật lý khi đã có dữ liệu → soft-delete only.
/// </summary>
public sealed class Tenant : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>Legal / trade name for documents (UI-14).</summary>
    public string? LegalName { get; set; }
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

    /// <summary>IANA timezone for display (storage remains UTC).</summary>
    public string TimeZoneId { get; set; } = TenantDefaults.TimeZoneId;
    public string DateFormat { get; set; } = TenantDefaults.DateFormat;
    public string DefaultCurrencyCode { get; set; } = TenantDefaults.CurrencyCode;

    public string? LogoContentType { get; set; }
    public byte[]? LogoBytes { get; set; }
}

public static class TenantDefaults
{
    public const string TimeZoneId = "Asia/Ho_Chi_Minh";
    public const string DateFormat = "dd/MM/yyyy";
    public const string CurrencyCode = "VND";
    public const int LogoMaxBytes = 512 * 1024;
}
