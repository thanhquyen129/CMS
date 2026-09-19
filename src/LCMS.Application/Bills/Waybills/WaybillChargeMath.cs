using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;

namespace LCMS.Application.Bills.Waybills;

/// <summary>Postage line catalog and documentary total checks (ADR-0018).</summary>
public static class WaybillChargeMath
{
    public const decimal Tolerance = 0.01m;

    /// <summary>Component lines that may become Expected Cost/Revenue. Totals are excluded to avoid double count.</summary>
    public static readonly (string Code, string LabelVi, Func<BillWaybill, decimal> Amount)[] ComponentLines =
    [
        ("postage.base", "Cước chính", w => w.BasePostage),
        ("postage.vat", "Cước GTGT", w => w.VatPostage),
        ("postage.surcharge", "Phụ cước", w => w.Surcharge),
        ("postage.cod_fee", "Phí COD", w => w.CodFee),
        ("postage.other", "Thu khác", w => w.OtherFee)
    ];

    /// <summary>Rounds a money amount (half away from zero, 4 dp).</summary>
    public static decimal RoundMoney(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);

    /// <summary>True when the value is economically empty.</summary>
    public static bool IsZero(decimal value) => Math.Abs(value) < Tolerance;

    /// <summary>Validates package/payer/role codes and documentary totals vs component sum.</summary>
    public static void EnsureValid(BillWaybill waybill)
    {
        if (waybill.PackageKind is not WaybillPackageKinds.Document and not WaybillPackageKinds.Goods)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["PackageKind"] = ["Loại hàng gửi phải là tài liệu hoặc hàng hóa."]
            });
        }

        if (waybill.PostagePayer is not WaybillPostagePayers.Sender and not WaybillPostagePayers.Consignee)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["PostagePayer"] = ["Người trả cước phải là người gửi hoặc người nhận."]
            });
        }

        if (waybill.ChargeEconomicRole is not WaybillChargeEconomicRoles.Cost
            and not WaybillChargeEconomicRoles.Revenue)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["ChargeEconomicRole"] = ["Vai trò kinh tế của cước phải là chi phí hoặc doanh thu."]
            });
        }

        if (waybill.NonDeliveryAction is { Length: > 0 }
            && waybill.NonDeliveryAction is not WaybillNonDeliveryActions.ReturnImmediately
                and not WaybillNonDeliveryActions.CallSender
                and not WaybillNonDeliveryActions.HoldUntilPickup
                and not WaybillNonDeliveryActions.Destroy)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["NonDeliveryAction"] = ["Chỉ dẫn không phát không hợp lệ."]
            });
        }

        var componentSum = RoundMoney(
            waybill.BasePostage + waybill.VatPostage + waybill.Surcharge + waybill.CodFee + waybill.OtherFee);

        if (HasAnyCharge(waybill) && Math.Abs(waybill.TotalPostageInclVat - componentSum) > Tolerance)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["TotalPostageInclVat"] =
                [
                    $"Tổng cước (gồm VAT) phải bằng tổng các dòng cước ({componentSum:0.####})."
                ]
            });
        }

        var expectedGrand = RoundMoney(waybill.TotalPostageInclVat + waybill.TotalCollect);
        if (HasAnyCharge(waybill) && Math.Abs(waybill.GrandTotal - expectedGrand) > Tolerance)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["GrandTotal"] =
                [
                    $"Tổng phải bằng tổng cước cộng tổng thu ({expectedGrand:0.####})."
                ]
            });
        }
    }

    /// <summary>Whether any postage/collect amount is non-zero.</summary>
    public static bool HasAnyCharge(BillWaybill waybill) =>
        !IsZero(waybill.BasePostage)
        || !IsZero(waybill.VatPostage)
        || !IsZero(waybill.Surcharge)
        || !IsZero(waybill.CodFee)
        || !IsZero(waybill.OtherFee)
        || !IsZero(waybill.TotalPostageInclVat)
        || !IsZero(waybill.GrandTotal);

    /// <summary>SourceType for a postage component (unique with SourceId = waybill id).</summary>
    public static string SourceTypeFor(string componentCode) =>
        CostSourceTypes.WaybillPrefix + componentCode;
}
