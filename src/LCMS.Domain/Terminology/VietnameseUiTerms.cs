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
        ["RATE_CARD"] = "Bảng giá",
        ["RATE_VERSION"] = "Phiên bản bảng giá",
        ["PRICING_RULE"] = "Quy tắc tính giá",
        ["PRICING_RULE_COMPONENT"] = "Thành phần giá",
        ["RATING"] = "Lần tính giá",
        ["RATING_DETAIL"] = "Chi tiết tính giá",
        ["RATE_PRICING"] = "Bảng giá & Tính giá",

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
        ["COST_ALLOCATION_DETAIL"] = "Chi tiết phân bổ chi phí",
        ["COST_ADJUSTMENT"] = "Điều chỉnh chi phí",
        ["REVENUE_ADJUSTMENT"] = "Điều chỉnh doanh thu",
        ["DIRECT_COST"] = "Chi phí trực tiếp",
        ["SHARED_COST"] = "Chi phí chung",
        ["ATTRIBUTION_DIRECT"] = "Trực tiếp",
        ["ATTRIBUTION_SHARED"] = "Chung",
        ["BILL_FINANCIAL_PROFILE"] = "Hồ sơ tài chính Bill",
        ["BEST_AVAILABLE"] = "Giá trị tốt nhất hiện có",
        ["PROFIT"] = "Lợi nhuận",
        ["AP"] = "Khoản phải trả",
        ["AR"] = "Khoản phải thu",
        ["RECONCILIATION"] = "Đối soát",
        ["VARIANCE"] = "Chênh lệch",
        ["EXCEPTION"] = "Ngoại lệ",
        ["FINANCIAL_CLOSE"] = "Chốt tài chính",
        ["REOPEN_FINANCIAL_CLOSE"] = "Mở lại chốt tài chính",

        // Financial documents (D07 / E08) — Received ≠ Accepted ≠ Matched
        ["FINANCIAL_DOCUMENT"] = "Chứng từ tài chính",
        ["FINANCIAL_DOCUMENT_LINE"] = "Dòng chứng từ",
        ["DOCUMENT_MATCH"] = "Khớp chứng từ",
        ["DOCUMENT_MATCH_DETAIL"] = "Chi tiết khớp chứng từ",
        ["RECEIVED"] = "Đã nhận",
        ["NOT_RECEIVED"] = "Chưa nhận",
        ["ACCEPTED"] = "Đã chấp nhận",
        ["NOT_ACCEPTED"] = "Chưa chấp nhận",
        ["REJECTED"] = "Từ chối",
        ["MATCHED"] = "Đã khớp",
        ["PARTIALLY_MATCHED"] = "Khớp một phần",
        ["UNMATCHED"] = "Chưa khớp",

        // Exposure & AP/AR (D08 / E09) — Exposure ≠ Recognized ≠ Settled
        ["PAYABLE_EXPOSURE"] = "Nghĩa vụ phải trả (exposure)",
        ["RECEIVABLE_EXPOSURE"] = "Quyền thu dự kiến (exposure)",
        ["ACCOUNTS_PAYABLE"] = "Khoản phải trả",
        ["ACCOUNTS_RECEIVABLE"] = "Khoản phải thu",
        ["EXPOSURE"] = "Exposure",
        ["RECOGNITION"] = "Ghi nhận",
        ["OUTSTANDING"] = "Số dư còn lại",
        ["PARTIAL_RECOGNITION"] = "Ghi nhận một phần",
        ["SETTLEMENT_OPEN"] = "Chưa tất toán",
        ["SETTLEMENT_PARTIAL"] = "Tất toán một phần",
        ["SETTLEMENT_SETTLED"] = "Đã tất toán"
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
