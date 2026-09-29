using System.Globalization;
using LCMS.Domain.Entities;

namespace LCMS.Application.OperationalReferences.Edit;

public static class OperationalFieldCodes
{
    public const string PackageCount = "package_count";
    public const string GrossWeightKg = "gross_weight_kg";
    public const string VolumeCbm = "volume_cbm";
    public const string ChargeableWeightKg = "chargeable_weight_kg";
    public const string CommodityTypeId = "commodity_type_id";
    public const string SpecialFlags = "special_flags";
    public const string CargoDescription = "cargo_description";
    public const string TransportMode = "transport_mode";
    public const string ServiceTypeCode = "service_type_code";
    public const string OriginCode = "origin_code";
    public const string DestinationCode = "destination_code";
    public const string RouteCode = "route_code";
    public const string EtdAt = "etd_at";
    public const string EtaAt = "eta_at";
    public const string CustomerReference = "customer_reference";
    public const string Description = "description";
    public const string CarrierName = "carrier_name";
    public const string SequenceNo = "sequence_no";
    public const string MovementOn = "movement_on";
}

public static class OperationalFieldKinds
{
    public const string Text = "text";
    public const string Decimal = "decimal";
    public const string Integer = "integer";
    public const string DateTime = "datetime";
    public const string TransportMode = "transport_mode";
    public const string Commodity = "commodity";
    public const string Flags = "flags";
}

/// <summary>One editable operational field. <see cref="RatingRelevant"/> fields can make a rating stale.</summary>
public sealed record OperationalFieldDef(
    string Code,
    string Label,
    string Kind,
    string Group,
    bool RatingRelevant,
    int MaxLength = 0,
    string? MeasureCode = null,
    string? OwnershipKey = null);

/// <summary>Editable field catalog per operational object type (ADR-0039). One pattern for Bill / Order / Shipment / Chặng / Chuyến.</summary>
public static class OperationalFieldCatalog
{
    private static readonly OperationalFieldDef PackageCount = new(OperationalFieldCodes.PackageCount, "Số kiện", OperationalFieldKinds.Integer, "cargo", false, MeasureCode: MeasureCodes.PackageCount);
    private static readonly OperationalFieldDef Gross = new(OperationalFieldCodes.GrossWeightKg, "Trọng lượng thực (kg)", OperationalFieldKinds.Decimal, "cargo", true, MeasureCode: MeasureCodes.GrossWeightKg);
    private static readonly OperationalFieldDef Volume = new(OperationalFieldCodes.VolumeCbm, "Thể tích (CBM)", OperationalFieldKinds.Decimal, "cargo", true, MeasureCode: MeasureCodes.VolumeCbm);
    private static readonly OperationalFieldDef Commodity = new(OperationalFieldCodes.CommodityTypeId, "Loại hàng", OperationalFieldKinds.Commodity, "cargo", true);
    private static readonly OperationalFieldDef Flags = new(OperationalFieldCodes.SpecialFlags, "Thuộc tính đặc biệt (DG / lạnh / quá khổ)", OperationalFieldKinds.Flags, "cargo", false);
    private static readonly OperationalFieldDef CargoDescription = new(OperationalFieldCodes.CargoDescription, "Mô tả hàng hóa", OperationalFieldKinds.Text, "cargo", false, 2000);
    private static readonly OperationalFieldDef Mode = new(OperationalFieldCodes.TransportMode, "Phương thức vận chuyển", OperationalFieldKinds.TransportMode, "service", true, 32);
    private static readonly OperationalFieldDef Service = new(OperationalFieldCodes.ServiceTypeCode, "Loại dịch vụ", OperationalFieldKinds.Text, "service", true, 64);
    private static readonly OperationalFieldDef Origin = new(OperationalFieldCodes.OriginCode, "Điểm đi", OperationalFieldKinds.Text, "route", true, 64, OwnershipKey: "origin_code");
    private static readonly OperationalFieldDef Destination = new(OperationalFieldCodes.DestinationCode, "Điểm đến", OperationalFieldKinds.Text, "route", true, 64, OwnershipKey: "origin_code");
    private static readonly OperationalFieldDef Route = new(OperationalFieldCodes.RouteCode, "Tuyến", OperationalFieldKinds.Text, "route", true, 128, OwnershipKey: "origin_code");
    private static readonly OperationalFieldDef Etd = new(OperationalFieldCodes.EtdAt, "ETD", OperationalFieldKinds.DateTime, "route", false);
    private static readonly OperationalFieldDef Eta = new(OperationalFieldCodes.EtaAt, "ETA", OperationalFieldKinds.DateTime, "route", false);
    private static readonly OperationalFieldDef CustomerRef = new(OperationalFieldCodes.CustomerReference, "Reference khách", OperationalFieldKinds.Text, "other", false, 128);
    private static readonly OperationalFieldDef Description = new(OperationalFieldCodes.Description, "Mô tả", OperationalFieldKinds.Text, "other", false, 2000);
    private static readonly OperationalFieldDef Carrier = new(OperationalFieldCodes.CarrierName, "Hãng vận chuyển", OperationalFieldKinds.Text, "service", false, 256);
    private static readonly OperationalFieldDef Sequence = new(OperationalFieldCodes.SequenceNo, "Thứ tự chặng", OperationalFieldKinds.Integer, "route", false);
    private static readonly OperationalFieldDef MovementOn = new(OperationalFieldCodes.MovementOn, "Ngày chuyến", OperationalFieldKinds.DateTime, "route", false);

    public static readonly OperationalFieldDef ChargeableWeight = new(
        OperationalFieldCodes.ChargeableWeightKg, "Trọng lượng tính cước", OperationalFieldKinds.Decimal, "cargo", true, MeasureCode: MeasureCodes.ChargeableWeightKg);

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<OperationalFieldDef>> ByType =
        new Dictionary<string, IReadOnlyList<OperationalFieldDef>>(StringComparer.Ordinal)
        {
            [OperationalObjectTypes.Bill] =
            [
                PackageCount, Gross, Volume, Commodity, Flags, CargoDescription,
                Mode, Service, Origin, Destination, Route, Etd, Eta, CustomerRef, Description
            ],
            [OperationalObjectTypes.Order] =
            [
                PackageCount, Gross, Volume, Commodity, Flags, CargoDescription,
                Mode, Service, Origin, Destination, Route, Etd, Eta, CustomerRef, Description
            ],
            [OperationalObjectTypes.Shipment] =
            [
                PackageCount, Gross, Volume, Commodity,
                Mode, Service, Carrier, Origin, Destination, Route, Etd, Eta, CustomerRef, Description
            ],
            [OperationalObjectTypes.Leg] = [Sequence, Origin, Destination],
            [OperationalObjectTypes.Movement] = [MovementOn, Mode]
        };

    public static IReadOnlyList<OperationalFieldDef> For(string objectType) =>
        ByType.TryGetValue(objectType, out var list) ? list : [];

    public static OperationalFieldDef? Find(string objectType, string code) =>
        code == OperationalFieldCodes.ChargeableWeightKg && OperationalObjectTypes.CargoParents.Contains(objectType)
            ? ChargeableWeight
            : For(objectType).FirstOrDefault(f => f.Code == code);

    public static string Label(string objectType, string code) => Find(objectType, code)?.Label ?? code;

    public static string ObjectLabel(string objectType) => objectType switch
    {
        OperationalObjectTypes.Bill => "Bill",
        OperationalObjectTypes.Order => "Đơn hàng",
        OperationalObjectTypes.Shipment => "Lô hàng",
        OperationalObjectTypes.Leg => "Chặng",
        OperationalObjectTypes.Movement => "Chuyến",
        _ => objectType
    };
}

/// <summary>Canonical string form for field values (audit + wire). Invariant culture.</summary>
public static class OperationalFieldValues
{
    public static string? FormatDecimal(decimal? value) =>
        value?.ToString("0.####", CultureInfo.InvariantCulture);

    public static string? FormatInt(int? value) => value?.ToString(CultureInfo.InvariantCulture);

    public static string? FormatDate(DateTimeOffset? value) =>
        value?.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    public static decimal? ParseDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return decimal.TryParse(raw.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    public static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
