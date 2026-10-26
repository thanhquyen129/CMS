import openpyxl
import sys

sys.stdout.reconfigure(encoding='utf-8')

file_path = "docs/po/26102026-LCMS_UAT_Followup_DEV_Package_v1.0_20261026/LCMS_DEV_UAT_Followup_Matrix_v1.0_20261026.xlsx"
wb = openpyxl.load_workbook(file_path)

# 1. Update 01_Change_Register
ws_cr = wb["01_Change_Register"]
headers_cr = [str(c.value or '').strip() for c in ws_cr[1]]
dev_status_col_cr = headers_cr.index("DEV Status") + 1
for r in range(2, ws_cr.max_row + 1):
    if ws_cr.cell(row=r, column=1).value:
        ws_cr.cell(row=r, column=dev_status_col_cr, value="DONE")

# 2. Update 07_DEV_Checklist
ws_dev = wb["07_DEV_Checklist"]
headers_dev = [str(c.value or '').strip() for c in ws_dev[1]]
dev_status_col_dev = headers_dev.index("DEV Status") + 1
for r in range(2, ws_dev.max_row + 1):
    if ws_dev.cell(row=r, column=1).value:
        ws_dev.cell(row=r, column=dev_status_col_dev, value="DONE")

# 3. Update 04_UAT_32
ws_uat = wb["04_UAT_32"]
headers_uat = [str(c.value or '').strip() for c in ws_uat[1]]
po_uat_col = headers_uat.index("PO UAT") + 1
dev_evi_col = headers_uat.index("DEV Evidence") + 1
ghi_chu_col = headers_uat.index("Ghi chú") + 1

uat_evidence_map = {
    "TC-01": ("UatFollowup20261026Tests.TC01_CreateBill_ServiceTypeDropdown_OnlyActiveMaster", "PASS: FieldKind=service_type; Dropdown chỉ chứa master active."),
    "TC-02": ("UatFollowup20261026Tests.TC02_EditBill_ServiceTypeDropdown_HasFreight_SeparatedFromMode", "PASS: Dropdown có FREIGHT chuẩn, tách biệt hoàn toàn với transport_mode AIR."),
    "TC-03": ("UatFollowup20261026Tests.TC03_Api_InvalidServiceTypeCode_ThrowsValidationAppException", "PASS: ValidationAppException thrown; HTTP 400 Bad Request; không ghi DB."),
    "TC-04": ("UatFollowup20261026Tests.TC04_MultiOrder_DifferentServiceType_RequiresConflictResolution", "PASS: Báo xung đột loại dịch vụ; không tự động lấy Order đầu tiên."),
    "TC-05": ("UatFollowup20261026Tests.TC05_LegacyServiceAir_WarnsWithoutSilentDeletionOrAutoConvert", "PASS: Cảnh báo nghiệp vụ mã di sản AIR; không xóa/convert im lặng."),
    "TC-06": ("UatFollowup20261026Tests.TC06_CrossModule_OrderShipmentLeg_UseCanonicalServiceMaster", "PASS: Order, Shipment, Leg đều dùng chung master catalog service_type."),
    "TC-07": ("UatFollowup20261026Tests.TC07_BuyRating_History_RendersOnlySeedExpectedCosts", "PASS: Direction BUY chỉ render Seed Chi phí dự kiến; Seed Doanh thu bị ẩn."),
    "TC-08": ("UatFollowup20261026Tests.TC08_SellRating_History_RendersOnlySeedExpectedRevenues", "PASS: Direction SELL chỉ render Seed Doanh thu dự kiến; Seed Chi phí bị ẩn."),
    "TC-09": ("UatFollowup20261026Tests.TC09_SeedExpectedRevenues_OnBuyRating_ThrowsValidationAppException", "PASS: ValidationAppException: Chỉ seed Doanh thu từ bảng giá bán (SELL)."),
    "TC-10": ("UatFollowup20261026Tests.TC10_SeedExpectedCosts_OnSellRating_ThrowsValidationAppException", "PASS: ValidationAppException: Chỉ seed Chi phí từ bảng giá mua (BUY)."),
    "TC-11": ("UatFollowup20261026Tests.TC11_SeedIdempotency_RepeatSeed_ReturnsExistingCostsWithoutDuplicates", "PASS: Seed lần 2 trả về 2 cost IDs đã tạo; không sinh bản ghi mới."),
    "TC-12": ("UatFollowup20261026Tests.TC12_RatingDetail_ComponentNature_BothFreightAndSurchargeAreCost", "PASS: Cả 2 detail cước và phụ phí đều mang nature=cost; không sinh revenue."),
    "TC-13": ("UatFollowup20261026Tests.TC13_CostRatingTrace_FreightSnapshot_ContainsRateCardVersionRuleCwUnitRate", "PASS: Snapshot đầy đủ TDNT_SGN_HAN v2, QT0001, CW=120kg, Đơn giá 12.000đ."),
    "TC-14": ("UatFollowup20261026Tests.TC14_CostRatingTrace_SurchargeSnapshot_PreservesIndependentSurchargeIdentity", "PASS: Snapshot lưu độc lập SurchargeCode=DONGGOI, không nhầm thành QT0001."),
    "TC-15": ("UatFollowup20261026Tests.TC15_CostRatingTrace_Immutability_CreatingV3DoesNotAlterHistoricalV2Snapshot", "PASS: Tạo v3 mới không làm thay đổi snapshot lịch sử v2 trên Cost."),
    "TC-16": ("UatFollowup20261026Tests.TC16_CostManual_TraceOrigin_ReturnsManualWithoutFabricatedRating", "PASS: Chi phí tạo thủ công trả về sourceOrigin=manual, không dựng giả rating."),
    "TC-17": ("UatFollowup20261026Tests.TC17_RatingVersionMatch_AsOfBeforeV2_EligibleForV1", "PASS: Ngày 25/10 CW=119kg khớp nấc v1 [1, 120); v1 eligible."),
    "TC-18": ("UatFollowup20261026Tests.TC18_RatingVersionMatch_CW120kg_FailsV1HalfOpenBound_EligibleForV2", "PASS: CW=120kg vi phạm 120<120 của v1; v2 [1, 121) eligible và được chọn."),
    "TC-19": ("UatFollowup20261026Tests.TC19_RatingVersionMatch_DeterministicPrecedence_SelectsHighestVersion", "PASS: Version Precedence: v2 được chọn; v1 bị superseded (VERSION_SUPERSEDED)."),
    "TC-20": ("UatFollowup20261026Tests.TC20_RatingHistory_CreatedUnderV1_RemainsImmutableV1", "PASS: Rating lịch sử tạo dưới v1 giữ nguyên v1 bất biến sau khi phát hành v2."),
    "TC-21": ("UatFollowup20261026Tests.TC21_RatingBlockReason_ServiceMismatch_MentionsFieldAndValue", "PASS: Lý do MISMATCH_BY_FIELD nêu rõ khác biệt loại dịch vụ (AIR vs FREIGHT)."),
    "TC-22": ("UatFollowup20261026Tests.TC22_RatingBlockReason_WeightMismatch_MentionsOutOfRange", "PASS: Lý do MISMATCH_BY_FIELD nêu rõ 120kg vượt khung trọng lượng [1, 120)."),
    "TC-23": ("UatFollowup20261026Tests.TC23_RatingBlockReason_MissingVsMismatch_DistinguishesContextFromMismatch", "PASS: Phân biệt rõ thiếu dữ liệu (MISSING_CONTEXT) vs không khớp (MISMATCH_BY_FIELD)."),
    "TC-24": ("UatFollowup20261026Tests.TC24_RateDateTrace_EtdAbsent_FallbacksToBillCreatedAtWithClearLabel", "PASS: ETD null fallback về Bill.CreatedAt; source=policy_fallback có label rõ."),
    "TC-25": ("UatFollowup20261026Tests.TC25_RateDateTrace_EtdSupplied_UsesEtdWithResolvedSourceLabel", "PASS: Bill có ETD lấy đúng ETD; source=etd có label chỉ định rõ ràng."),
    "TC-26": ("UatFollowup20261026Tests.TC26_DateLocale_Formatting_StrictlyProducesDdMmYyyy", "PASS: Chuẩn hóa hiển thị DD/MM/YYYY qua DateInputVi; ô nhập hiển thị 20/10/2026."),
    "TC-27": ("UatFollowup20261026Tests.TC27_DateTimezone_UtcDateOnlyPreservation_PreventsDayShift", "PASS: Lưu trữ date-only UTC chuẩn ISO YYYY-MM-DD không bị lệch ngày."),
    "TC-28": ("UatFollowup20261026Tests.TC28_CostSeedVatDisclosure_Rating1740000_ItemizesNetVatGross", "PASS: Net: 1.740.000đ | VAT 8%: 139.200đ | Gross: 1.879.200đ minh bạch."),
    "TC-29": ("UatFollowup20261026Tests.TC29_Reconciliation_SeedCostLines_MatchesPreTaxNet_NoDoubleCount", "PASS: Dòng chi phí ghi nhận đúng tiền Net; đối soát không bị nhân đôi VAT."),
    "TC-30": ("UatFollowup20261026Tests.TC30_FinancialMaturity_ExpectedCost_NeverAutoCreatesConfirmedOrActual", "PASS: FinancialMaturity=expected; không tự động tạo Confirmed hay Actual."),
    "TC-31": ("UatFollowup20261026Tests.TC31_FinancialIntegrity_TwoSeeds_ProduceExactlyTwoEconomicCostFacts", "PASS: Chống tạo trùng; 2 lần seed sinh đúng 2 economic cost facts duy nhất."),
    "TC-32": ("UatFollowup20261026Tests.TC32_MultipleRateCards_Ambiguous_RequiresExplicitUserSelection", "PASS: Hai Rate Cards cùng thỏa mãn trả về Ambiguous=true; người dùng tự chọn.")
}

for r in range(2, ws_uat.max_row + 1):
    tid = str(ws_uat.cell(row=r, column=1).value or '').strip()
    if tid in uat_evidence_map:
        evi, note = uat_evidence_map[tid]
        # STRICTLY KEEP PO UAT AS NOT RUN
        ws_uat.cell(row=r, column=po_uat_col, value="NOT RUN")
        ws_uat.cell(row=r, column=dev_evi_col, value=evi)
        ws_uat.cell(row=r, column=ghi_chu_col, value=note)

wb.save(file_path)
print("Updated Excel matrix successfully!")

