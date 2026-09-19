using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: bill_waybills — paper waybill snapshot on the Bill financial anchor (ADR-0018).
/// Not a TMS execution record (no dispatch/GPS/e-POD workflow).
/// </summary>
public sealed class BillWaybill : TenantEntityBase
{
    public Guid BillId { get; set; }

    public string? CarrierName { get; set; }
    public string? ItemFormCode { get; set; }

    public string? SenderName { get; set; }
    public string? SenderPhone { get; set; }
    public string? SenderEmail { get; set; }
    public string? SenderAddress { get; set; }
    public string? SenderCustomerCode { get; set; }
    public string? SenderPostalCode { get; set; }

    public string? ConsigneeName { get; set; }
    public string? ConsigneePhone { get; set; }
    public string? ConsigneeEmail { get; set; }
    public string? ConsigneeAddress { get; set; }
    public string? ConsigneeDeliveryCode { get; set; }
    public string? ConsigneePostalCode { get; set; }

    /// <summary>document | goods</summary>
    public string PackageKind { get; set; } = WaybillPackageKinds.Goods;

    public string? ContentsDescription { get; set; }
    public int? ContentsQuantity { get; set; }
    public decimal? DeclaredValue { get; set; }
    public string? AccompanyingDocs { get; set; }

    public string? VatServicesNote { get; set; }

    /// <summary>return_immediately | call_sender | hold_until_pickup | destroy</summary>
    public string? NonDeliveryAction { get; set; }

    public bool SenderCommitAccepted { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    public int ParcelCount { get; set; } = 1;
    public decimal? ActualWeightKg { get; set; }
    public decimal? ChargeableWeightKg { get; set; }

    public string CurrencyCode { get; set; } = "VND";

    public decimal BasePostage { get; set; }
    public decimal VatPostage { get; set; }
    public decimal Surcharge { get; set; }
    public decimal CodFee { get; set; }
    public decimal OtherFee { get; set; }
    public decimal TotalPostageInclVat { get; set; }
    public decimal TotalCollect { get; set; }
    public decimal GrandTotal { get; set; }

    /// <summary>sender | consignee — who pays postage on the paper.</summary>
    public string PostagePayer { get; set; } = WaybillPostagePayers.Sender;

    /// <summary>cost | revenue — Single Economic layer seeded from postage lines (ADR-0018).</summary>
    public string ChargeEconomicRole { get; set; } = WaybillChargeEconomicRoles.Cost;

    /// <summary>Amount to collect from consignee (thu hộ). Not automatic revenue.</summary>
    public decimal CodCollectAmount { get; set; }

    public string? OperationsNote { get; set; }
    public string? AcceptingOffice { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public string? AcceptedBy { get; set; }
    public DateTimeOffset? ReceivedAt { get; set; }
    public string? ReceivedBy { get; set; }

    public Bill? Bill { get; set; }
}

/// <summary>Package kind codes stored on <see cref="BillWaybill.PackageKind"/>.</summary>
public static class WaybillPackageKinds
{
    public const string Document = "document";
    public const string Goods = "goods";
}

/// <summary>Non-delivery instruction codes (paper section 6).</summary>
public static class WaybillNonDeliveryActions
{
    public const string ReturnImmediately = "return_immediately";
    public const string CallSender = "call_sender";
    public const string HoldUntilPickup = "hold_until_pickup";
    public const string Destroy = "destroy";
}

/// <summary>Who pays postage on the waybill.</summary>
public static class WaybillPostagePayers
{
    public const string Sender = "sender";
    public const string Consignee = "consignee";
}

/// <summary>Whether postage lines seed Cost or Revenue (tenant role on the Bill).</summary>
public static class WaybillChargeEconomicRoles
{
    public const string Cost = "cost";
    public const string Revenue = "revenue";
}
