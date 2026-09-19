using LCMS.Application.Abstractions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;

namespace LCMS.Application.Bills.Waybills;

/// <summary>Maps entity ↔ DTO and applies write-body snapshots.</summary>
public static class BillWaybillMapper
{
    /// <summary>Applies capture/upsert body onto a waybill row (money rounded).</summary>
    public static void Apply(BillWaybill target, WaybillWriteBody body)
    {
        target.CarrierName = TrimOrNull(body.CarrierName);
        target.ItemFormCode = TrimOrNull(body.ItemFormCode);

        var sender = body.Sender;
        target.SenderName = TrimOrNull(sender?.Name);
        target.SenderPhone = TrimOrNull(sender?.Phone);
        target.SenderEmail = TrimOrNull(sender?.Email);
        target.SenderAddress = TrimOrNull(sender?.Address);
        target.SenderCustomerCode = TrimOrNull(sender?.CustomerCode);
        target.SenderPostalCode = TrimOrNull(sender?.PostalCode);

        var consignee = body.Consignee;
        target.ConsigneeName = TrimOrNull(consignee?.Name);
        target.ConsigneePhone = TrimOrNull(consignee?.Phone);
        target.ConsigneeEmail = TrimOrNull(consignee?.Email);
        target.ConsigneeAddress = TrimOrNull(consignee?.Address);
        target.ConsigneeDeliveryCode = TrimOrNull(consignee?.DeliveryCode);
        target.ConsigneePostalCode = TrimOrNull(consignee?.PostalCode);

        target.PackageKind = string.IsNullOrWhiteSpace(body.PackageKind)
            ? WaybillPackageKinds.Goods
            : body.PackageKind.Trim().ToLowerInvariant();
        target.ContentsDescription = TrimOrNull(body.ContentsDescription);
        target.ContentsQuantity = body.ContentsQuantity;
        target.DeclaredValue = body.DeclaredValue is decimal dv
            ? WaybillChargeMath.RoundMoney(dv)
            : null;
        target.AccompanyingDocs = TrimOrNull(body.AccompanyingDocs);
        target.VatServicesNote = TrimOrNull(body.VatServicesNote);
        target.NonDeliveryAction = string.IsNullOrWhiteSpace(body.NonDeliveryAction)
            ? null
            : body.NonDeliveryAction.Trim().ToLowerInvariant();
        target.SenderCommitAccepted = body.SenderCommitAccepted;
        target.SentAt = body.SentAt?.ToUniversalTime();
        target.ParcelCount = body.ParcelCount is > 0 ? body.ParcelCount.Value : 1;
        target.ActualWeightKg = body.ActualWeightKg;
        target.ChargeableWeightKg = body.ChargeableWeightKg;

        target.CurrencyCode = string.IsNullOrWhiteSpace(body.CurrencyCode)
            ? "VND"
            : body.CurrencyCode.Trim().ToUpperInvariant();
        target.BasePostage = Money(body.BasePostage);
        target.VatPostage = Money(body.VatPostage);
        target.Surcharge = Money(body.Surcharge);
        target.CodFee = Money(body.CodFee);
        target.OtherFee = Money(body.OtherFee);
        var componentSum = WaybillChargeMath.RoundMoney(
            target.BasePostage + target.VatPostage + target.Surcharge + target.CodFee + target.OtherFee);
        if (body.TotalPostageInclVat is decimal tp)
        {
            target.TotalPostageInclVat = WaybillChargeMath.RoundMoney(tp);
            var residual = WaybillChargeMath.RoundMoney(target.TotalPostageInclVat - componentSum);
            if (residual > WaybillChargeMath.Tolerance)
            {
                // Paper totals often omit a split line (common on VNPost). Keep the charged total; residual → Thu khác.
                target.OtherFee = WaybillChargeMath.RoundMoney(target.OtherFee + residual);
            }
        }
        else
        {
            target.TotalPostageInclVat = componentSum;
        }
        target.TotalCollect = Money(body.TotalCollect);
        target.GrandTotal = body.GrandTotal is decimal gt
            ? WaybillChargeMath.RoundMoney(gt)
            : WaybillChargeMath.RoundMoney(target.TotalPostageInclVat + target.TotalCollect);
        target.PostagePayer = string.IsNullOrWhiteSpace(body.PostagePayer)
            ? WaybillPostagePayers.Sender
            : body.PostagePayer.Trim().ToLowerInvariant();
        target.ChargeEconomicRole = string.IsNullOrWhiteSpace(body.ChargeEconomicRole)
            ? WaybillChargeEconomicRoles.Cost
            : body.ChargeEconomicRole.Trim().ToLowerInvariant();
        target.CodCollectAmount = Money(body.CodCollectAmount);

        target.OperationsNote = TrimOrNull(body.OperationsNote);
        target.AcceptingOffice = TrimOrNull(body.AcceptingOffice);
        target.AcceptedAt = body.AcceptedAt?.ToUniversalTime();
        target.AcceptedBy = TrimOrNull(body.AcceptedBy);
        target.ReceivedAt = body.ReceivedAt?.ToUniversalTime();
        target.ReceivedBy = TrimOrNull(body.ReceivedBy);
    }

    /// <summary>Builds the API DTO; redacts postage when the caller lacks the matching finance permission.</summary>
    public static BillWaybillDto ToDto(
        BillWaybill waybill,
        string billNo,
        bool canSeeCharges)
    {
        var charges = canSeeCharges
            ? new WaybillChargesDto(
                waybill.BasePostage,
                waybill.VatPostage,
                waybill.Surcharge,
                waybill.CodFee,
                waybill.OtherFee,
                waybill.TotalPostageInclVat,
                waybill.TotalCollect,
                waybill.GrandTotal,
                waybill.CurrencyCode,
                waybill.PostagePayer,
                waybill.ChargeEconomicRole,
                waybill.CodCollectAmount,
                AmountsRedacted: false)
            : new WaybillChargesDto(
                0, 0, 0, 0, 0, 0, 0, 0,
                waybill.CurrencyCode,
                waybill.PostagePayer,
                waybill.ChargeEconomicRole,
                0,
                AmountsRedacted: true);

        return new BillWaybillDto(
            waybill.Id,
            waybill.BillId,
            billNo,
            waybill.CarrierName,
            waybill.ItemFormCode,
            new WaybillPartyDto(
                waybill.SenderName,
                waybill.SenderPhone,
                waybill.SenderEmail,
                waybill.SenderAddress,
                waybill.SenderCustomerCode,
                waybill.SenderPostalCode),
            new WaybillPartyDto(
                waybill.ConsigneeName,
                waybill.ConsigneePhone,
                waybill.ConsigneeEmail,
                waybill.ConsigneeAddress,
                CustomerCode: null,
                waybill.ConsigneePostalCode,
                waybill.ConsigneeDeliveryCode),
            waybill.PackageKind,
            waybill.ContentsDescription,
            waybill.ContentsQuantity,
            canSeeCharges ? waybill.DeclaredValue : null,
            waybill.AccompanyingDocs,
            waybill.VatServicesNote,
            waybill.NonDeliveryAction,
            waybill.SenderCommitAccepted,
            waybill.SentAt,
            waybill.ParcelCount,
            waybill.ActualWeightKg,
            waybill.ChargeableWeightKg,
            charges,
            waybill.OperationsNote,
            waybill.AcceptingOffice,
            waybill.AcceptedAt,
            waybill.AcceptedBy,
            waybill.ReceivedAt,
            waybill.ReceivedBy);
    }

    /// <summary>CostRead for cost-role postage; RevenueRead for revenue-role postage.</summary>
    public static async Task<bool> CanSeeChargesAsync(
        IPermissionService permissions,
        string chargeEconomicRole,
        CancellationToken cancellationToken)
    {
        if (string.Equals(chargeEconomicRole, WaybillChargeEconomicRoles.Revenue, StringComparison.OrdinalIgnoreCase))
        {
            return await permissions.HasPermissionAsync(PermissionCodes.RevenueRead, cancellationToken);
        }

        return await permissions.HasPermissionAsync(PermissionCodes.CostRead, cancellationToken);
    }

    private static decimal Money(decimal? value) =>
        WaybillChargeMath.RoundMoney(value ?? 0m);

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
