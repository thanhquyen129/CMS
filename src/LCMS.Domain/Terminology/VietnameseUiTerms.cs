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
        ["PARTY_ROLE"] = "Vai trò đối tác",
        ["CURRENCY"] = "Tiền tệ",
        ["DATA_SCOPE"] = "Phạm vi dữ liệu",
        ["DATA_SCOPE_ALL"] = "Toàn bộ",
        ["DATA_SCOPE_ORGANIZATION"] = "Theo tổ chức",
        ["DATA_SCOPE_OWN"] = "Của tôi",
        ["PERMISSION"] = "Quyền hành động",
        ["ROLE"] = "Vai trò",
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
        ["ALLOCATION_BASIS_EQUAL"] = "Phân bổ đều",
        ["ALLOCATION_BASIS_QUANTITY"] = "Phân bổ theo số lượng",
        ["ALLOCATION_BASIS_MANUAL_RATIO"] = "Phân bổ theo tỷ lệ thủ công",
        ["ALLOCATION_SUPERSEDED"] = "Phân bổ đã bị thay thế",
        ["BASE_AMOUNT"] = "Số tiền quy đổi",
        ["FX_STUB_RATE"] = "Tỷ giá stub",
        ["COST_CONFIRM_APPROVAL_THRESHOLD"] = "Ngưỡng phê duyệt xác nhận chi phí",
        ["REVENUE_CONFIRM_APPROVAL_THRESHOLD"] = "Ngưỡng phê duyệt xác nhận doanh thu",
        ["BILL_FINANCIAL_PROFILE"] = "Hồ sơ tài chính Bill",
        ["BILL_PROFITABILITY"] = "Lợi nhuận theo Bill",
        ["BEST_AVAILABLE"] = "Giá trị tốt nhất hiện có",
        ["PROFIT"] = "Lợi nhuận",
        ["PROFITABILITY_VIEW"] = "Góc nhìn lợi nhuận",
        ["VARIANCE_EXPECTED_VS_ACTUAL"] = "Chênh lệch Dự kiến vs Thực tế",
        ["AP"] = "Khoản phải trả",
        ["AR"] = "Khoản phải thu",
        ["RECONCILIATION"] = "Đối soát",
        ["RECONCILIATION_DETAIL"] = "Chi tiết đối soát",
        ["VARIANCE"] = "Chênh lệch",
        ["EXCEPTION"] = "Ngoại lệ",
        ["APPROVAL"] = "Phê duyệt",
        ["APPROVAL_PENDING"] = "Chờ phê duyệt",
        ["APPROVAL_APPROVED"] = "Đã phê duyệt",
        ["APPROVAL_REJECTED"] = "Từ chối phê duyệt",
        ["EXCEPTION_OPEN"] = "Ngoại lệ mở",
        ["EXCEPTION_RESOLVED"] = "Ngoại lệ đã xử lý",
        ["EXCEPTION_CLOSED"] = "Ngoại lệ đã đóng",
        ["FINANCIAL_CLOSE"] = "Chốt tài chính",
        ["FINANCIAL_CLOSE_SNAPSHOT"] = "Bản chốt tài chính",
        ["FINANCIAL_CLOSE_SNAPSHOT_DETAIL"] = "Chi tiết bản chốt tài chính",
        ["REOPEN_FINANCIAL_CLOSE"] = "Mở lại chốt tài chính",
        ["RECLOSE_FINANCIAL_CLOSE"] = "Chốt lại tài chính",
        ["CLOSE_LOCKED"] = "Đã khóa chốt",
        ["CLOSE_REOPENED"] = "Đã mở lại chốt",
        ["FINANCIAL_CONTROL"] = "Kiểm soát tài chính",

        // Reporting / dashboard / control queues (E13)
        ["DASHBOARD"] = "Bảng điều khiển",
        ["DASHBOARD_SUMMARY"] = "Tóm tắt bảng điều khiển",
        ["CONTROL_QUEUE"] = "Hàng đợi kiểm soát",
        ["EXCEPTION_QUEUE"] = "Hàng đợi ngoại lệ",
        ["APPROVAL_QUEUE"] = "Hàng đợi phê duyệt",
        ["MATURITY_BREAKDOWN"] = "Phân tách độ chín",
        ["SETTLEMENT_OUTSTANDING"] = "Số dư tất toán còn lại",
        ["AS_OF"] = "Tại thời điểm",
        ["REPORTING_PROJECTION"] = "Projection báo cáo",

        // Financial documents (D07 / E08) — Received ≠ Accepted ≠ Matched
        ["FINANCIAL_DOCUMENT"] = "Chứng từ tài chính",
        ["FINANCIAL_DOCUMENT_LINE"] = "Dòng chứng từ",
        ["DOCUMENT_MATCH"] = "Khớp chứng từ",
        ["DOCUMENT_MATCH_DETAIL"] = "Chi tiết khớp chứng từ",
        ["MATCH_METHOD_LINE_TO_LINE"] = "Khớp dòng ↔ dòng",
        ["MATCH_METHOD_LINE_TO_COST"] = "Khớp dòng ↔ chi phí",
        ["MATCH_METHOD_LINE_TO_REVENUE"] = "Khớp dòng ↔ doanh thu",
        ["MATCH_TOLERANCE"] = "Dung sai khớp",
        ["OPEN_MATCH_AMOUNT"] = "Số tiền mở chưa khớp",
        ["DOCUMENT_DUPLICATE_CONTROL"] = "Kiểm soát chứng từ trùng",
        ["ACCEPT_BEFORE_MATCH"] = "Chấp nhận trước khi khớp",
        ["MATCH_DETAIL_REVERSED"] = "Chi tiết khớp đã đảo",
        ["DOCUMENT_CANCELLED"] = "Chứng từ đã hủy",
        ["DOCUMENT_VOIDED"] = "Chứng từ vô hiệu",
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
        ["SETTLEMENT_SETTLED"] = "Đã tất toán",

        // Settlement (D09 / E10) — Payment/Collection; allocation finalizes outstanding
        ["PAYMENT"] = "Thanh toán",
        ["COLLECTION"] = "Thu tiền",
        ["PAYMENT_ALLOCATION"] = "Phân bổ thanh toán",
        ["COLLECTION_ALLOCATION"] = "Phân bổ thu tiền",
        ["UNAPPLIED_AMOUNT"] = "Số tiền chưa phân bổ",
        ["PARTIAL_SETTLEMENT"] = "Tất toán một phần",
        ["SETTLEMENT_REVERSAL"] = "Đảo tất toán",
        ["ALLOCATION_DRAFT"] = "Nháp phân bổ",
        ["ALLOCATION_FINALIZED"] = "Đã chốt phân bổ",
        ["ALLOCATION_REVERSED"] = "Đã đảo phân bổ",

        // Hardening / Audit / Integration (D12 / E14–E16)
        ["AUDIT_EVENT"] = "Sự kiện kiểm toán",
        ["AUDIT_TRAIL"] = "Nhật ký kiểm toán",
        ["INTEGRATION_RECORD"] = "Bản ghi tích hợp",
        ["INTEGRATION_ERROR"] = "Lỗi tích hợp",
        ["IDEMPOTENCY"] = "Tính bất biến khi gửi lại",
        ["CORRELATION_ID"] = "Mã tương quan",
        ["RATE_LIMIT"] = "Giới hạn tốc độ",
        ["SECURITY_HEADER"] = "Tiêu đề bảo mật",

        // Operational reference (D03 / E03) — Bill-centric graph
        ["ORDER"] = "Đơn hàng",
        ["SHIPMENT"] = "Lô hàng",
        ["TRANSPORT_LEG"] = "Chặng vận chuyển",
        ["TRANSPORT_MOVEMENT"] = "Chuyến vận chuyển",
        ["BILL_GRAPH"] = "Đồ thị Bill",
        ["OPERATIONAL_SEARCH"] = "Tìm kiếm vận hành",
        ["ORDER_BILL_LINK"] = "Liên kết đơn hàng–Bill",
        ["BILL_SHIPMENT_LINK"] = "Liên kết Bill–lô hàng",
        ["BILL_LEG_LINK"] = "Liên kết Bill–chặng",
        ["LEG_MOVEMENT_LINK"] = "Liên kết chặng–chuyến",
        ["BILL_MOVEMENT_LINK"] = "Liên kết Bill–chuyến"
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
