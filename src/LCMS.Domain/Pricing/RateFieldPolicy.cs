namespace LCMS.Domain.Pricing;

/// <summary>
/// Canonical service/mode codes and the explicit charge-group contract (DEC-01..04).
/// Does not read the database and does not rewrite published history.
/// </summary>
public static class RateFieldPolicy
{
    public const string ServiceAirBlocked =
        "Loại dịch vụ AIR trùng phương thức vận chuyển. Không chọn cho quy tắc mới. Dữ liệu cũ vẫn được giữ để xem.";

    public const string WaterBlocked =
        "Đường thủy (WATER) là dữ liệu cũ, chưa có trong danh mục phương thức. Không tạo mới và không đổi thành đường biển.";

    public const string ExpressBlocked =
        "Chuyển phát nhanh (EXPRESS) chưa được bật vì Bill và Chặng chưa có quy tắc trọng lượng riêng. Không đổi thành hàng không.";

    public const string ModeRouteConflict =
        "Phương thức không khớp tuyến đã chọn. Chọn đúng phương thức của tuyến, hoặc chọn Tất cả tuyến.";

    public const string ChargeRequired =
        "Dòng quy tắc chưa có khoản giá chuẩn. Chọn khoản giá từ danh mục; hệ thống tự xác định nhóm tính giá.";

    public const string ChargeNotInCatalog =
        "Khoản giá không có trong danh mục khoản giá đang dùng. Không nhập mã tự do; khai báo khoản giá tại Khoản mục kinh tế trước.";

    public const string ChargeCodeMismatch =
        "Nhóm tính giá do hệ thống xác định từ khoản giá đã chọn. Không gửi mã nhóm khác khoản giá.";

    public const string DraftChargeWarning =
        "Bản nháp còn dòng chưa có khoản giá chuẩn. Có thể lưu nháp, chưa phát hành được.";

    public const string LegacySurchargeWarning =
        "Phiên bản cũ có quy tắc phụ phí giao hàng / vùng xa bị tách khỏi cước chính khi tính giá. Có nguy cơ bỏ sót phụ phí. Dữ liệu đã phát hành giữ nguyên; khai báo lại ở Quản lý phụ phí hoặc phiên bản mới.";

    public const string SurchargeRedirect =
        "Phụ phí không khai trên quy tắc giá. Hãy tạo ở Quản lý phụ phí.";

    public const string PublishCostRequired =
        "Phát hành bảng giá mua cần loại chi phí trên từng dòng thu/chi.";

    public const string PublishRevenueRequired =
        "Phát hành bảng giá bán cần loại doanh thu trên từng dòng thu/chi.";

    public const string PublishSupplierRequired =
        "Bảng giá mua cần nhà cung cấp trước khi phát hành để tính theo chặng.";

    public const string DraftFinancialWarning =
        "Bản nháp còn dòng chưa có loại chi phí hoặc loại doanh thu. Có thể lưu, chưa phát hành được.";

    public const string Unclassified = "Chưa phân loại kinh tế";

    public const string ServiceNotInCatalog = "Loại dịch vụ không có trong danh mục đang dùng.";

    public const string ModeNotInCatalog = "Phương thức không có trong danh mục đang dùng.";

    public const string CommodityNotInCatalog = "Loại hàng không có trong danh mục đang dùng.";

    public const string CatalogEmpty = "Danh mục trống. Không tự sinh mã.";

    public const string MatrixCodeTooLong =
        "Mã quy tắc ma trận vượt 64 ký tự. Rút ngắn mã nhóm hoặc mã loại hàng.";

    public const string MatrixCodeCollision =
        "Mã quy tắc ma trận bị trùng. Đổi mã nhóm hoặc mã loại hàng.";

    public const string MatrixGroupOverlap =
        "Hai nhóm ma trận cùng tuyến, phương thức, hãng và loại hàng sẽ cộng trùng cước chính.";

    /// <summary>
    /// EXPRESS still uses the air volumetric weight rule, so it is not an independent rating context.
    /// </summary>
    public static bool ExpressContextSupported => false;

    public static bool IsLegacyServiceAir(string? code) =>
        string.Equals(code?.Trim(), "AIR", StringComparison.OrdinalIgnoreCase);

    public static string? ToStoredMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "AIR" => "air",
            "SEA" => "sea",
            "ROAD" => "road",
            "RAIL" => "rail",
            "WATER" => "water",
            "EXPRESS" => "express",
            _ => value.Trim().ToLowerInvariant()
        };
    }

    public static string? ToCanonicalMode(string? stored)
    {
        var mode = ToStoredMode(stored);
        return mode?.ToUpperInvariant();
    }

    public static bool SameMode(string? left, string? right)
    {
        var a = ToStoredMode(left);
        var b = ToStoredMode(right);
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        return string.Equals(a, b, StringComparison.Ordinal);
    }

    public static string? NewModeBlock(string? mode)
    {
        var stored = ToStoredMode(mode);
        if (stored is null)
        {
            return null;
        }

        if (stored == "water")
        {
            return WaterBlocked;
        }

        if (stored == "express" && !ExpressContextSupported)
        {
            return ExpressBlocked;
        }

        return null;
    }

    public static string? RouteModeConflict(string? routeMode, string? ruleMode, bool appliesAnyRoute)
    {
        if (appliesAnyRoute)
        {
            return null;
        }

        var route = ToStoredMode(routeMode);
        var rule = ToStoredMode(ruleMode);
        if (route is null || rule is null || route == "multimodal")
        {
            return null;
        }

        return SameMode(route, rule) ? null : ModeRouteConflict;
    }

    public static string? ServiceBlock(string? serviceTypeCode, bool alreadyStored)
    {
        if (!IsLegacyServiceAir(serviceTypeCode) || alreadyStored)
        {
            return null;
        }

        return ServiceAirBlocked;
    }

    public static string NextBasicCode(IEnumerable<string> existing)
    {
        var used = new HashSet<string>(
            existing.Where(code => !string.IsNullOrWhiteSpace(code)).Select(code => code.Trim()),
            StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < 100_000; i++)
        {
            var code = $"QT{i:0000}";
            if (!used.Contains(code))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Không cấp được mã quy tắc duy nhất.");
    }

    public static string SuggestName(string? chargeLabel, string? routeLabel, decimal? min, decimal? max)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(chargeLabel))
        {
            parts.Add(chargeLabel.Trim());
        }

        if (!string.IsNullOrWhiteSpace(routeLabel))
        {
            parts.Add(routeLabel.Trim());
        }

        if (min is not null || max is not null)
        {
            parts.Add(WeightBand.Label(min ?? 0m, max));
        }

        return string.Join(" · ", parts);
    }

    public static string PartyLabel(string? partyType) =>
        string.Equals(partyType?.Trim(), "customer", StringComparison.OrdinalIgnoreCase) ? "Giá bán" : "Giá mua";

    /// <summary>The header is the only price direction. A blank rule value inherits; a different one is rejected.</summary>
    public static string? PartyTypeConflict(string headerPartyType, string? rulePartyType)
    {
        if (string.IsNullOrWhiteSpace(rulePartyType)
            || string.Equals(rulePartyType.Trim(), headerPartyType.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rule = rulePartyType.Trim().ToLowerInvariant() switch
        {
            "customer" => "Giá bán",
            "vendor" => "Giá mua",
            _ => "không xác định"
        };
        return $"Loại giá của quy tắc ({rule}) trái với Loại giá ở Thông tin chung ({PartyLabel(headerPartyType)}). Quy tắc kế thừa Loại giá của bảng giá; bỏ Loại giá ở dòng quy tắc.";
    }

    public static string PublishChargeMissing(string ruleCode) =>
        $"Chưa phát hành được: quy tắc {ruleCode} chưa có khoản giá chuẩn trong danh mục nên hệ thống chưa xác định được nhóm tính giá. Bản nháp vẫn được giữ; hệ thống không tự gán cước vận tải mặc định.";

    public static string? PublishBlock(
        bool buy,
        bool hasSupplier,
        IReadOnlyList<ExplicitRuleFinance> rules,
        IReadOnlyCollection<string>? chargeCatalog = null)
    {
        if (buy && !hasSupplier)
        {
            return PublishSupplierRequired;
        }

        foreach (var rule in rules)
        {
            if (chargeCatalog is not null
                && (string.IsNullOrWhiteSpace(rule.ChargeCode)
                    || !chargeCatalog.Contains(rule.ChargeCode.Trim(), StringComparer.OrdinalIgnoreCase)))
            {
                return PublishChargeMissing(rule.RuleCode ?? "");
            }

            if (string.IsNullOrWhiteSpace(rule.ChargeCode))
            {
                return ChargeRequired;
            }

            if (string.Equals(rule.LineRole, "surcharge", StringComparison.OrdinalIgnoreCase))
            {
                return SurchargeRedirect;
            }

            if (buy && string.IsNullOrWhiteSpace(rule.CostTypeCode))
            {
                return PublishCostRequired;
            }

            if (!buy && string.IsNullOrWhiteSpace(rule.RevenueTypeCode))
            {
                return PublishRevenueRequired;
            }
        }

        return null;
    }

    public static string? MatrixCodeError(IEnumerable<string> codes)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in codes)
        {
            var code = raw.Trim();
            if (code.Length > 64)
            {
                return MatrixCodeTooLong;
            }

            if (!seen.Add(code))
            {
                return MatrixCodeCollision;
            }
        }

        return null;
    }

    public static string? MatrixGroupOverlapError(IEnumerable<MatrixGroupScope> groups)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            var route = group.AppliesAnyRoute ? "*" : group.RouteKey?.Trim() ?? "";
            var mode = ToStoredMode(group.TransportMode) ?? "";
            var carrier = group.CarrierKey?.Trim() ?? "";
            foreach (var commodity in group.CommodityCodes)
            {
                var key = $"{route}|{mode}|{carrier}|{commodity.Trim().ToUpperInvariant()}";
                if (!seen.Add(key))
                {
                    return MatrixGroupOverlap;
                }
            }
        }

        return null;
    }

    /// <summary>Historical rating amounts are not a function of catalog mapping.</summary>
    public static (decimal Buy, decimal Sell, string Currency) HistoricalRating(
        string billNo,
        decimal buy,
        decimal sell,
        string currency) => (buy, sell, currency);
}

public readonly record struct ExplicitRuleFinance(
    string? ChargeCode,
    string? LineRole,
    string? CostTypeCode,
    string? RevenueTypeCode,
    string? RuleCode = null,
    bool Matrix = false);

public readonly record struct MatrixGroupScope(
    string? RouteKey,
    bool AppliesAnyRoute,
    string? TransportMode,
    string? CarrierKey,
    IReadOnlyList<string> CommodityCodes);
