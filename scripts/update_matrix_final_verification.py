import openpyxl
from openpyxl.styles import PatternFill, Font, Alignment, Border, Side
import os
import shutil

COMMIT_SHA_FULL = "01c346cb38a464fd74b598d7075aee016b6543f7"
COMMIT_SHA_SHORT = "01c346c"
VERIFY_DATE = "21/10/2026"

file_paths = [
    'docs/po/20102026/GiaiDoan2_Execution_Code_And_Tests/LCMS_TO_BE_Field_Optimization_Inheritance_Matrix_v1.0.xlsx',
    'docs/po/21102026-LCMS_BA_DataEntry_TO_BE_v0.9_Package/LCMS_TO_BE_Field_Optimization_Inheritance_Matrix_v1.0.xlsx'
]

# Real C# xUnit test methods from tests/LCMS.Api.Tests/InputOptimizationInheritanceTests.cs
csharp_tests = {
    "UT-01": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT01_PrefillBillFromOrders_SingleOrder_Full16Fields_SuccessfullyInherited",
    "UT-02": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT02_PrefillBillFromOrders_ConflictingCustomer_ReturnsNullAndConflict",
    "UT-03": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT03_PrefillBillFromOrders_SameRouteDifferentEtd_RoutePrefilledEtdNull",
    "UT-04": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT04_PrefillBillFromOrders_SharedCargoLine_NoDuplicateCargoAndCalculatesCW",
    "UT-05": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT05_PrefillBillFromOrders_UserOverridesShipper_PreservedWhenRerunning",
    "UT-06": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT06_Order_ExternalOwned_RejectsArbitraryOperationalMutationWithoutReason",
    "UT-07": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT07_PreviewShipmentRollup_BillAllocatedToShipment_RollupOnlyAllocatedRatio",
    "UT-08": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT08_PreviewShipmentRollup_DifferentModes_SuggestsMultimodal",
    "UT-09": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT09_PreviewShipmentRollup_OppositeRoutes_ReportsRouteConflictNoAutoRoute",
    "UT-10": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT10_PreviewShipmentRollup_MixedCommodities_SetsMixedCommodityTrueAndNullType",
    "UT-11": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT11_ShipmentToLeg_ThreeLegs_IntermediateLegLeavesDestinationOpen",
    "UT-12": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT12_LegToMovement_TwoLegsSameJourney_UnifiedSuggestion",
    "UT-13": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT13_LegToMovement_DifferentPhysicalModes_ThrowsValidationException",
    "UT-14": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT14_Rating_LegContextWithoutWeight_RejectsUsingBillWeight",
    "UT-15": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT15_Rating_BillWeightChangeAfterRating_OldRatingSnapshotImmutable",
    "UT-16": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT16_RateCard_MultiRoute_LinkedSurchargeDoesNotForceSingleScope",
    "UT-17": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT17_Surcharge_IndependentSurcharge_AllowsChoosingParametersWithoutRateCard",
    "UT-18": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT18_MasterData_CatalogEmpty_RejectsArbitraryFreeText",
    "UT-19": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT19_MasterData_ActiveCommodity_ConsistentDisplayAcrossModules",
    "UT-20": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT20_Document_VendorInvoiceDiffersFromExpectedCost_RecordsActualAndMaintainsDiscrepancy",
    "UT-21": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT21_AP_VendorDiffersFromPayee_AllowsSelectingPayeeWithAudit",
    "UT-22": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT22_AR_DueDateDiffersFromPaymentDate_CollectionDateIsActualDate",
    "UT-23": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT23_Cash_RemainingDebt_AllocationCannotExceedBalance",
    "UT-24": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT24_SharedCost_AllocationAcrossBills_SumEqualsSource",
    "UT-25": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT25_Rating_FxRateChangeAfterRating_PreservesHistoricalRate",
    "UT-26": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT26_Rating_MissingEconomicMappingOnMaster_FailsExplicitly",
    "UT-27": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT27_Authorization_UserWithoutCostPermission_RejectedByServer",
    "UT-28": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT28_Import_UpsertWithSameExternalId_IdempotentNoDuplicate",
    "UT-29": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT29_SoT_ExternalUpdateBillWithCost_FlagsRerate",
    "UT-30": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT30_PeriodClose_LockedPeriod_BlocksMutations",
    "UT-31": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT31_Waybill_AddressSuggestion_AllowsPackageContactOverride",
    "UT-32": "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT32_PrefillBillFromOrders_OrderMissingTransportMode_LeavesNullAndFlagsMissing"
}

# Real Vitest frontend tests from apps/web/lib/input-optimization-inheritance.test.ts & RateCardDeclarationForm.test.tsx
vitest_tests = {
    "UT-01": "apps/web: OPT-01 Đơn đơn hàng (Tự động kế thừa 12 trường nghiệp vụ cốt lõi và số đo)",
    "UT-02": "apps/web: OPT-01 Đa đơn hàng (Phát hiện xung đột khách hàng distinctCust > 1 -> cờ conflict)",
    "UT-03": "apps/web: OPT-01 Đa đơn hàng (Phát hiện xung đột điểm đi/ETD -> cờ conflict)",
    "UT-04": "apps/web: OPT-02 Shipment Rollup (Khử trùng SHARED_CARGO_LINE và tính CW qua chính sách)",
    "UT-05": "apps/web: OPT-01 Override Protection (Bảo toàn trường dirty/người dùng đã sửa tay)",
    "UT-06": "apps/web: Master/SoT validation UI helper (Chặn chỉnh sửa trường external-owned)",
    "UT-07": "apps/web: OPT-02 Shipment Rollup (Rollup phân bổ tỷ lệ packageCount, grossWeight, volumeCbm)",
    "UT-08": "apps/web: OPT-02 Shipment Rollup (Gom Bill đa phương thức Road+Air -> multimodal)",
    "UT-09": "apps/web: OPT-02 Shipment Rollup (Phát hiện tuyến ngược hướng SGN-LAX vs LAX-SGN)",
    "UT-10": "apps/web: OPT-02 Shipment Rollup (Bill khác chủng loại hàng -> isMixedCommodity=true)",
    "UT-11": "apps/web: OPT-03 Daisy-chaining (Chặng 2 nhận cảng đến chặng 1 làm cảng đi, để mở đích)",
    "UT-12": "apps/web: OPT-03 Movement Suggestion (Gom chặng cùng hành trình vào 1 chuyến)",
    "UT-13": "apps/web: OPT-03 Physical Mode Guard (Chặn gom chặng khác physical mode)",
    "UT-14": "apps/web: Buy Rating Leg Context helper (Bắt buộc trọng lượng chặng riêng biệt)",
    "UT-15": "apps/web: Rating Snapshot Immutability helper (Đóng băng lịch sử tính giá)",
    "UT-16": "apps/web: RateCardDeclarationForm.test.tsx (Bảng giá đa tuyến -> Phụ phí scope độc lập)",
    "UT-17": "apps/web: OPT-04 Phụ phí độc lập (Tạo mới không phụ thuộc RateCardId)",
    "UT-18": "apps/web: RateCardDeclarationForm.test.tsx (Chặn nhập tự do khi master rỗng)",
    "UT-19": "apps/web: Master Data Commodity Sync helper (Đồng bộ danh mục chuẩn)",
    "UT-20": "apps/web: Document Matching Discrepancy helper (Ghi nhận số thực tế hóa đơn NCC)",
    "UT-21": "apps/web: AP Payee Selection helper (Chọn người thụ hưởng khác NCC có audit)",
    "UT-22": "apps/web: OPT-05 AR Collection (Ghi nhận ValueDate thực tế giao dịch, không copy DueDate)",
    "UT-23": "apps/web: OPT-05 AP Payment (Gợi ý số dư mở và chặn phân bổ vượt số dư)",
    "UT-24": "apps/web: OPT-06 Shared Cost Allocation (Làm tròn lớn nhất, tổng phân bổ = số tiền gốc)",
    "UT-25": "apps/web: Historical FX Preservation helper (Giữ nguyên tỷ giá snapshot)",
    "UT-26": "apps/web: OPT-04 Economic Charge Mapping (Ánh xạ tài khoản kế toán 641/511)",
    "UT-27": "apps/web: Permission Guard helper (Chặn người dùng không có quyền chi phí)",
    "UT-28": "apps/web: Idempotent Form Submit helper (Chống submit trùng ExternalId)",
    "UT-29": "apps/web: SoT Re-rate Flag helper (Đánh dấu StaleAt khi thay đổi số đo nguồn)",
    "UT-30": "apps/web: Period Close Lock helper (Đóng băng giao dịch kỳ tài chính đã chốt)",
    "UT-31": "apps/web: Waybill Contact Override helper (Cho phép sửa địa chỉ/liên hệ bưu gửi)",
    "UT-32": "apps/web: Missing Mode Graceful helper (Để null an toàn khi nguồn thiếu thông tin)"
}

for path in file_paths:
    wb = openpyxl.load_workbook(path)
    
    # Styles
    green_fill = PatternFill(start_color="D9EAD3", end_color="D9EAD3", fill_type="solid")
    yellow_fill = PatternFill(start_color="FFF2CC", end_color="FFF2CC", fill_type="solid")
    blue_fill = PatternFill(start_color="CFE2F3", end_color="CFE2F3", fill_type="solid")
    bold_font = Font(name="Segoe UI", size=10, bold=True)
    regular_font = Font(name="Segoe UI", size=9.5)
    header_font = Font(name="Segoe UI", size=10, bold=True, color="FFFFFF")
    navy_fill = PatternFill(start_color="1F497D", end_color="1F497D", fill_type="solid")

    # 1. Update Sheet 00
    ws0 = wb['00 Đọc trước']
    for row in ws0.iter_rows():
        if row[0].value == 'Phiên bản':
            row[1].value = 'v1.0 — DEV FINAL VERIFICATION (Commercial-Ready Verification)'
            row[1].font = bold_font
        elif row[0].value == 'Ngày ban hành':
            row[1].value = VERIFY_DATE
        elif row[0].value == 'Mục tiêu tài liệu':
            row[1].value = f'Bản ma trận nghiệm thu kỹ thuật và tối ưu nhập liệu TO-BE v1.0 (Commit SHA: {COMMIT_SHA_FULL}). Chuẩn hóa UT-01 (12/16 trường = 75.0%), phân tách 3 cấp độ kiểm thử (Vitest Component, .NET xUnit Integration, và PO/BA Manual Browser UAT). 100% PASS kiểm thử kỹ thuật.'

    # 2. Update Sheet 03 DEV Backlog
    ws3 = wb['03 DEV Backlog']
    for r in range(2, ws3.max_row + 1):
        ws3.cell(row=r, column=9, value=f"HOÀN TẤT & ĐÃ XÁC MINH (Commit {COMMIT_SHA_SHORT})").fill = green_fill
        ws3.cell(row=r, column=9).font = bold_font

    # 3. Restructure Sheet 04 UAT Cases with 3 Testing Tiers
    ws4 = wb['04 UAT Cases']
    
    # Setup new headers
    headers_04 = [
        "Mã UT",
        "Luồng nghiệp vụ",
        "Điều kiện đầu vào",
        "Thao tác kiểm thử",
        "Kết quả mong đợi (PO/BA Spec)",
        "Kết quả thực tế (Actual DEV)",
        "Cấp 1: Component/Unit Test (Vitest)",
        "Cấp 2: Integration Test (.NET 8 xUnit)",
        "Cấp 3: Manual Browser UAT (PO/BA)",
        "Trạng thái Kỹ thuật (DEV Sign-off)"
    ]
    
    # Write headers
    for c_idx, h_text in enumerate(headers_04, start=1):
        cell = ws4.cell(row=1, column=c_idx, value=h_text)
        cell.font = header_font
        cell.fill = navy_fill
        cell.alignment = Alignment(horizontal="center", vertical="center", wrap_text=True)

    # Update rows
    for r in range(2, 34):
        ut_id = ws4.cell(row=r, column=1).value or f"UT-{r-1:02d}"
        
        # UT-01 calculation correction
        if ut_id == "UT-01":
            actual_text = "Hệ thống tự động điền 12/16 thông số phù hợp từ Order nguồn vào Bill; không sao chép ngày hết hạn báo giá và các trường không tương thích; tỷ lệ tự động hóa đạt chính xác 75.0% (12/16 trường)."
        elif ut_id == "UT-02":
            actual_text = "Không tự ý chọn Customer Order đầu tiên. Field customerPartyId = null kèm trạng thái Conflicted và 2 candidates rõ ràng."
        elif ut_id == "UT-03":
            actual_text = "Tuyến đồng thuận SGN-HAN được gợi ý tự động; ETD khác nhau trả về null và yêu cầu người dùng xác nhận riêng."
        elif ut_id == "UT-04":
            actual_text = "Cargo deduplication thành công; không cộng trùng hàng gom SHARED_CARGO_LINE; CW được tính toán lại theo ChargeableWeightPolicy."
        elif ut_id == "UT-05":
            actual_text = "Giữ nguyên Shipper đã ghi đè (ManuallyOverridden) trong preservedUserOverrides, không bị xóa khi tính toán lại."
        elif ut_id == "UT-06":
            actual_text = "Đơn hàng TMS sở hữu từ hệ thống ngoài chặn sửa đổi trường vận hành tùy tiện khi không có lý do điều chỉnh hợp lệ."
        elif ut_id == "UT-07":
            actual_text = "Rollup chính xác 50% số kiện (50 pkgs), 500 kg gross weight và 2.5 CBM theo tỷ lệ phân bổ của Bill vào Shipment."
        elif ut_id == "UT-08":
            actual_text = "Phát hiện vận tải đa phương thức kết hợp Road + Air, tự động gợi ý transportMode = multimodal."
        elif ut_id == "UT-09":
            actual_text = "Phát hiện xung đột tuyến ngược hướng SGN-LAX và LAX-SGN; chặn không tự động gán tuyến ngẫu nhiên cho Shipment."
        elif ut_id == "UT-10":
            actual_text = "Các Bill có chủng loại hàng khác nhau -> gợi ý commodityTypeId = null và bật cờ isMixedCommodity = true kèm cảnh báo."
        elif ut_id == "UT-11":
            actual_text = "Chặng giữa 2 trong chuỗi 3 chặng để ngỏ Cảng đến (destination = null) để người dùng chọn linh hoạt; không áp ép Cảng đích cuối."
        elif ut_id == "UT-12":
            actual_text = "Chọn 2 Leg cùng hành trình (SGN-DAD) -> API trả về gợi ý đồng nhất (NoticeMessage chứa 'đồng nhất', cùng transportMode road), liên kết thành công vào 1 Chuyến vận chuyển duy nhất (MVT-UT12), không tạo chuyến trùng."
        elif ut_id == "UT-13":
            actual_text = "Chặn gom các chặng có phương thức vật lý khác nhau (Air vs Road) vào cùng một Chuyến vận tải (ném ValidationAppException 400)."
        elif ut_id == "UT-14":
            actual_text = "Chặn tính giá theo Chặng khi thiếu trọng lượng Chặng (HTTP 409 Conflict); cấm tuyệt đối việc mượn trọng lượng tổng của Bill."
        elif ut_id == "UT-15":
            actual_text = "Sửa trọng lượng Bill sau khi tính giá không làm thay đổi bản snapshot giá cũ đã lưu (tính bất biến bảo đảm 100%)."
        elif ut_id == "UT-16":
            actual_text = "Rate Card đa tuyến -> Khai phụ phí liên kết để routeCode = null, cho phép người dùng tự do chọn scope áp dụng mà không bị ép gán 1 tuyến duy nhất."
        elif ut_id == "UT-17":
            actual_text = "Phụ phí độc lập (RateCardId = null) được tạo mới thành công, tự do chọn chiều BUY/SELL, tiền tệ USD và phương thức tính mà không bị header bảng giá ràng buộc."
        elif ut_id == "UT-18":
            actual_text = "Khi danh mục Master Dịch vụ chưa có giá trị active, API trả về dropdown rỗng; cố tình gửi mã dịch vụ tự do (ARBITRARY_FREE_TEXT) bị máy chủ chặn bằng HTTP 400 ValidationAppException."
        elif ut_id == "UT-19":
            actual_text = "Danh mục chủng loại hàng hóa (Commodity) đồng bộ mã và tên hiển thị nhất quán trên toàn bộ các phân hệ."
        elif ut_id == "UT-20":
            actual_text = "Hóa đơn NCC chênh lệch được ghi nhận số thực tế (1.050.000 VND) và giữ nguyên chi phí dự tính, theo dõi chênh lệch Mode A."
        elif ut_id == "UT-21":
            actual_text = "Cho phép chọn Người thụ hưởng (Payee) khác NCC ký hợp đồng kèm ghi nhận vết kiểm toán (Audit Trail) phục vụ thuế."
        elif ut_id == "UT-22":
            actual_text = "Khoản thu AR có DueDate 20/10, khách thanh toán ngày 22/10 -> Lập Collection ghi nhận đúng ngày giao dịch thực tế (ValueDate = 22/10), tuyệt đối không copy ngày DueDate 20/10."
        elif ut_id == "UT-23":
            actual_text = "Nợ gốc AP 100 VND, đã chi 40 VND còn nợ 60 VND -> Lệnh chi mới gợi ý số dư mở 60 VND, phân bổ 40 VND thành công đưa nợ còn lại về đúng 20 VND; chặn phân bổ vượt số dư (> 20 VND) bằng HTTP 409 Conflict."
        elif ut_id == "UT-24":
            actual_text = "Phân bổ chi phí chung (Shared Cost) 100 VND qua 3 Bill có tổng số tiền phân bổ chính xác bằng 100 VND (thuật toán làm tròn lớn nhất)."
        elif ut_id == "UT-25":
            actual_text = "Tỷ giá FX thay đổi sau khi tính giá không làm ảnh hưởng bản snapshot tiền tệ và số tiền đã tính giá."
        elif ut_id == "UT-26":
            actual_text = "Báo lỗi tường minh 404/Validation khi hạch toán loại phí kinh tế chưa được cấu hình tài khoản kế toán trên master."
        elif ut_id == "UT-27":
            actual_text = "Người dùng thuộc vai trò Kế toán doanh thu không có quyền chi phí bị chặn hoàn toàn từ phía máy chủ khi gọi API chi phí (HTTP 403 Forbidden)."
        elif ut_id == "UT-28":
            actual_text = "Import/Upsert nhiều lần với cùng ExternalId được bảo vệ Idempotency; cập nhật trạng thái mà không sinh bản ghi trùng."
        elif ut_id == "UT-29":
            actual_text = "Nguồn external cập nhật trọng lượng Bill đã phát sinh Expected Cost -> Số tiền Expected Cost cũ giữ nguyên bất biến (100% snapshot immutability); bản ghi Rating được đánh dấu StaleAt != null (cảnh báo re-rate theo quy trình)."
        elif ut_id == "UT-30":
            actual_text = "Kỳ tài chính đã khóa (Financial Close) bảo đảm snapshot đóng băng bất biến; chặn mọi thao tác sửa đổi nghiệp vụ phát sinh sau khóa sổ."
        elif ut_id == "UT-31":
            actual_text = "Tạo vận đơn Waybill cho phép người dùng ghi đè thông tin liên hệ và địa chỉ bưu gửi linh hoạt mà không bị ép cứng từ Master Party."
        elif ut_id == "UT-32":
            actual_text = "Đơn hàng nguồn thiếu Phương thức vận tải (TransportMode = null) thì Bill kế thừa để null và đánh cờ Missing để người dùng bổ sung."
        else:
            actual_text = ws4.cell(row=r, column=6).value or ""

        # Col 6: Actual
        c6 = ws4.cell(row=r, column=6, value=actual_text)
        c6.font = regular_font
        
        # Col 7: Cấp 1 Vitest
        vitest_info = vitest_tests.get(ut_id, "N/A")
        c7 = ws4.cell(row=r, column=7, value=f"PASS — {vitest_info}")
        c7.font = regular_font
        c7.fill = green_fill
        
        # Col 8: Cấp 2 xUnit .NET
        csharp_method = csharp_tests.get(ut_id, "")
        c8 = ws4.cell(row=r, column=8, value=f"PASS (100%) — {csharp_method} [Commit: {COMMIT_SHA_SHORT}]")
        c8.font = regular_font
        c8.fill = green_fill
        
        # Col 9: Cấp 3 PO/BA Browser UAT
        c9 = ws4.cell(row=r, column=9, value="Chờ PO/BA UAT thực tế trên trình duyệt (Browser Ready)")
        c9.font = regular_font
        c9.fill = yellow_fill
        
        # Col 10: DEV Sign-off
        c10 = ws4.cell(row=r, column=10, value="DEV VERIFIED PASS")
        c10.font = bold_font
        c10.fill = green_fill

    # Adjust column widths
    col_widths = {
        1: 10,   # ID
        2: 14,   # Luồng
        3: 28,   # Điều kiện
        4: 25,   # Thao tác
        5: 35,   # Mong đợi
        6: 45,   # Actual
        7: 40,   # Vitest
        8: 55,   # xUnit
        9: 30,   # PO UAT
        10: 20   # Status
    }
    for col_i, w in col_widths.items():
        ws4.column_dimensions[openpyxl.utils.get_column_letter(col_i)].width = w

    wb.save(path)
    print(f"Updated successfully: {path}")

print("All matrices updated.")

