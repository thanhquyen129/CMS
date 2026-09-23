using LCMS.Application.Ratings;
using LCMS.Domain.Entities;

namespace LCMS.Application.Demo;

/// <summary>
/// NewSkyExpress VN–MY reference tariffs (docs/po/Bảng giá tham khảo).
/// Air AVMCC26_002 (24/03/2026, VND/kg). Sea SYMCC23_002_CBM (05/08/2023, USD/CBM).
/// Sabah/Sarawak remote fee is 85,000 VND per actual kg.
/// </summary>
public static class ReferenceTariffCatalog
{
    public const string AirCode = "NSE-AIR-VN-MY";
    public const string SeaCode = "NSE-SEA-VN-MY";
    public const string Carrier = "NewSkyExpress";
    public const string Route = "VN-MY";
    public const decimal RemotePerKgVnd = 85_000m;
    public const decimal DeliveryUnder2KgVnd = 50_000m;

    public const string CommodityGeneral = "GENERAL";
    public const string CommodityDryFood = "DRY_FOOD";
    public const string CommodityCosmetics = "COSMETICS";
    public const string CommodityCosmeticsFood = "COSMETICS_FOOD";
    public const string CommodityExpress = "EXPRESS";

    public const string ChargeFreight = "FREIGHT";
    public const string ChargeDelivery = "DELIVERY";
    public const string ChargeRemote = "REMOTE";

    public static readonly (string Code, string Name)[] Commodities =
    [
        (CommodityGeneral, "Hàng thường"),
        (CommodityDryFood, "Thực phẩm khô"),
        (CommodityCosmetics, "Mỹ phẩm"),
        (CommodityCosmeticsFood, "Mỹ phẩm, thực phẩm"),
        (CommodityExpress, "Chuyển nhanh")
    ];

    public static IReadOnlyList<ReferenceRateCard> Cards { get; } = [Air(), Sea()];

    public sealed record ReferenceRateCard(
        string Code,
        string Name,
        string CurrencyCode,
        string TransportMode,
        string Description,
        DateTimeOffset EffectiveFrom,
        string Note,
        IReadOnlyList<ReferenceRule> Rules);

    public sealed record ReferenceRule(
        string Code,
        string Name,
        string CalcMethod,
        decimal UnitAmount,
        string CurrencyCode,
        string? ChargeCode,
        string? CommodityCode,
        string? DestinationCode,
        string? Applicability,
        decimal? MinAmount,
        decimal? VolumetricFactor,
        int SortOrder,
        IReadOnlyList<ReferenceBreak> Breaks);

    public sealed record ReferenceBreak(
        int SequenceNo,
        decimal MinQuantity,
        decimal? MaxQuantity,
        decimal UnitAmount);

    private static ReferenceRateCard Air()
    {
        var bands = new (decimal Min, decimal? Max)[]
        {
            (0m, 5.999m),
            (6m, 10.999m),
            (11m, 20.999m),
            (21m, 100.999m),
            (101m, 300.999m),
            (301m, 500.999m),
            (501m, null)
        };
        decimal?[] general = [100_000, 65_000, 60_000, 58_000, 56_000, 54_000, 52_500];
        decimal?[] dry = [100_000, 68_000, 66_000, 65_000, 60_000, 59_000, 58_000];
        decimal?[] cosmetics = [100_000, 68_000, 66_000, 65_000, 60_000, 59_000, 58_000];
        decimal?[] express = [null, null, 87_000, 81_000, 73_000, 70_000, 65_000];

        var rules = new List<ReferenceRule>
        {
            Freight("AIR-GENERAL", "Hàng thường", CommodityGeneral, "VND", 10, bands, general, 167m),
            Freight("AIR-DRY-FOOD", "Thực phẩm khô", CommodityDryFood, "VND", 20, bands, dry, 167m),
            Freight("AIR-COSMETICS", "Mỹ phẩm", CommodityCosmetics, "VND", 30, bands, cosmetics, 167m),
            Freight("AIR-EXPRESS", "Chuyển nhanh", CommodityExpress, "VND", 40, bands, express, 167m),
            new(
                "AIR-DELIVERY",
                "Phí giao hàng dưới 2 kg",
                PricingCalcMethods.WeightStep,
                0m,
                "VND",
                ChargeDelivery,
                null,
                null,
                null,
                null,
                null,
                50,
                [
                    new(1, 0m, 1.999m, DeliveryUnder2KgVnd),
                    new(2, 2m, null, 0m)
                ]),
            Remote("AIR-REMOTE-SBH", "SBH", 60, perGrossKg: false),
            Remote("AIR-REMOTE-SWK", "SWK", 61, perGrossKg: false)
        };

        return new ReferenceRateCard(
            AirCode,
            "Cước Air Việt Nam – Malaysia",
            "VND",
            "air",
            "Bảng giá tham khảo NewSkyExpress, số AVMCC26_002.",
            new DateTimeOffset(2026, 3, 24, 0, 0, 0, TimeSpan.Zero),
            "AVMCC26_002, hiệu lực 24/03/2026. VND/kg theo khung; 10,5 kg dùng giá 6–10. Chuyển nhanh không có giá dưới 11 kg. Giao dưới 2 kg: 50.000 đ/đơn. Sabah/Sarawak (SBH, SWK): 85.000 đ/kg. Chưa gồm phí xử lý và VAT.",
            rules);
    }

    private static ReferenceRateCard Sea()
    {
        var bands = new (decimal Min, decimal? Max)[]
        {
            (0m, 3.999m),
            (4m, 6.999m),
            (7m, 10m),
            (10.001m, null)
        };
        decimal?[] general = [170, 160, 155, 150];
        decimal?[] special = [180, 175, 165, 160];

        var rules = new List<ReferenceRule>
        {
            Freight("SEA-GENERAL", "Hàng thường", CommodityGeneral, "USD", 10, bands, general, null, minCharge: 170m),
            Freight("SEA-SPECIAL", "Mỹ phẩm, thực phẩm", CommodityCosmeticsFood, "USD", 20, bands, special, null, minCharge: 180m),
            Remote("SEA-REMOTE-SBH", "SBH", 60, perGrossKg: true),
            Remote("SEA-REMOTE-SWK", "SWK", 61, perGrossKg: true)
        };

        return new ReferenceRateCard(
            SeaCode,
            "Cước Sea Việt Nam – Malaysia",
            "USD",
            "sea",
            "Bảng giá tham khảo NewSkyExpress, số SYMCC23_002_CBM.",
            new DateTimeOffset(2023, 8, 5, 0, 0, 0, TimeSpan.Zero),
            "SYMCC23_002_CBM, hiệu lực 05/08/2023. USD/CBM, tối thiểu 1 CBM. Khung 4–6 gồm đến dưới 7 CBM. Sabah/Sarawak: 85.000 đ/kg thực khi điểm đến SBH hoặc SWK (cần tỷ giá VND/USD). Chưa gồm phí xử lý và VAT.",
            rules);
    }

    private static ReferenceRule Freight(
        string code,
        string name,
        string commodity,
        string currency,
        int sort,
        (decimal Min, decimal? Max)[] bands,
        decimal?[] rates,
        decimal? volumetric,
        decimal? minCharge = null)
    {
        var breaks = new List<ReferenceBreak>();
        var seq = 1;
        for (var i = 0; i < bands.Length; i++)
        {
            if (rates[i] is not decimal rate)
            {
                continue;
            }

            breaks.Add(new ReferenceBreak(seq++, bands[i].Min, bands[i].Max, rate));
        }

        return new ReferenceRule(
            code,
            name,
            PricingCalcMethods.WeightBreakPivot,
            0m,
            currency,
            ChargeFreight,
            commodity,
            null,
            null,
            minCharge,
            volumetric,
            sort,
            breaks);
    }

    private static ReferenceRule Remote(string code, string destination, int sort, bool perGrossKg) =>
        new(
            code,
            $"Sabah/Sarawak {destination}",
            PricingCalcMethods.UnitRate,
            RemotePerKgVnd,
            "VND",
            ChargeRemote,
            null,
            destination,
            perGrossKg ? RatingEngine.PerGrossKg : null,
            null,
            null,
            sort,
            []);
}
