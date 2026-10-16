namespace LCMS.Application.Ratings;

public static class BillRateNotices
{
    public const string HasPublished = "Có phiên bản đã phát hành";
    public const string NoPublished = "Chưa có phiên bản đã phát hành";
    public const string ReadyForBill = "Sẵn sàng tính giá cho Bill";
    public const string NoPublishedNotice = "Không có phiên bản đã phát hành";
    public const string NoMatchNotice = "Có phiên bản đã phát hành nhưng không khớp điều kiện";
    public const string MissingBillNotice = "Thiếu dữ liệu Bill để đánh giá";
    public const string Choose = "Có nhiều bảng giá phù hợp. Chọn một bảng giá trước khi tính.";
    public const string Retired = "Bảng giá đã ngừng sử dụng. Không tính giá mới.";
    public const string RetiredShort = "Bảng giá đã ngừng sử dụng";
    public const string OutsideDate = "Ngoài ngày hiệu lực";
    public const string RouteMismatch = "Không khớp tuyến";
    public const string ModeMismatch = "Không khớp phương thức";
}

public sealed record RateSuggestionCard(
    Guid Id,
    string Code,
    string Name,
    string PartyType,
    bool IsActive,
    string? RouteCode,
    string? TransportMode,
    string CurrencyCode,
    Guid? SupplierPartyId = null);

public sealed record RateSuggestionVersion(
    Guid Id,
    Guid RateCardId,
    int VersionNo,
    string Status,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    bool ExplicitChargeGroups = false);

public sealed record RateSuggestionRule(
    Guid RateVersionId,
    string Code,
    string Name,
    string? ChargeCode,
    string? ServiceTypeCode,
    string? PartyTypeCode,
    string? RouteCode,
    string? TransportMode,
    string? OriginCode,
    string? DestinationCode,
    string? CommodityCode,
    bool AppliesAnyRoute = false);

public sealed record BillRateMatchContext(
    string? RouteCode,
    string? TransportMode,
    string? ServiceTypeCode,
    string? OriginCode,
    string? DestinationCode,
    string? CommodityCode);

public sealed record BillRateOptionDto(
    Guid RateCardId,
    string Code,
    string Name,
    string CurrencyCode,
    string? TransportMode,
    Guid VersionId,
    int VersionNo,
    bool Selectable,
    string? Reason);

public sealed record BillRateSideDto(
    string PublicationLabel,
    string? ReadyLabel,
    string Notice,
    bool Ambiguous,
    Guid? SelectedVersionId,
    IReadOnlyList<BillRateOptionDto> Options);

/// <summary>
/// Derived rating readiness. Not stored on the rate card.
/// </summary>
public static class BillRateSuggestion
{
    public static BillRateSideDto Classify(
        IEnumerable<RateSuggestionCard> cards,
        IEnumerable<RateSuggestionVersion> versions,
        IEnumerable<RateSuggestionRule> rules,
        BillRateMatchContext bill,
        DateTimeOffset rateDate)
    {
        var ruleList = rules.ToList();
        var versionList = versions.ToList();
        var published = versionList.Where(v =>
            string.Equals(v.Status, "published", StringComparison.OrdinalIgnoreCase)).ToList();
        var options = new List<BillRateOptionDto>();
        foreach (var card in cards)
        {
            foreach (var version in published.Where(v => v.RateCardId == card.Id).OrderByDescending(v => v.VersionNo))
            {
                var versionRules = ruleList.Where(r => r.RateVersionId == version.Id).ToList();
                var reason = BlockReason(card, version, versionRules, bill, rateDate);
                options.Add(new BillRateOptionDto(
                    card.Id,
                    card.Code,
                    card.Name,
                    card.CurrencyCode,
                    card.TransportMode,
                    version.Id,
                    version.VersionNo,
                    reason is null,
                    reason));
            }
        }

        var selectable = options.Where(o => o.Selectable).ToList();
        var hasPublished = published.Any(v => cards.Any(c => c.Id == v.RateCardId));
        string notice;
        string? ready = null;
        Guid? selected = null;
        var ambiguous = selectable.Count > 1;
        if (!hasPublished)
        {
            notice = BillRateNotices.NoPublishedNotice;
        }
        else if (selectable.Count == 1)
        {
            notice = "";
            ready = BillRateNotices.ReadyForBill;
            selected = selectable[0].VersionId;
        }
        else if (selectable.Count > 1)
        {
            notice = BillRateNotices.Choose;
        }
        else
        {
            var reasons = options.Select(o => o.Reason).Where(r => !string.IsNullOrWhiteSpace(r)).Distinct().ToList();
            if (reasons.Count > 0 && reasons.All(r => r == BillRateNotices.MissingBillNotice))
            {
                notice = BillRateNotices.MissingBillNotice;
            }
            else
            {
                var detail = string.Join("; ", reasons);
                notice = string.IsNullOrWhiteSpace(detail)
                    ? BillRateNotices.NoMatchNotice
                    : $"{BillRateNotices.NoMatchNotice}: {detail}";
            }
        }

        return new BillRateSideDto(
            hasPublished ? BillRateNotices.HasPublished : BillRateNotices.NoPublished,
            ready,
            notice,
            ambiguous,
            selected,
            options);
    }

    public static string? BlockReason(
        RateSuggestionCard card,
        RateSuggestionVersion version,
        IReadOnlyList<RateSuggestionRule> rules,
        BillRateMatchContext bill,
        DateTimeOffset rateDate)
    {
        if (!card.IsActive)
        {
            return BillRateNotices.RetiredShort;
        }

        if (version.EffectiveFrom is DateTimeOffset from && rateDate < from)
        {
            return BillRateNotices.OutsideDate;
        }

        if (version.EffectiveTo is DateTimeOffset to && rateDate > to)
        {
            return BillRateNotices.OutsideDate;
        }

        var rulesCarryRoute = rules.Any(r => r.AppliesAnyRoute || !string.IsNullOrWhiteSpace(r.RouteCode));
        var rulesCarryMode = rules.Any(r => !string.IsNullOrWhiteSpace(r.TransportMode));
        if (!rulesCarryRoute && !string.IsNullOrWhiteSpace(card.RouteCode) && string.IsNullOrWhiteSpace(bill.RouteCode))
        {
            return BillRateNotices.MissingBillNotice;
        }

        if (!rulesCarryMode && !string.IsNullOrWhiteSpace(card.TransportMode) && string.IsNullOrWhiteSpace(bill.TransportMode))
        {
            return BillRateNotices.MissingBillNotice;
        }

        if (!rulesCarryRoute
            && !string.IsNullOrWhiteSpace(card.RouteCode)
            && !string.Equals(card.RouteCode.Trim(), bill.RouteCode?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return BillRateNotices.RouteMismatch;
        }

        if (!rulesCarryMode
            && !string.IsNullOrWhiteSpace(card.TransportMode)
            && !LCMS.Domain.Pricing.RateFieldPolicy.SameMode(card.TransportMode, bill.TransportMode))
        {
            return BillRateNotices.ModeMismatch;
        }

        return RuleReason(card.PartyType, rules, bill);
    }

    public static string? RuleReason(string partyType, IReadOnlyList<RateSuggestionRule> rules, BillRateMatchContext bill)
    {
        if (rules.Count == 0)
        {
            return "Không có quy tắc giá phù hợp";
        }

        if (rules.Any(r => Matches(r, partyType, bill)))
        {
            return null;
        }

        var viable = rules.Where(r => !HasHardConflict(r, partyType, bill)).ToList();
        if (viable.Count > 0 && viable.All(r => HasMissingBillField(r, bill)))
        {
            return BillRateNotices.MissingBillNotice;
        }

        if (rules.Any(r => Hard(r.RouteCode, bill.RouteCode)))
        {
            return BillRateNotices.RouteMismatch;
        }

        if (rules.Any(r => Hard(r.TransportMode, bill.TransportMode)))
        {
            return BillRateNotices.ModeMismatch;
        }

        return "Không khớp điều kiện";
    }

    private static bool Matches(RateSuggestionRule rule, string partyType, BillRateMatchContext bill)
    {
        var probe = new LCMS.Domain.Entities.PricingRule
        {
            ServiceTypeCode = rule.ServiceTypeCode,
            PartyTypeCode = rule.PartyTypeCode,
            RouteCode = rule.RouteCode,
            TransportMode = rule.TransportMode,
            OriginCode = rule.OriginCode,
            DestinationCode = rule.DestinationCode,
            CommodityCode = rule.CommodityCode
        };
        return RatingEngine.Matches(
            probe,
            bill.ServiceTypeCode,
            partyType,
            bill.RouteCode,
            bill.TransportMode,
            bill.OriginCode,
            bill.DestinationCode,
            bill.CommodityCode);
    }

    private static bool HasHardConflict(RateSuggestionRule rule, string partyType, BillRateMatchContext bill) =>
        Hard(rule.ServiceTypeCode, bill.ServiceTypeCode)
        || Hard(rule.PartyTypeCode, partyType)
        || Hard(rule.RouteCode, bill.RouteCode)
        || Hard(rule.TransportMode, bill.TransportMode)
        || Hard(rule.OriginCode, bill.OriginCode)
        || Hard(rule.DestinationCode, bill.DestinationCode)
        || Hard(rule.CommodityCode, bill.CommodityCode);

    private static bool HasMissingBillField(RateSuggestionRule rule, BillRateMatchContext bill) =>
        Missing(rule.ServiceTypeCode, bill.ServiceTypeCode)
        || Missing(rule.RouteCode, bill.RouteCode)
        || Missing(rule.TransportMode, bill.TransportMode)
        || Missing(rule.OriginCode, bill.OriginCode)
        || Missing(rule.DestinationCode, bill.DestinationCode)
        || Missing(rule.CommodityCode, bill.CommodityCode);

    private static bool Hard(string? filter, string? value) =>
        !string.IsNullOrWhiteSpace(filter)
        && !string.IsNullOrWhiteSpace(value)
        && !string.Equals(filter.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool Missing(string? filter, string? value) =>
        !string.IsNullOrWhiteSpace(filter) && string.IsNullOrWhiteSpace(value);
}
