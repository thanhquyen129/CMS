using LCMS.Application.Abstractions;
using LCMS.Application.Ratings;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.OperationalReferences.Edit;

/// <summary>
/// Chargeable Weight state shown to users. <see cref="Value"/> is null when not determinable — never 0 (ADR-0039 D02/D07).
/// </summary>
public sealed record ChargeableWeightStateDto(
    string State,
    decimal? Value,
    string Uom,
    string? SourceChannel,
    string SourceLabel,
    decimal? SourceValue,
    string? RuleCode,
    bool IsConfirmed,
    DateTimeOffset? ConfirmedAt,
    string? OverrideReason,
    DateTimeOffset? OverriddenAt,
    bool CanConfirm,
    string? MissingReason,
    bool Persisted);

public static class ChargeableWeightStates
{
    public const string Missing = "missing";
    public const string System = "system";
    public const string Source = "source";
    public const string Manual = "manual";
    public const string Override = "override";
}

/// <summary>System calculation of Chargeable Weight. Needs both Actual Weight and Volume — Actual Weight alone is never CW.</summary>
public static class ChargeableWeightPolicy
{
    public const string AirRuleCode = "AIR_VOLUMETRIC_167";
    public const string SeaRuleCode = "SEA_WM_1000";

    public sealed record Computation(decimal? Value, string? RuleCode, string? MissingReason, string Uom);

    public static bool IsSea(string? transportMode) =>
        transportMode?.Trim().ToLowerInvariant() is "sea" or "ocean";

    public static bool IsAir(string? transportMode) =>
        transportMode?.Trim().ToLowerInvariant() is "air" or "express" or "courier";

    public static string UomFor(string? transportMode) => IsSea(transportMode) ? "W/M" : "kg";

    public static Computation Compute(
        string? transportMode,
        decimal? grossKg,
        decimal? volumeCbm,
        decimal? volumetricFactor = null,
        decimal? roundingStep = null)
    {
        var uom = UomFor(transportMode);
        if (!IsAir(transportMode) && !IsSea(transportMode))
        {
            return new Computation(
                null,
                null,
                string.IsNullOrWhiteSpace(transportMode)
                    ? "Chưa có phương thức vận chuyển để hệ thống tính."
                    : $"Phương thức {transportMode} chưa có quy tắc tính trọng lượng tính cước.",
                uom);
        }

        if (grossKg is not > 0m && volumeCbm is not > 0m)
        {
            return new Computation(null, null, "Thiếu Trọng lượng thực và Thể tích.", uom);
        }

        if (grossKg is not > 0m)
        {
            return new Computation(null, null, "Thiếu Trọng lượng thực.", uom);
        }

        if (volumeCbm is not > 0m)
        {
            return new Computation(null, null, "Thiếu Thể tích.", uom);
        }

        var value = RatingEngine.Chargeable(transportMode, grossKg, volumeCbm, volumetricFactor, roundingStep);
        var rule = IsSea(transportMode)
            ? SeaRuleCode
            : volumetricFactor is decimal f && f != RatingEngine.DefaultAirFactor
                ? $"AIR_VOLUMETRIC_{f:0.##}"
                : AirRuleCode;
        return new Computation(value, rule, null, uom);
    }

    public static string SourceLabel(string? channel, string? sourceSystem = null) => channel switch
    {
        MeasureSourceChannels.System => "Hệ thống tính",
        MeasureSourceChannels.Import => string.IsNullOrWhiteSpace(sourceSystem) ? "Import" : $"Import ({sourceSystem})",
        MeasureSourceChannels.Api => string.IsNullOrWhiteSpace(sourceSystem) ? "API" : $"API ({sourceSystem})",
        MeasureSourceChannels.Override => "Ghi đè",
        MeasureSourceChannels.Manual => "Nhập thủ công",
        _ => "Chưa xác định"
    };

    /// <summary>Resolves the effective Chargeable Weight for a cargo parent (bill / order / shipment).</summary>
    public static async Task<ChargeableWeightStateDto> ResolveAsync(
        ILcmsDbContext db,
        string objectType,
        Guid objectId,
        string? transportMode,
        string? sourceSystem,
        CancellationToken cancellationToken)
    {
        var measures = await db.OperationalMeasurements.AsNoTracking()
            .Where(m => m.ObjectType == objectType && m.ObjectId == objectId)
            .ToListAsync(cancellationToken);
        var overrideRow = await db.OperationalFieldOverrides.AsNoTracking()
            .FirstOrDefaultAsync(
                o => o.ObjectType == objectType && o.ObjectId == objectId && o.FieldCode == OperationalFieldCodes.ChargeableWeightKg,
                cancellationToken);
        return Resolve(measures, overrideRow, transportMode, sourceSystem);
    }

    public static ChargeableWeightStateDto Resolve(
        IReadOnlyCollection<OperationalMeasurement> measures,
        OperationalFieldOverride? overrideRow,
        string? transportMode,
        string? sourceSystem)
    {
        var gross = measures.FirstOrDefault(m => m.MeasureCode == MeasureCodes.GrossWeightKg)?.Quantity;
        var volume = measures.FirstOrDefault(m => m.MeasureCode == MeasureCodes.VolumeCbm)?.Quantity;
        var cw = measures.FirstOrDefault(m => m.MeasureCode == MeasureCodes.ChargeableWeightKg);
        var computed = Compute(transportMode, gross, volume);
        var uom = computed.Uom;

        if (cw is not null)
        {
            var channel = string.IsNullOrWhiteSpace(cw.SourceChannel) ? MeasureSourceChannels.Manual : cw.SourceChannel;
            var state = channel switch
            {
                MeasureSourceChannels.Override => ChargeableWeightStates.Override,
                MeasureSourceChannels.System => ChargeableWeightStates.System,
                MeasureSourceChannels.Import or MeasureSourceChannels.Api => ChargeableWeightStates.Source,
                _ => ChargeableWeightStates.Manual
            };
            decimal? sourceValue = null;
            if (state == ChargeableWeightStates.Override)
            {
                sourceValue = OperationalFieldValues.ParseDecimal(overrideRow?.SourceValue) ?? computed.Value;
            }

            return new ChargeableWeightStateDto(
                state,
                cw.Quantity,
                uom,
                channel,
                SourceLabel(channel, sourceSystem),
                sourceValue,
                cw.RuleCode,
                cw.IsConfirmed,
                cw.ConfirmedAt,
                state == ChargeableWeightStates.Override ? overrideRow?.Reason : null,
                state == ChargeableWeightStates.Override ? overrideRow?.OverriddenAt : null,
                !cw.IsConfirmed,
                null,
                true);
        }

        if (computed.Value is decimal systemValue)
        {
            return new ChargeableWeightStateDto(
                ChargeableWeightStates.System,
                systemValue,
                uom,
                MeasureSourceChannels.System,
                SourceLabel(MeasureSourceChannels.System),
                null,
                computed.RuleCode,
                false,
                null,
                null,
                null,
                true,
                null,
                false);
        }

        return new ChargeableWeightStateDto(
            ChargeableWeightStates.Missing,
            null,
            uom,
            null,
            "Chưa xác định",
            null,
            null,
            false,
            null,
            null,
            null,
            false,
            computed.MissingReason ?? "Chưa tính được.",
            false);
    }
}
