namespace LCMS.Domain.Terminology;

/// <summary>
/// CP6.5 Vietnamese UX terminology contract (Sprint 0 / E16 foundation).
/// <para>
/// Rule: UI must display Vietnamese UI terms from this dictionary — never raw English enums,
/// Canonical English labels, or internal CodeKeys. Code/API/DB may use CodeKeys;
/// user-facing labels always resolve through this map.
/// </para>
/// Mapping principle: Vietnamese UI Term ← Canonical English ← CodeKey.
/// </summary>
public static class VietnameseUiTerms
{
    /// <summary>
    /// CodeKey → Vietnamese UI term for core entities and maturity labels already in the system.
    /// </summary>
    public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // Core entities
        ["TENANT"] = "Thuê bao",
        ["BILL"] = "Bill",
        ["COST"] = "Chi phí",
        ["REVENUE"] = "Doanh thu",
        ["ORGANIZATION"] = "Tổ chức",
        ["USER"] = "Người dùng",
        ["BUSINESS_PARTY"] = "Đối tác kinh doanh",

        // Financial maturity (Expected / Confirmed / Actual — never overwrite layers)
        ["EXPECTED"] = "Dự kiến",
        ["CONFIRMED"] = "Đã xác nhận",
        ["ACTUAL"] = "Thực tế",
        ["EXPECTED_COST"] = "Chi phí dự kiến",
        ["CONFIRMED_COST"] = "Chi phí đã xác nhận",
        ["ACTUAL_COST"] = "Chi phí thực tế",
        ["EXPECTED_REVENUE"] = "Doanh thu dự kiến",
        ["CONFIRMED_REVENUE"] = "Doanh thu đã xác nhận",
        ["ACTUAL_REVENUE"] = "Doanh thu thực tế",

        // CP6.5 examples locked in TD6 (foundation subset)
        ["COST_ALLOCATION"] = "Phân bổ chi phí",
        ["AP"] = "Khoản phải trả",
        ["AR"] = "Khoản phải thu",
        ["RECONCILIATION"] = "Đối soát",
        ["VARIANCE"] = "Chênh lệch",
        ["EXCEPTION"] = "Ngoại lệ",
        ["FINANCIAL_CLOSE"] = "Chốt tài chính",
        ["REOPEN_FINANCIAL_CLOSE"] = "Mở lại chốt tài chính"
    };

    public static string Get(string codeKey)
    {
        if (All.TryGetValue(codeKey, out var term))
        {
            return term;
        }

        throw new KeyNotFoundException($"Không tìm thấy thuật ngữ UI cho mã '{codeKey}'.");
    }
}
