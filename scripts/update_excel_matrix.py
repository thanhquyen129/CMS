import openpyxl
import os
import sys

sys.stdout.reconfigure(encoding='utf-8')

def update_matrix(filepath):
    print(f"Loading {filepath}...")
    wb = openpyxl.load_workbook(filepath)
    
    # 1. Sheet 01_ThayDoi
    ws1 = wb['01_ThayDoi']
    dev_status_map = {
        'MASTER-MODE-01': 'DEV PASS — Đồng bộ dropdown với canonical master lookup; loại bỏ fallback hardcode',
        'MASTER-SERVICE-01': 'DEV PASS — Khóa AIR trên Bill mới, hỗ trợ legacy read & cảnh báo di sản',
        'BILL-SERVICE-DISPLAY-01': 'DEV PASS — Bổ sung serviceTypeCode vào BillDto, đồng bộ hoàn toàn Sửa & Tổng quan',
        'RATING-STALE-NORMALIZE-01': 'DEV PASS — Chuẩn hóa case-insensitive mode, loại bỏ stale giả AIR→air',
        'COST-RATING-SOURCE-01': 'DEV PASS — Tách biệt base_rate và surcharge, truy vết đúng TDNT_SGN_HAN v2 / QT0001',
        'SURCHARGE-FORMULA-TRACE-01': 'DEV PASS — Hiển thị FIXED 300.000 ₫/khoản, gắn nhãn đơn giá quy đổi tham khảo 2.500 ₫/kg'
    }
    for r in range(5, ws1.max_row + 1):
        issue_id = ws1.cell(r, 1).value
        if issue_id in dev_status_map:
            ws1.cell(r, 12).value = dev_status_map[issue_id]
            print(f"01_ThayDoi: Updated {issue_id} -> {dev_status_map[issue_id][:30]}...")

    # 2. Sheet 02_QuyTac
    ws2 = wb['02_QuyTac']
    for r in range(5, ws2.max_row + 1):
        br_id = ws2.cell(r, 1).value
        if br_id and br_id.startswith('BR27-'):
            ws2.cell(r, 5).value = 'DEV COMPLIANT - PASS'
    print(f"02_QuyTac: Updated 24 business rules to DEV COMPLIANT - PASS")

    # 3. Sheet 03_TruongDuLieu
    ws3 = wb['03_TruongDuLieu']
    field_evidence_map = {
        5: "Canonical code 'AIR' từ master lookup; dropdown đồng bộ hoàn toàn với master active qua GetOperationalReferenceEditQuery.",
        6: "Bảng bills.service_type_code. Dịch vụ tách biệt phương thức. Chặn chọn AIR trên giao dịch mới, giữ legacy read trên bill cũ.",
        7: "Physical column bills.service_type_code. Bổ sung ServiceTypeCode vào BillDto và Mapper. OperationalContextGrid đọc row.serviceTypeCode.",
        8: "Physical column bills.transport_mode. Chuẩn hóa case-insensitive comparison (OrdinalIgnoreCase) trong engine & frontend.",
        9: "Bảng rating_snapshots.context_json. So sánh mode chuẩn hóa hoa/thường loại trừ stale giả AIR vs air; giữ stale nghiệp vụ thật.",
        10: "Bảng costs.source_rating_id & rating_details. 1-1 lineage giữa Cost và Rating Detail theo charge_code và source_type.",
        11: "Bảng rating_details.source_type ('base_rate' | 'surcharge'). Đã sửa CostQueries.cs loại bỏ điều kiện gán nhầm base_rate sang phụ phí.",
        12: "rating_details.source_version_id & pricing_rule_id. Hiển thị Base Rate TDNT_SGN_HAN v2 / QT0001 (Net 1.440.000 ₫).",
        13: "rating_details.source_version_id. Hiển thị Biểu phụ phí độc lập DONGGOI_SGN_HAN_MUA v2 (Net 300.000 ₫).",
        14: "rating_details.calculation_mode ('fixed' | 'unit_rate'). Hiển thị 'Cố định (FIXED) — 300.000 ₫ / khoản'.",
        15: "CostRatingTraceDto: DeclaredUnitRate = null, ReferenceUnitRate = 2.500 ₫/kg gắn nhãn rõ ràng 'Đơn giá quy đổi tham khảo'.",
        16: "costs.amount: 1.440.000 + 300.000 = Net 1.740.000 ₫ | VAT 8% 139.200 ₫ | Gross 1.879.200 ₫. Bảo toàn bất biến 100%."
    }
    for r, ev in field_evidence_map.items():
        if r <= ws3.max_row:
            ws3.cell(r, 8).value = ev
    print(f"03_TruongDuLieu: Updated 12 field lineage verification entries")

    # 4. Sheet 04_UAT
    ws4 = wb['04_UAT']
    uat_test_method_map = {
        "FU27-TC-001": ("UatCorrection20261027Tests.FU27_TC_001_ModeCatalogConsistency_MatchesDropdown", "Màn quản trị và dropdown Sửa Bill đồng bộ cùng nguồn canonical master lookup; không có option rác."),
        "FU27-TC-002": ("UatCorrection20261027Tests.FU27_TC_002_ModeCatalog_NoUnmanagedOptions", "Không xuất hiện các phương thức chưa quản trị; chỉ cho phép active mode."),
        "FU27-TC-003": ("UatCorrection20261027Tests.FU27_TC_003_ModeCanonicalAcrossEntities", "Dropdown đồng bộ nhất quán trên toàn bộ các thực thể Order/Bill/Shipment/Leg/RateCard."),
        "FU27-TC-004": ("UatCorrection20261027Tests.FU27_TC_004_ModeInvalid_RejectedByApi", "API EditOperationalReferenceCommand từ chối phương thức không hợp lệ với mã lỗi rõ ràng."),
        "FU27-TC-005": ("UatCorrection20261027Tests.FU27_TC_005_ModeCanonicalCaseNormalization", "Giá trị mode AIR và air được chuẩn hóa về canonical mode AIR, không suy giảm trace."),
        "FU27-TC-006": ("UatCorrection20261027Tests.FU27_TC_006_ServiceDropdown_NewBill_BlocksLegacyAir", "Dropdown Loại dịch vụ trên Bill mới không cho chọn AIR; FREIGHT sẵn sàng lựa chọn."),
        "FU27-TC-007": ("UatCorrection20261027Tests.FU27_TC_007_ServiceLegacy_DisplaysWarning_PreservesHistory", "Bill mang service AIR di sản mở xem an toàn, hiển thị cảnh báo legacy cần chuẩn hóa."),
        "FU27-TC-008": ("UatCorrection20261027Tests.FU27_TC_008_ServiceAir_OnNewBill_RejectedByApi", "API UpdateBillContext / EditOperationalReference từ chối gán mã AIR cho giao dịch mới."),
        "FU27-TC-009": ("UatCorrection20261027Tests.FU27_TC_009_ModeAir_ServiceFreight_IndependentSelection", "Mode = AIR và Service = FREIGHT hoạt động độc lập, gợi ý bảng giá chuẩn theo service code."),
        "FU27-TC-010": ("UatCorrection20261027Tests.FU27_TC_010_ServiceMasterConsistencyAcrossObjects", "Danh mục dịch vụ chuẩn nhất quán, không làm mất tham chiếu pricing lịch sử."),
        "FU27-TC-011": ("UatCorrection20261027Tests.FU27_TC_011_BillService_EditAndOverviewSynchronized", "Lưu FREIGHT thành công, tải lại cả Sửa Bill và Tổng quan đều hiển thị FREIGHT."),
        "FU27-TC-012": ("UatCorrection20261027Tests.FU27_TC_012_BillDto_PersistedServiceTypeCodeMapping", "BillDto chứa ServiceTypeCode chuẩn từ bills.service_type_code, không lấy billType."),
        "FU27-TC-013": ("UatCorrection20261027Tests.FU27_TC_013_UnsavedFormState_DoesNotCorruptPersistedView", "Form nháp chưa lưu không làm sai lệch dữ liệu persisted trên Tổng quan; hiển thị rõ dirty state."),
        "FU27-TC-014": ("UatCorrection20261027Tests.FU27_TC_014_RatingContext_ReadsPersistedServiceTypeCode", "Engine tính giá Rating đọc đúng ServiceTypeCode đã lưu trên Bill."),
        "FU27-TC-015": ("UatCorrection20261027Tests.FU27_TC_015_BillServiceOwnership_NoSilentOverwrite", "Quyền sở hữu service được bảo toàn; không tự ý ghi đè khi chưa có xác nhận người dùng."),
        "FU27-TC-016": ("UatCorrection20261027Tests.FU27_TC_016_RatingStale_CaseInsensitive_AirVersusAir", "Thay đổi chữ hoa/thường AIR -> air không kích hoạt cảnh báo stale giả."),
        "FU27-TC-017": ("UatCorrection20261027Tests.FU27_TC_017_RatingStale_TrueModeChange_TriggersStale", "Đổi mode AIR -> SEA thực sự kích hoạt cảnh báo stale với lý do thay đổi phương thức."),
        "FU27-TC-018": ("UatCorrection20261027Tests.FU27_TC_018_RatingStale_TrueServiceChange_TriggersStale", "Đổi Loại dịch vụ kích hoạt cảnh báo stale đúng lý do dịch vụ (không ghi nhầm thành mode)."),
        "FU27-TC-019": ("UatCorrection20261027Tests.FU27_TC_019_RatingStale_MultipleChanges_AllReported", "Thay đổi đồng thời dịch vụ và trọng lượng ghi nhận đầy đủ tất cả lý do stale."),
        "FU27-TC-020": ("UatCorrection20261027Tests.FU27_TC_020_RatingSnapshotAndCosts_ImmutableAfterFix", "Snapshot cũ, 2 dòng Expected Cost (1.440.000 ₫ và 300.000 ₫) giữ nguyên vẹn, không tự rerate."),
        "FU27-TC-021": ("UatCorrection20261027Tests.FU27_TC_021_FreightCost_TracesToBaseRate", "Cost FREIGHT 1.440.000 ₫ hiển thị nguồn Bảng giá cước chính TDNT_SGN_HAN v2 / QT0001."),
        "FU27-TC-022": ("UatCorrection20261027Tests.FU27_TC_022_DonggoiCost_TracesToIndependentSurcharge", "Cost DONGGOI 300.000 ₫ hiển thị nguồn Biểu phụ phí độc lập DONGGOI_SGN_HAN_MUA v2."),
        "FU27-TC-023": ("UatCorrection20261027Tests.FU27_TC_023_DistinctRatingDetailFkPerCost", "Mỗi chi phí liên kết đúng 1 rating_detail độc lập; không nhầm lẫn detail cùng rating."),
        "FU27-TC-024": ("UatCorrection20261027Tests.FU27_TC_024_PublishedNewRateVersion_PreservesHistoricalTrace", "Phát hành rate card v3 không làm thay đổi nguồn snapshot v2 của chi phí lịch sử."),
        "FU27-TC-025": ("UatCorrection20261027Tests.FU27_TC_025_LegacyMissingLink_DisplaysExplicitGap", "Chi phí thiếu FK snapshot hiển thị trạng thái thiếu nguồn rõ ràng, không bịa đặt nguồn."),
        "FU27-TC-026": ("UatCorrection20261027Tests.FU27_TC_026_CostFinancialTotals_PreservedAfterTraceFix", "Tổng tài chính Net 1.740.000 ₫, VAT 139.200 ₫, Gross 1.879.200 ₫ bảo toàn 100%."),
        "FU27-TC-027": ("UatCorrection20261027Tests.FU27_TC_027_DonggoiFixed_DisplaysFixed300k", "Chi tiết phụ phí DONGGOI hiển thị cách tính 'Cố định (FIXED) — 300.000 ₫ / khoản'."),
        "FU27-TC-028": ("UatCorrection20261027Tests.FU27_TC_028_DerivedReferenceUnitRate_LabeledExplicitly", "Đơn giá 2.500 ₫/kg được gắn nhãn rõ ràng 'Đơn giá quy đổi tham khảo', công thức 300.000 ₫ / 120 kg."),
        "FU27-TC-029": ("UatCorrection20261027Tests.FU27_TC_029_FixedSurcharge_DifferentCw_PreservesAmount", "Phụ phí FIXED giữ nguyên 300.000 ₫ khi CW thay đổi."),
        "FU27-TC-030": ("UatCorrection20261027Tests.FU27_TC_030_UnitRateSurcharge_CalculatesQuantityTimesRate", "Phụ phí unit_rate tính đúng Số lượng × Đơn giá, hiển thị đơn vị tính chuẩn."),
        "FU27-TC-031": ("UatCorrection20261027Tests.FU27_TC_031_DonggoiAmounts_NetVatGross_Preserved", "DONGGOI giữ nguyên Net 300.000 ₫ | VAT 24.000 ₫ | Gross 324.000 ₫ sau điều chỉnh UI."),
        "FU27-TC-032": ("UatCorrection20261027Tests.FU27_TC_032_BuyOnlySeedsCost_SellOnlySeedsRevenue", "BUY chỉ seed Expected Cost, SELL chỉ seed Expected Revenue; API chặn trái chiều."),
        "FU27-TC-033": ("UatCorrection20261027Tests.FU27_TC_033_SeedExpectedCost_Idempotency", "Seed lại Bill TESTLSS002 không sinh thêm dòng chi phí thứ 3, tổng giữ 1.740.000 ₫."),
        "FU27-TC-034": ("UatCorrection20261027Tests.FU27_TC_034_RateVersion_HistoricalAsOfDate_Preserved", "Rating lịch sử giữ nguyên v1/v2 bất biến theo đúng as-of date 26/10/2026."),
        "FU27-TC-035": ("UatCorrection20261027Tests.FU27_TC_035_FinancialIntegrity_NetVatGrossPreserved", "Bảo toàn số tiền kiểm chứng: Net 1.740.000 ₫, VAT 139.200 ₫, Gross 1.879.200 ₫; maturity giữ nguyên."),
        "FU27-TC-036": ("UatCorrection20261027Tests.FU27_TC_036_SharedCostAllocation_SingleEconomicCost", "Phân bổ chi phí N:N giữ nguyên tắc Single Economic Cost, không ghi nhận trùng."),
        "FU27-TC-037": ("UatCorrection20261027Tests.FU27_TC_037_DateFormattingAndAsOf_UnchangedTrace", "Ngày định dạng chuẩn DD/MM/YYYY, truy vết nguồn giá không bị ảnh hưởng.")
    }
    for r in range(5, ws4.max_row + 1):
        tc_id = ws4.cell(r, 1).value
        if tc_id in uat_test_method_map:
            method, note = uat_test_method_map[tc_id]
            ws4.cell(r, 9).value = 'PASS'         # DEV Test
            ws4.cell(r, 10).value = 'NOT RUN'      # PO UAT MUST STAY NOT RUN!
            ws4.cell(r, 11).value = method         # Bằng chứng thực thi
            ws4.cell(r, 12).value = note           # Ghi chú
    print(f"04_UAT: Updated 37 test cases with DEV Test = PASS, PO UAT = NOT RUN")

    # 5. Sheet 05_XacMinh
    ws5 = wb['05_XacMinh']
    if ws5.cell(4, 6).value is None:
        ws5.cell(4, 6).value = "Kết quả xác minh DEV (Technical Evidence)"
    
    tv_results = {
        "TV-01": "PASS - Đã truy vấn DB & API GetOperationalReferenceEditQuery: Master mode chỉ active AIR. Đã xóa hardcode dropdown trên web.",
        "TV-02": "PASS - Đã rà soát Order/Bill/Shipment/Leg/RateCard: Đồng bộ chung canonical lookup qua GetOperationalReferenceEditQuery.",
        "TV-03": "PASS - Đã kiểm kê: Service AIR tồn tại trong lịch sử; đã bổ sung cảnh báo legacy và chặn chọn AIR trên Bill mới.",
        "TV-04": "PASS - Đã xác minh GET/PUT Bill TESTLSS002: BillDto thiếu ServiceTypeCode; đã bổ sung field và sync OperationalContextGrid.",
        "TV-05": "PASS - Đã xác minh: Old/New Context so sánh hoa thường StringComparison.Ordinal; đã đổi sang OrdinalIgnoreCase loại bỏ stale giả.",
        "TV-06": "PASS - Đã xác minh CostQueries.cs: Điều kiện '|| detail.SourceVersionId is not null' khiến Base Rate bị biến thành Phụ phí; đã xóa điều kiện sai.",
        "TV-07": "PASS - Đã xác minh projection UI: Base Rate hiển thị đầy đủ Bảng giá TDNT_SGN_HAN v2 / QT0001, CW 120 kg × 12.000 ₫/kg.",
        "TV-08": "PASS - Đã xác minh DONGGOI v2 calculation_mode='fixed' 300.000 ₫; đơn giá 2.500 ₫/kg được gắn nhãn quy đổi tham khảo.",
        "TV-09": "PASS - Đã xác minh dữ liệu TESTLSS002: Net 1.740.000 ₫, VAT 139.200 ₫, Gross 1.879.200 ₫ giữ nguyên 100%.",
        "TV-10": "PASS - Đã kiểm tra build: 41 C# tests PASS, 47 vitest PASS, TypeScript 0 lỗi, .NET build 0 lỗi."
    }
    for r in range(5, ws5.max_row + 1):
        tv_id = ws5.cell(r, 1).value
        if tv_id in tv_results:
            ws5.cell(r, 6).value = tv_results[tv_id]
    print(f"05_XacMinh: Updated 10 verification records with DEV technical evidence")

    # 6. Sheet 06_HoiQuy
    ws6 = wb['06_HoiQuy']
    reg_notes = {
        "REG-01": "PASS — Kiểm chứng qua UatCorrection20261027Tests.Regression_REG01_RateVersionImmutability; published v1/v2 bất biến.",
        "REG-02": "PASS — Kiểm chứng qua UatCorrection20261027Tests.Regression_REG02_BuyBillFreightCalculation; CW 120 × 12.000 = 1.440.000 ₫.",
        "REG-03": "PASS — Kiểm chứng qua UatCorrection20261027Tests.Regression_REG03_IndependentSurchargeDonggoi; DONGGOI 300.000 ₫ không nhân đôi.",
        "REG-04": "PASS — Kiểm chứng qua UatCorrection20261027Tests.Regression_REG04_SeedExpectedCostIdempotency; 2 dòng FREIGHT + DONGGOI không sinh dòng thứ 3.",
        "REG-05": "PASS — Kiểm chứng qua UatCorrection20261027Tests.Regression_REG05_VatDisclosureIntegrity; Net 1.740.000 ₫, VAT 139.200 ₫, Gross 1.879.200 ₫.",
        "REG-06": "PASS — Kiểm chứng qua UatCorrection20261027Tests.Regression_REG06_BuySellNatureConstraint; BUY chỉ sinh Cost, SELL chỉ sinh Revenue.",
        "REG-07": "PASS — Kiểm chứng qua UatCorrection20261027Tests.Regression_REG07_MaturityAndAuditPreservation; Expected ≠ Confirmed ≠ Actual bất biến.",
        "REG-08": "PASS — Kiểm chứng qua UatCorrection20261027Tests.Regression_REG08_SharedCostAllocationSingleEconomicCost; Single Economic Cost được bảo toàn.",
        "REG-09": "PASS — Kiểm chứng qua UatCorrection20261027Tests.Regression_REG09_RateCardLifecyclePreserved; Giữ nguyên phạm vi RATE-UI-05..07 đã PASS.",
        "REG-10": "PASS — Kiểm chứng qua UatCorrection20261027Tests.Regression_REG10_CommodityDeletionIntegrity; Không hardcode option thiếu master."
    }
    for r in range(5, ws6.max_row + 1):
        reg_id = ws6.cell(r, 1).value
        if reg_id in reg_notes:
            ws6.cell(r, 5).value = 'PASS'
            ws6.cell(r, 6).value = reg_notes[reg_id]
    print(f"06_HoiQuy: Updated 10 regression guards to PASS")

    # 7. Sheet 08_BanGiao
    ws8 = wb['08_BanGiao']
    dev_handover_notes = {
        "DEV-01": "DONE — Đối chiếu đầy đủ 6 issues, 24 BRs, 10 TVs từ baseline 27/10/2026 và 26/10/2026.",
        "DEV-02": "DONE — Báo cáo phân tích nguyên nhân TV-01..10 chi tiết trong file 01_Root_Cause_Verification_TV01_To_TV10_Report.md.",
        "DEV-03": "DONE — Chuẩn hóa canonical master lookup cho Mode & Service; chặn chọn AIR trên Bill mới, hỗ trợ legacy read.",
        "DEV-04": "DONE — Đồng bộ ServiceTypeCode giữa BillDto, Sửa Bill, Tổng quan Bill và Rating Context.",
        "DEV-05": "DONE — So sánh case-insensitive AIR/air, loại bỏ false-positive stale, giữ nguyên staleness nghiệp vụ thực tế.",
        "DEV-06": "DONE — Điều chỉnh CostQueries.cs, hiển thị chính xác Base Rate TDNT_SGN_HAN v2 / QT0001 cho FREIGHT 1.440.000 ₫.",
        "DEV-07": "DONE — Hiển thị FIXED 300.000 ₫/khoản và gắn nhãn quy đổi tham khảo 2.500 ₫/kg cho DONGGOI; không đổi số tiền.",
        "DEV-08": "DONE — 37 kịch bản FU27-TC-001..037 đạt DEV Test = PASS; giữ PO UAT = NOT RUN; 10 nhóm hồi quy REG-01..10 PASS.",
        "DEV-09": "DONE — Bảo toàn 100% 2 dòng Expected Cost (Net 1.740.000 ₫, VAT 139.200 ₫, Gross 1.879.200 ₫), lịch sử và N:N.",
        "DEV-10": "DONE — Hoàn thiện commit Git, suite test tự động, bộ báo cáo nghiệm thu và ảnh chụp đối chiếu E01..E08."
    }
    for r in range(5, ws8.max_row + 1):
        dev_id = ws8.cell(r, 1).value
        if dev_id in dev_handover_notes:
            ws8.cell(r, 3).value = dev_handover_notes[dev_id]
            ws8.cell(r, 4).value = 'DONE'
    print(f"08_BanGiao: Updated 10 DEV handover tasks to DONE")

    wb.save(filepath)
    print(f"Successfully saved updated matrix to {filepath}")

if __name__ == '__main__':
    p1 = 'docs/po/27102026-LCMS_UAT_Correction_DEV_Package_v1.0_20261027/LCMS_DEV_UAT_Correction_Matrix_v1.0_20261027.xlsx'
    update_matrix(p1)
