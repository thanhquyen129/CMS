using System.Text.Json;
using System.Text.Json.Serialization;

namespace LCMS.Application.OperationalReferences;

/// <summary>
/// Extra create-form fields stored as jsonb. Rating/financial context only — not TMS execution.
/// </summary>
public sealed class OperationalContextDocument
{
    public string? ServiceType { get; set; }
    public string? Incoterm { get; set; }
    public DateTimeOffset? RequestedAt { get; set; }
    public string? PickupLocation { get; set; }
    public string? DeliveryLocation { get; set; }
    public int? PackageCount { get; set; }
    public decimal? GrossWeightKg { get; set; }
    public decimal? VolumeCbm { get; set; }
    public decimal? ChargeableWeightKg { get; set; }
    public int? ContainerCount { get; set; }
    public decimal? Teu { get; set; }
    public string? Commodity { get; set; }
    public IReadOnlyList<string>? SpecialFlags { get; set; }
    public string? CargoDescription { get; set; }
    public string? QuoteReference { get; set; }
    public string? ShipperName { get; set; }
    public string? ConsigneeName { get; set; }
    public IReadOnlyList<string>? ExtraServices { get; set; }
    public string? SpecialInstructions { get; set; }
    public string? ContactName { get; set; }
    public string? ContactChannel { get; set; }
    public string? CarrierName { get; set; }
    public string? PreferredCurrency { get; set; }
    public string? RateDatePolicy { get; set; }
    public Guid? VendorPartyId { get; set; }
    public Guid? BuyRateCardId { get; set; }
    public DateTimeOffset? RateDate { get; set; }
    public string? RatingNote { get; set; }
    public string? MasterBillNo { get; set; }
}

/// <summary>JSON helpers for <see cref="OperationalContextDocument"/>.</summary>
public static class OperationalContextJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Serializes context; returns null when document is empty.</summary>
    public static string? Serialize(OperationalContextDocument? doc)
    {
        if (doc is null || IsEmpty(doc))
        {
            return null;
        }

        return JsonSerializer.Serialize(doc, Options);
    }

    /// <summary>Deserializes jsonb; invalid payloads become null.</summary>
    public static OperationalContextDocument? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<OperationalContextDocument>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Whitespace-only strings become null so TMS upserts can clear a field.</summary>
    public static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsEmpty(OperationalContextDocument d) =>
        d.ServiceType is null
        && d.Incoterm is null
        && d.RequestedAt is null
        && d.PickupLocation is null
        && d.DeliveryLocation is null
        && d.PackageCount is null
        && d.GrossWeightKg is null
        && d.VolumeCbm is null
        && d.ChargeableWeightKg is null
        && d.ContainerCount is null
        && d.Teu is null
        && d.Commodity is null
        && (d.SpecialFlags is null || d.SpecialFlags.Count == 0)
        && d.CargoDescription is null
        && d.QuoteReference is null
        && d.ShipperName is null
        && d.ConsigneeName is null
        && (d.ExtraServices is null || d.ExtraServices.Count == 0)
        && d.SpecialInstructions is null
        && d.ContactName is null
        && d.ContactChannel is null
        && d.CarrierName is null
        && d.PreferredCurrency is null
        && d.RateDatePolicy is null
        && d.VendorPartyId is null
        && d.BuyRateCardId is null
        && d.RateDate is null
        && d.RatingNote is null
        && d.MasterBillNo is null;
}
