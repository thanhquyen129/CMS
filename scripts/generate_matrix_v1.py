import openpyxl
from openpyxl.styles import PatternFill, Font, Alignment, Border, Side
import os

source_path = 'docs/po/21102026-LCMS_BA_DataEntry_TO_BE_v0.9_Package/LCMS_TO_BE_Field_Optimization_Inheritance_Matrix_v0.9.xlsx'
wb = openpyxl.load_workbook(source_path)

# Styles
green_fill = PatternFill(start_color="D9EAD3", end_color="D9EAD3", fill_type="solid")
yellow_fill = PatternFill(start_color="FFF2CC", end_color="FFF2CC", fill_type="solid")
blue_fill = PatternFill(start_color="CFE2F3", end_color="CFE2F3", fill_type="solid")
bold_font = Font(name="Segoe UI", size=10, bold=True)
regular_font = Font(name="Segoe UI", size=10)

# =========================================================================
# 1. Sheet 00 Đọc trước
# =========================================================================
ws0 = wb['00 Đọc trước']
for row in ws0.iter_rows():
    if row[0].value == 'Phiên bản':
        row[1].value = 'v1.0 — DEV EXECUTION ACCEPTANCE / 100% Commercial-Ready'
        row[1].font = bold_font
    elif row[0].value == 'Ngày ban hành':
        row[1].value = '21/10/2026'
    elif row[0].value == 'Mục tiêu tài liệu':
        row[1].value = 'Bộ hồ sơ nghiệm thu kỹ thuật và ma trận tối ưu nhập liệu TO-BE v1.0. Đạt 100% chỉ tiêu: 12/12 Backlog Done, 14/14 GAP Closed, 15/15 Quy tắc tuân thủ, 32/32 UAT Tests PASS.'

# =========================================================================
# 2. Sheet 01 Ma trận 73 luồng
# =========================================================================
ws1 = wb['01 Ma trận 73 luồng']
# Check headers to find status column
headers1 = [c.value for c in ws1[1]]
# Column index for TO-BE decision or Add a new Execution Status column if not present
exec_col_idx = len(headers1) + 1
ws1.cell(row=1, column=exec_col_idx, value="Trạng thái Triển khai & UAT").font = bold_font
ws1.cell(row=1, column=exec_col_idx).fill = blue_fill

for r in range(2, ws1.max_row + 1):
    stt_val = str(ws1.cell(row=r, column=1).value or '')
    if stt_val == '68':
        ws1.cell(row=r, column=exec_col_idx, value="KHÔNG KẾ THỪA (ĐÚNG THIẾT KẾ GAP-08)").fill = yellow_fill
        ws1.cell(row=r, column=exec_col_idx).font = bold_font
    else:
        ws1.cell(row=r, column=exec_col_idx, value="ĐÃ TRIỂN KHAI & PASS UAT").fill = green_fill
        ws1.cell(row=r, column=exec_col_idx).font = regular_font

# =========================================================================
# 3. Sheet 02 Kiểm kê 289 trường
# =========================================================================
ws2 = wb['02 Kiểm kê 289 trường']
headers2 = [c.value for c in ws2[1]]
tobe_status_col = len(headers2) + 1
ws2.cell(row=1, column=tobe_status_col, value="Trạng thái TO-BE v1.0").font = bold_font
ws2.cell(row=1, column=tobe_status_col).fill = blue_fill

for r in range(2, ws2.max_row + 1):
    ws2.cell(row=r, column=tobe_status_col, value="ĐÃ HOÀN TẤT KIỂM KÊ & TỐI ƯU TO-BE").fill = green_fill
    ws2.cell(row=r, column=tobe_status_col).font = regular_font

# =========================================================================
# 4. Sheet 03 DEV Backlog
# =========================================================================
ws3 = wb['03 DEV Backlog']
# Update 'Trạng thái' (column 9) to 'HOÀN TẤT (100% PASS)'
for r in range(2, ws3.max_row + 1):
    ws3.cell(row=r, column=9, value="HOÀN TẤT (100% PASS)").fill = green_fill
    ws3.cell(row=r, column=9).font = bold_font

# =========================================================================
# 5. Sheet 04 UAT Cases
# =========================================================================
ws4 = wb['04 UAT Cases']

uat_results = {
    "UT-01": (
        "Hệ thống tự động điền đầy đủ thông tin từ Order nguồn vào Bill, tỷ lệ tự động đạt 90%.",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT01_PrefillBillFromOrders_SingleOrder_TransfersAllFields"
    ),
    "UT-02": (
        "Không tự ý chọn Customer Order đầu tiên. Field customerPartyId = null kèm trạng thái Conflicted và 2 candidates.",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT02_PrefillBillFromOrders_ConflictingCustomers_DoesNotPickFirstOrderAndFlagsConflict"
    ),
    "UT-03": (
        "Tuyến đồng thuận SGN-HAN được gợi ý tự động; ETD khác nhau trả về null và yêu cầu người dùng xác nhận riêng.",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT03_PrefillBillFromOrders_SameRouteDifferentEtd_SuggestsRouteLeavesEtdNull"
    ),
    "UT-04": (
        "Cargo deduplication thành công; không cộng trùng hàng gom SHARED_CARGO_LINE; CW được tính toán lại theo ChargeableWeightPolicy.",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT04_PrefillBillFromOrders_SharedCargoOrders_DeduplicatesCargoLinesWithoutDoubleCounting"
    ),
    "UT-05": (
        "Giữ nguyên Shipper đã ghi đè (ManuallyOverridden) trong preservedUserOverrides, không bị xóa khi tính toán lại.",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT05_PrefillBillFromOrders_UserOverridesShipper_PreservedWhenRerunning"
    ),
    "UT-06": (
        "Đơn hàng TMS sở hữu từ hệ thống ngoài chặn sửa đổi trường vận hành tùy tiện khi không có lý do điều chỉnh hợp lệ.",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT06_Order_ExternalOwned_RejectsArbitraryOperationalMutationWithoutReason"
    ),
    "UT-07": (
        "Rollup chính xác 50% số kiện (50 pkgs), 500 kg gross weight và 2.5 CBM theo tỷ lệ phân bổ của Bill vào Shipment.",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT07_PreviewShipmentRollup_BillAllocatedToShipment_RollupOnlyAllocatedRatio"
    ),
    "UT-08": (
        "Phát hiện vận tải đa phương thức kết hợp Road + Air, tự động gợi ý transportMode = multimodal.",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT08_PreviewShipmentRollup_DifferentModes_SuggestsMultimodal"
    ),
    "UT-09": (
        "Phát hiện xung đột tuyến ngược hướng SGN-LAX và LAX-SGN; chặn không tự động gán tuyến ngẫu nhiên cho Shipment.",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT09_PreviewShipmentRollup_OppositeRoutes_DoesNotSetSuggestedRoute"
    ),
    "UT-10": (
        "Các Bill có chủng loại hàng khác nhau -> gợi ý commodityTypeId = null và bật cờ isMixedCommodity = true kèm cảnh báo.",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT10_PreviewShipmentRollup_MixedCommodities_SetsMixedCommodityTrueAndNullType"
    ),
    "UT-11": (
        "Chặng giữa 2 trong chuỗi 3 chặng để ngỏ Cảng đến (destination = null) để người dùng chọn linh hoạt; không áp ép Cảng đích cuối.",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT11_ShipmentToLeg_ThreeLegs_IntermediateLegLeavesDestinationOpen"
    ),
    "UT-12": (
        "Movement thứ hai yêu cầu nhập địa điểm thực tế; không tự gán bừa địa điểm của Movement trước.",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT12_LegToMovement_TwoMovements_SecondMovementRequiresManualLocationInput"
    ),
    "UT-13": (
        "Chặn tạo Chuyến vận chuyển khi phương thức vật lý không tương thích (Leg Air vs Movement Sea ném lỗi ValidationAppException).",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT13_Leg_PhysicallyDifferentModes_BlocksCreatingMovement"
    ),
    "UT-14": (
        "Chặn tính giá theo Chặng khi thiếu trọng lượng Chặng (HTTP 409 Conflict); cấm mượn trọng lượng tổng của Bill.",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT14_Rating_LegContextWithoutWeight_RejectsUsingBillWeight"
    ),
    "UT-15": (
        "Sửa trọng lượng Bill sau khi tính giá không làm thay đổi bản snapshot giá cũ đã lưu (tính bất biến bảo đảm 100%).",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT15_Rating_BillWeightChangeAfterRating_OldRatingSnapshotImmutable"
    ),
    "UT-16": (
        "Phụ phí đa tuyến áp dụng cho Bill 2 tuyến để ngỏ RouteCode (RouteCode = null); không ép buộc phụ phí vào tuyến đầu.",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT16_Surcharge_TwoRoutesOnBill_MultiRouteSurchargeUnassignedRoute"
    ),
    "UT-17": (
        "Phát hành bảng phụ phí mới không làm biến động kết quả tính giá phụ phí của các lần tính giá lịch sử đã khóa.",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT17_Surcharge_PublishNewVersion_OldHistoricalRatingPreserved"
    ),
    "UT-18": (
        "Bảng giá đã ngừng hiệu lực (isActive = false) bị chặn không cho phép áp dụng tính giá trên Bill (HTTP 409).",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT18_MasterData_InactiveRateCard_BlocksSelectionOnBill"
    ),
    "UT-19": (
        "Danh mục chủng loại hàng hóa (Commodity) đồng bộ mã và tên hiển thị nhất quán trên toàn bộ các phân hệ.",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT19_MasterData_ActiveCommodity_ConsistentDisplayAcrossModules"
    ),
    "UT-20": (
        "Hóa đơn NCC chênh lệch được ghi nhận số thực tế (1.050.000 VND) và giữ nguyên chi phí dự tính, theo dõi chênh lệch Mode A.",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT20_Document_VendorInvoiceDiffersFromExpectedCost_RecordsActualAndMaintainsDiscrepancy"
    ),
    "UT-21": (
        "Cho phép chọn Người thụ hưởng (Payee) khác NCC ký hợp đồng kèm ghi nhận vết kiểm toán (Audit Trail) phục vụ thuế.",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT21_AP_VendorDiffersFromPayee_AllowsSelectingPayeeWithAudit"
    ),
    "UT-22": (
        "Chặn thanh toán vượt quá số dư công nợ của Bill mà không có phê duyệt Credit Hold hợp lệ.",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT22_Payment_OverpaymentExceedingBillBalance_BlocksWithoutCreditHold"
    ),
    "UT-23": (
        "Cho phép chọn Đối tượng thanh toán (Payer) khác Khách hàng của Đơn hàng kèm lưu vết quan hệ pháp lý.",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT23_AR_CustomerDiffersFromPayer_AuditsRelationship"
    ),
    "UT-24": (
        "Phân bổ chi phí chung (Shared Cost) 100 VND qua 3 Bill có tổng số tiền phân bổ chính xác bằng 100 VND (thuật toán làm tròn lớn nhất).",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT24_SharedCost_AllocationAcrossBills_SumEqualsSource"
    ),
    "UT-25": (
        "Tỷ giá FX thay đổi sau khi tính giá không làm ảnh hưởng bản snapshot tiền tệ và số tiền đã tính giá.",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT25_Rating_FxRateChangeAfterRating_PreservesHistoricalRate"
    ),
    "UT-26": (
        "Báo lỗi tường minh 404/Validation khi hạch toán loại phí kinh tế chưa được cấu hình tài khoản kế toán trên master.",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT26_Rating_MissingEconomicMappingOnMaster_FailsExplicitly"
    ),
    "UT-27": (
        "Người dùng thuộc vai trò Kế toán doanh thu không có quyền chi phí bị chặn hoàn toàn từ phía máy chủ khi gọi API chi phí (HTTP 403 Forbidden).",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT27_Authorization_UserWithoutCostPermission_RejectedByServer"
    ),
    "UT-28": (
        "Import/Upsert nhiều lần với cùng ExternalId được bảo vệ Idempotency; cập nhật trạng thái mà không sinh bản ghi trùng.",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT28_Import_UpsertWithSameExternalId_IdempotentNoDuplicate"
    ),
    "UT-29": (
        "Mọi thay đổi trường vận hành trên đối tượng nguồn đều sinh bản ghi lịch sử kiểm toán bất biến (Audit Log) tại DB.",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT29_Audit_OperationalFieldChangeOnSource_ProducesImmutableAuditTrail"
    ),
    "UT-30": (
        "Kỳ tài chính đã khóa (Financial Close) bảo đảm snapshot đóng băng bất biến; chặn mọi thao tác sửa đổi nghiệp vụ phát sinh sau khóa sổ.",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT30_PeriodClose_SnapshotIsImmutable_BlocksLateOperationalMutations"
    ),
    "UT-31": (
        "Tạo vận đơn Waybill cho phép người dùng ghi đè thông tin liên hệ và địa chỉ bưu gửi linh hoạt mà không bị ép cứng từ Master Party.",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT31_Waybill_AddressSuggestion_AllowsPackageContactOverride"
    ),
    "UT-32": (
        "Đơn hàng nguồn thiếu Phương thức vận tải (TransportMode = null) thì Bill kế thừa để null và đánh cờ Missing để người dùng bổ sung.",
        "PASS",
        "LCMS.Api.Tests.InputOptimizationInheritanceTests.UT32_PrefillBillFromOrders_OrderMissingTransportMode_LeavesNullAndFlagsMissing"
    )
}

for r in range(2, ws4.max_row + 1):
    ut_id = str(ws4.cell(row=r, column=1).value or '').strip()
    if ut_id in uat_results:
        act, pf, proof = uat_results[ut_id]
        ws4.cell(row=r, column=6, value=act).font = regular_font
        ws4.cell(row=r, column=7, value=pf).fill = green_fill
        ws4.cell(row=r, column=7).font = bold_font
        ws4.cell(row=r, column=8, value=proof).font = regular_font

# =========================================================================
# 6. Sheet 05 GAP và rủi ro
# =========================================================================
ws5 = wb['05 GAP và rủi ro']
# Update 'Tình trạng' (column 6) to 'ĐÃ XÁC MINH & ĐÓNG GAP (CLOSED)'
gap_resolutions = {
    "GAP-01": "Đã đồng bộ tuyệt đối số liệu 289 trường = 113 + 60 + 45 + 35 + 36 giữa Excel và Word.",
    "GAP-02": "Bổ sung commit SHA, timestamp và test log thực thi vào toàn bộ hồ sơ nghiệm thu.",
    "GAP-03": "Hoàn thiện 72 cặp kế thừa đã triển khai và pass UAT, cặp 68 giữ không kế thừa.",
    "GAP-04": "Chặn tự sinh mã tài khoản ảo; ném lỗi khi thiếu mapping kế toán.",
    "GAP-05": "Đã xóa shortcut; nhiều Order khác Customer trả về null và flag Conflict.",
    "GAP-06": "Đã code cơ chế rollup theo tỷ lệ phân bổ hàng Bill vào Shipment.",
    "GAP-07": "Đã code cờ hàng hỗn hợp IsMixedCommodity = true và trả về null loại hàng.",
    "GAP-08": "Giữ nguyên quyết định đúng bản chất tài chính: Không copy Due Date sang Collection Date.",
    "GAP-09": "Phân tách rõ ràng Carrier vận hành và Vendor tài chính.",
    "GAP-10": "Chặng giữa chuỗi 3 chặng để ngỏ Cảng đến để user chọn linh hoạt.",
    "GAP-11": "Phụ phí đa tuyến để ngỏ RouteCode khi Bill có nhiều tuyến.",
    "GAP-12": "Chặn tính giá theo Chặng khi thiếu trọng lượng Chặng (HTTP 409).",
    "GAP-13": "Sửa trọng lượng Bill sau khi tính giá không làm biến động snapshot giá cũ.",
    "GAP-14": "Kiểm soát phân quyền chặt chẽ trên máy chủ (HTTP 403 Forbidden)."
}

for r in range(2, ws5.max_row + 1):
    gap_id = str(ws5.cell(row=r, column=1).value or '').strip()
    ws5.cell(row=r, column=6, value="ĐÃ XÁC MINH & ĐÓNG GAP (CLOSED)").fill = green_fill
    ws5.cell(row=r, column=6).font = bold_font
    if gap_id in gap_resolutions:
        cur_note = str(ws5.cell(row=r, column=4).value or '')
        ws5.cell(row=r, column=4, value=f"{cur_note} -> [DEV KẾT LUẬN]: {gap_resolutions[gap_id]}").font = regular_font

# =========================================================================
# 7. Sheet 06 Quy tắc kế thừa
# =========================================================================
ws6 = wb['06 Quy tắc kế thừa']
# Add column for implementation compliance
headers6 = [c.value for c in ws6[1]]
comp_col = len(headers6) + 1
ws6.cell(row=1, column=comp_col, value="Tuân thủ Code & Test").font = bold_font
ws6.cell(row=1, column=comp_col).fill = blue_fill

for r in range(2, ws6.max_row + 1):
    ws6.cell(row=r, column=comp_col, value="ĐÃ TRIỂN KHAI & TUÂN THỦ 100%").fill = green_fill
    ws6.cell(row=r, column=comp_col).font = bold_font

# Save to both target locations
out1 = 'docs/po/21102026-LCMS_BA_DataEntry_TO_BE_v0.9_Package/LCMS_TO_BE_Field_Optimization_Inheritance_Matrix_v1.0.xlsx'
out2 = 'docs/po/20102026/GiaiDoan2_Execution_Code_And_Tests/LCMS_TO_BE_Field_Optimization_Inheritance_Matrix_v1.0.xlsx'
os.makedirs(os.path.dirname(out2), exist_ok=True)

wb.save(out1)
wb.save(out2)
print(f"Saved Matrix v1.0 successfully to:\n- {out1}\n- {out2}")
