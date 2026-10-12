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
    string CurrencyCode);

public sealed record RateSuggestionVersion(
    Guid Id,
    Guid RateCardId,
    int VersionNo,
    string Status,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo);

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
        string? billRoute,
        string? billMode,
        DateTimeOffset rateDate)
    {
        var versionList = versions.ToList();
        var published = versionList.Where(v =>
            string.Equals(v.Status, "published", StringComparison.OrdinalIgnoreCase)).ToList();
        var options = new List<BillRateOptionDto>();
        foreach (var card in cards)
        {
            foreach (var version in published.Where(v => v.RateCardId == card.Id).OrderByDescending(v => v.VersionNo))
            {
                var reason = BlockReason(card, version, billRoute, billMode, rateDate);
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
        string? billRoute,
        string? billMode,
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

        if (!string.IsNullOrWhiteSpace(card.RouteCode) && string.IsNullOrWhiteSpace(billRoute))
        {
            return BillRateNotices.MissingBillNotice;
        }

        if (!string.IsNullOrWhiteSpace(card.TransportMode) && string.IsNullOrWhiteSpace(billMode))
        {
            return BillRateNotices.MissingBillNotice;
        }

        if (!string.IsNullOrWhiteSpace(card.RouteCode)
            && !string.Equals(card.RouteCode.Trim(), billRoute?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return BillRateNotices.RouteMismatch;
        }

        if (!string.IsNullOrWhiteSpace(card.TransportMode)
            && !string.Equals(card.TransportMode.Trim(), billMode?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return BillRateNotices.ModeMismatch;
        }

        return null;
    }
}
