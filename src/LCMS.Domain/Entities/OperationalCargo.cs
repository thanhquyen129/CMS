using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: operational_measurements — typed measure on a Bill, Order, or Shipment.</summary>
public sealed class OperationalMeasurement : TenantEntityBase
{
    public string ObjectType { get; set; } = string.Empty;
    public Guid ObjectId { get; set; }
    public string MeasureCode { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = string.Empty;
    public string SourceChannel { get; set; } = "manual";
    public string? RuleCode { get; set; }
    public bool IsConfirmed { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
}

/// <summary>Table: cargo_packages — child packages used by rating and allocation.</summary>
public sealed class CargoPackage : TenantEntityBase
{
    public string ObjectType { get; set; } = string.Empty;
    public Guid ObjectId { get; set; }
    public int SequenceNo { get; set; }
    public int PackageCount { get; set; }
    public decimal? LengthCm { get; set; }
    public decimal? WidthCm { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? WeightKg { get; set; }
}

/// <summary>Table: cargo_containers — child containers. Not dispatch equipment.</summary>
public sealed class CargoContainer : TenantEntityBase
{
    public string ObjectType { get; set; } = string.Empty;
    public Guid ObjectId { get; set; }
    public int SequenceNo { get; set; }
    public string ContainerType { get; set; } = string.Empty;
    public string? ContainerNo { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal Teu { get; set; }
}

/// <summary>Table: field_ownerships — which system may write a field.</summary>
public sealed class FieldOwnership : TenantEntityBase
{
    public string ObjectType { get; set; } = string.Empty;
    public Guid ObjectId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string OwnerSystem { get; set; } = string.Empty;
}

public static class MeasureCodes
{
    public const string PackageCount = "package_count";
    public const string GrossWeightKg = "gross_weight_kg";
    public const string VolumeCbm = "volume_cbm";
    public const string ChargeableWeightKg = "chargeable_weight_kg";
    public const string ContainerCount = "container_count";
    public const string Teu = "teu";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        PackageCount, GrossWeightKg, VolumeCbm, ChargeableWeightKg, ContainerCount, Teu
    };
}

public static class OperationalObjectTypes
{
    public const string Bill = "bill";
    public const string Order = "order";
    public const string Shipment = "shipment";
    public const string Leg = "leg";
    public const string Movement = "movement";

    public static readonly IReadOnlySet<string> CargoParents = new HashSet<string>(StringComparer.Ordinal)
    {
        Bill, Order, Shipment
    };
}
