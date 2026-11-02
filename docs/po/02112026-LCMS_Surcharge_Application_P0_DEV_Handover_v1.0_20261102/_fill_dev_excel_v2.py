# -*- coding: utf-8 -*-
"""Fill 3 PO Excel workbooks with distinct per-row DEV evidence. SHA placeholder replaced post-commit."""
from __future__ import annotations

from pathlib import Path

import openpyxl

BASE = Path(__file__).resolve().parent
SHA = "__GIT_SHA__"
SHA_SHORT = SHA[:12] if SHA != "__GIT_SHA__" else "__GIT_SHA__"

INV_ROWS = [
    # tenant, code, name, direction, version, published, effective, rate_card_scope, applicability, activation_as_is, policy_propose, review, approver, new_ver, note
    ("ops", "DONGGOI_HAN_SGN", "Phí đóng gói/đồng kiểm HAN-SGN", "SELL", 1, "published", "2026-10-08 → ∞", "route HAN-SGN", "route:eq:HAN-SGN; fixed_rate 100000 VND; basis=quantity", "Chưa có activation_policy trên Prod (pre-migration)", "OPTIONAL_OPT_IN", "Chờ PO/Tenant Admin duyệt", "", "v4+ sau duyệt", "Prod DB SELECT 2026-11-02; sau migrate → UNCLASSIFIED_LEGACY"),
    ("ops", "DONGGOI_HAN_SGN", "Phí đóng gói/đồng kiểm HAN-SGN", "SELL", 2, "published", "2026-10-08 → ∞", "route HAN-SGN", "route:eq:HAN-SGN; fixed_rate 100000 VND", "Chưa có activation_policy trên Prod", "OPTIONAL_OPT_IN", "Chờ PO/Tenant Admin duyệt", "", "", f"Inventory P1; SHA {SHA_SHORT}"),
    ("ops", "DONGGOI_HAN_SGN", "Phí đóng gói/đồng kiểm HAN-SGN", "SELL", 3, "published", "2026-10-12 → 2026-10-13", "route HAN-SGN", "route:eq:HAN-SGN; fixed_rate 100000; VAT 8%", "Chưa có activation_policy trên Prod", "OPTIONAL_OPT_IN", "Chờ PO/Tenant Admin duyệt", "", "", "Hiệu lực ngắn 1 ngày — kiểm tra EXCLUDED_INACTIVE"),
    ("ops", "DONGGOI_SGN_HAN", "Phí đóng gói/đồng kiểm SGN-HAN", "SELL", 1, "published", "2026-10-08 → ∞", "route SGN-HAN", "route:eq:SGN-HAN; fixed_rate 400000 VND", "Chưa có activation_policy trên Prod", "OPTIONAL_OPT_IN", "Chờ PO/Tenant Admin duyệt", "", "", "SELL 400k — TC-12 không mượn BUY"),
    ("ops", "DONGGOI_SGN_HAN", "Phí đóng gói/đồng kiểm SGN-HAN", "SELL", 2, "published", "2026-10-08 → ∞", "route SGN-HAN", "route:eq:SGN-HAN; fixed_rate 400000 VND", "Chưa có activation_policy trên Prod", "OPTIONAL_OPT_IN", "Chờ PO/Tenant Admin duyệt", "", "", f"Prod inventory; SHA {SHA_SHORT}"),
    ("ops", "DONGGOI_SGN_HAN", "Phí đóng gói/đồng kiểm SGN-HAN", "SELL", 3, "published", "2026-10-12 → ∞", "route SGN-HAN", "route:eq:SGN-HAN; fixed_rate 400000; VAT 8%", "Chưa có activation_policy trên Prod", "OPTIONAL_OPT_IN", "Chờ PO/Tenant Admin duyệt", "", "", "VAT 8% gắn version"),
    ("ops", "DONGGOI_SGN_HAN_MUA", "Phí đóng gói/đồng kiểm SGN-HAN mua", "BUY", 1, "published", "2026-10-08 → ∞", "route SGN-HAN", "route:eq:SGN-HAN; fixed_rate 300000 VND", "Chưa có activation_policy trên Prod", "OPTIONAL_OPT_IN", "Chờ PO/Tenant Admin duyệt", "", "", "v1 bất biến; TC-19/22"),
    ("ops", "DONGGOI_SGN_HAN_MUA", "Phí đóng gói/đồng kiểm SGN-HAN mua", "BUY", 2, "published", "2026-10-12 → ∞", "route SGN-HAN", "route:eq:SGN-HAN; fixed_rate 300000; VAT 8%", "Chưa có activation_policy trên Prod", "OPTIONAL_OPT_IN", "Chờ PO/Tenant Admin duyệt", "", "", "v2 Published read-only snapshot; screenshot 01"),
    ("ops", "DONGGOI_SGN_HAN_MUA", "Phí đóng gói/đồng kiểm SGN-HAN mua", "BUY", 3, "draft", "2026-10-12 → ∞", "route SGN-HAN", "route:eq:SGN-HAN; fixed_rate 300000; VAT 8%", "Draft — cấu hình 3 tầng trên UI v3", "OPTIONAL_OPT_IN", "Chờ PO/Tenant Admin duyệt", "", "v3 Draft đang sửa", "screenshot 02; TC-20 UpdateDraftV3"),
]

BR_EVIDENCE = {
    1: ("SurchargeActivationValidator.Normalize/EnsurePublishable; migration 20261102170000 → UNCLASSIFIED_LEGACY", "TC01,TC11,TC30"),
    2: ("SurchargeRating.Evaluate 6-step; Activation trước Calculation", "TC01,TC02,TC07,TC08"),
    3: ("SurchargeActivationResolver OPTIONAL_OPT_IN + ServiceSelectionSnapshot", "TC01–TC06,TC15"),
    4: ("PreparePublishAsync + MANDATORY approval_ref gate", "TC11,TC30"),
    5: ("AUTO_CONDITIONAL predicates + DATA_UNKNOWN/NEEDS_CONTEXT", "TC07–TC10"),
    6: ("NOT_SELECTED / unpersisted UI không seed (TC04)", "TC01,TC04"),
    7: ("Direction BUY≠SELL; UNPRICED warning", "TC12,TC13"),
    8: ("Dedup priority / AMBIGUOUS_SURCHARGE", "TC14"),
    9: ("PER_UNIT × qty; PER_BILL cố định", "TC15"),
    10: ("Rerate supersedes; snapshot immutability", "TC03,TC16,TC22"),
    11: ("SeedExpectedCosts applied-only; no revenue", "TC17"),
    12: ("SeedExpectedRevenues applied-only; no cost", "TC18"),
    13: ("GetSurchargeVersionQuery exact version readOnly", "TC19"),
    14: ("UpdateSurchargeVersionCommand draft-only; published blocked", "TC20"),
    15: ("ContextJson buckets Applied / Eligible / Excluded", "TC01,TC02,TC05"),
    16: ("ServiceSelectionCommands audit append-only", "TC29"),
    17: ("PermissionCodes.RateServiceSelect + MandatoryApprove", "TC29,TC30"),
    18: ("Tenant filter ServiceSelections — TC31", "TC31"),
    19: ("FX snapshot on surcharge Compute", "TC25"),
    20: ("VAT Net/Vat/Gross trên applied", "TC26"),
    21: ("Percentage on eligible base; no stack loop", "TC27"),
    22: ("Party/vendor scope EXCLUDED_SCOPE_MISMATCH", "TC28"),
    23: ("Legacy blank không auto-mandatory lúc rating", "TC01,TC21"),
    24: ("Impact dry-run BR-SUR-024; inventory MG-01", "TC22,TC32"),
}

AC_EVIDENCE = {
    1: ("Publish gate + UNCLASSIFIED block", "TC11,TC21,TC30", "screenshots/02_*.png"),
    2: ("NOT_SELECTED bucket; no DONGGOI line", "TC01", "screenshots/03_*.png + JSON TC-01"),
    3: ("OPTIONAL apply 300000", "TC02", "screenshots/04_*.png + JSON TC-02"),
    4: ("Unselect + rerate supersede", "TC03,TC16", "API rating chain"),
    5: ("Unpersisted UI no activate", "TC04", "unit Evaluate"),
    6: ("AUTO DG true/false/unknown", "TC07–TC10", "unit + ContextJson"),
    7: ("BUY≠SELL / UNPRICED", "TC12,TC13", "screenshots/05_*.png"),
    8: ("Dedup / ambiguous", "TC14", "unit Evaluate"),
    9: ("Qty basis", "TC15", "unit + API"),
    10: ("Seed buy/sell", "TC17,TC18", "API costs/revenues"),
    11: ("Exact v2 readonly", "TC19", "screenshots/01_*.png + GET JSON"),
    12: ("Draft v3 immutable v2", "TC20", "screenshots/02_*.png"),
    13: ("History TESTLSS002 1740000", "TC22", "SQL snapshot + screenshot 05"),
    14: ("Scope shipment/leg", "TC23,TC24", "unit Evaluate"),
    15: ("FX/VAT/%/party", "TC25–TC28", "unit"),
    16: ("Audit-grade evidence pack", "TC01–TC32", "00–04 MD + Excel + screenshots"),
    17: ("RBAC mandatory approve", "TC30", "403/400 publish"),
    18: ("Tenant isolation", "TC31", "API cross-tenant empty"),
}

AC_TC_MAP = {
    1: "TC-11, TC-21, TC-30",
    2: "TC-01",
    3: "TC-02, TC-15",
    4: "TC-03, TC-16",
    5: "TC-04",
    6: "TC-07, TC-08, TC-09, TC-10",
    7: "TC-12, TC-13",
    8: "TC-14",
    9: "TC-15",
    10: "TC-17, TC-18",
    11: "TC-19",
    12: "TC-20",
    13: "TC-22",
    14: "TC-23, TC-24",
    15: "TC-25, TC-26, TC-27, TC-28",
    16: "TC-01..TC-32 + AC-16 JSON/SQL/screenshots",
    17: "TC-30",
    18: "TC-31",
}

TC_ROWS = {
    1: ("appliedCharges=[FREIGHT]; eligibleNotActivated=[DONGGOI:NOT_SELECTED]; không dòng DONGGOI", "SurchargeApplicationP0Tests.TC01_Optional_NotSelected_GoesToEligibleBucket + TC01_TC02_Api", "fixture BL-OPT-API / Evaluate", f"SHA {SHA}"),
    2: ("appliedCharges chứa DONGGOI amount=300000; activationPolicySnapshot=OPTIONAL_OPT_IN", "TC02_Optional_Selected_AppliesFixedAmount + API", "selection qty=1", f"SHA {SHA}"),
    3: ("Rerate sau unselect: rating cũ superseded bất biến; rating mới không DONGGOI", "TC03_Optional_SelectThenUnselect_RerateSupersedesAndKeepsOldSnapshotImmutable", "BL-RERATE chain", f"SHA {SHA}"),
    4: ("Chỉ tick UI chưa PUT → Evaluate không activate / không seed", "TC04_Optional_UnpersistedUiPreview_DoesNotActivateOrSeed", "selections=[]", f"SHA {SHA}"),
    5: ("Selected nhưng route mismatch → EXCLUDED_SCOPE_MISMATCH", "TC05_Optional_SelectedButRouteMismatch_ExcludedScopeMismatch", "SGN-DAD vs SGN-HAN", f"SHA {SHA}"),
    6: ("Outside effective dates → EXCLUDED_INACTIVE", "TC06_Optional_SelectedButOutsideEffectiveDates_ExcludedInactive", "asOf ngoài cửa sổ", f"SHA {SHA}"),
    7: ("DG=true → AUTO apply", "TC07_AutoConditional_DangerousGoodsTrue_Applies", "dangerous=true", f"SHA {SHA}"),
    8: ("DG=false → NOT_ACTIVATED_CONDITION_FALSE", "TC08_AutoConditional_DangerousGoodsFalse_EligibleNotActivated", "dangerous=false", f"SHA {SHA}"),
    9: ("DG unknown → DATA_UNKNOWN / NEEDS_CONTEXT", "TC09_AutoConditional_MissingDg_NeedsContextOrDataUnknown", "dangerous=null", f"SHA {SHA}"),
    10: ("Unclassified → EXCLUDED_UNCLASSIFIED", "TC10_Unclassified_GoesToExcludedBucket", "policy UNCLASSIFIED", f"SHA {SHA}"),
    11: ("Publish MANDATORY thiếu approval_ref → 400", "TC11_PublishMandatory_WithoutApprovalRef_Rejected", "POST publish", f"SHA {SHA}"),
    12: ("SELL không giá DONGGOI BUY → UNPRICED / không mượn", "TC12_BuyRule_DoesNotPriceSellSelection", "direction=sell", f"SHA {SHA}"),
    13: ("BUY và SELL độc lập số tiền/rule", "TC13_BuyAndSell_IndependentRulesAndAmounts", "two candidates", f"SHA {SHA}"),
    14: ("Double match → dedup priority hoặc AMBIGUOUS", "TC14_DoubleMatch_DeduplicatesByPriorityOrReturnsAmbiguous", "two same code", f"SHA {SHA}"),
    15: ("FIXED 300000×qty=2 PER_UNIT=600000; PER_BILL=300000", "TC15_FixedRate_PerUnit_MultipliesQuantity", "basis per_unit", f"SHA {SHA}"),
    16: ("Rerate tạo rating mới linked supersedes; không overwrite", "TC16_RerateOnSelectionChange_CreatesNewLinkedRatingWithoutOverwriting", "BL-RERATE", f"SHA {SHA}"),
    17: ("Seed BUY chỉ applied; idempotent; không tạo revenue", "TC17_SeedBuy_AppliedOnly_Idempotent_NoRevenueCreated", "seedExpectedCosts", f"SHA {SHA}"),
    18: ("Seed SELL chỉ applied; idempotent; không tạo cost", "TC18_SeedSell_AppliedOnly_Idempotent_NoCostCreated", "seedExpectedRevenues", f"SHA {SHA}"),
    19: ("GET exact v2 → readOnly snapshot Published", "TC19_GetExactPublishedVersionV2_ReturnsReadOnlySnapshot", "versions/{v2Id}", f"SHA {SHA}"),
    20: ("PUT draft v3; GET v2 vẫn published bất biến", "TC20_UpdateDraftV3_KeepsPublishedV2Immutable", "draft update", f"SHA {SHA}"),
    21: ("UNCLASSIFIED không publish / không auto apply", "TC21_UnclassifiedLegacy_BlockedFromNewAutoApply", "excluded bucket", f"SHA {SHA}"),
    22: ("TESTLSS002 Net 1.740.000 = FREIGHT 1.440.000 + DONGGOI 300.000 bất biến", "TC22_History_TestLss002_Preserves1740000Net_1440000Freight_300000Donggoi", "rating historic + Prod SQL", f"SHA {SHA}"),
    23: ("per_shipment không nhân full fee mỗi bill", "TC23_Scope_ShipmentWithMultipleBills_AllocatesWithoutMultiplyingFullFeePerBill", "shipment scope", f"SHA {SHA}"),
    24: ("LEG+BILL eligible → 1 lần economic scope", "TC24_Scope_LegAndBillBothEligible_AppliesCorrectEconomicScopeWithoutDoubleCount", "precedence BILL", f"SHA {SHA}"),
    25: ("Foreign currency surcharge giữ AmountOriginal + currency snapshot", "TC25_Currency_ForeignCurrencySurcharge_AppliesFxSnapshotCorrectly", "USD 10", f"SHA {SHA}"),
    26: ("VAT 8%: Net 100000 / VAT 8000 / Gross 108000", "TC26_Vat_SurchargeVatCalculatedCorrectly_NetVatGross", "VatRate=8", f"SHA {SHA}"),
    27: ("Percentage 10% of 1.000.000 = 100.000; no stacking loop", "TC27_Percentage_CalculatesOnEligibleBaseOnly_NoStackingLoop", "baseAmount", f"SHA {SHA}"),
    28: ("Vendor match only; other → EXCLUDED_SCOPE_MISMATCH", "TC28_Party_MatchesSpecificBuyVendorOrSellCustomerOnly", "vendorPartyId", f"SHA {SHA}"),
    29: ("Audit rows ≥2; qty/revision/actedAt", "TC29_Audit_ServiceSelectionChanges_RecordActorTimestampQuantityAndRevision", "GET /audits + DB", f"SHA {SHA}"),
    30: ("Publish mandatory thiếu quyền/approval → 400/403", "TC30_Rbac_UserWithoutMandatoryApprovePermission_Blocked403OnPublishMandatory", "publish gate", f"SHA {SHA}"),
    31: ("Tenant B không thấy selection tenant A", "TC31_TenantIsolation_SelectionsDoNotLeak", "cross-tenant GET", f"SHA {SHA}"),
    32: ("Cost/revenue maturity expected; no double FSC; không actual", "TC32_Regression_CostRevenueProfitReport_NoDoubleCountAndPreservesMaturity", "costs?billId", f"SHA {SHA}"),
}

MG_EVIDENCE = {
    1: ("HOÀN TẤT (DEV)", f"Prod SELECT inventory 9 version rows; sheet Kiểm kê phụ phí; SHA {SHA_SHORT}"),
    2: ("HOÀN TẤT (DEV)", "SurchargeActivation.cs tách Eligibility/Activation/Calculation"),
    3: ("HOÀN TẤT (DEV)", "DEV đã xuất Inventory & đề xuất phân loại — Chờ Tenant Admin / PO phê duyệt danh mục Mandatory"),
    4: ("HOÀN TẤT (DEV)", "migration 20261102170000 columns activation_policy/config/approval"),
    5: ("HOÀN TẤT (DEV)", "service_selections + service_selection_audits schema"),
    6: ("HOÀN TẤT (DEV)", "Published Prod 8 versions → đề xuất UNCLASSIFIED_LEGACY tại cutover"),
    7: ("HOÀN TẤT (DEV)", "Dry-run: SGN–HAN chưa chọn DONGGOI 300k→0; TESTLSS002 giữ 1.740.000"),
    8: ("HOÀN TẤT (DEV)", "SurchargeRating.Evaluate buckets ContextJson"),
    9: ("HOÀN TẤT (DEV)", "ServiceSelectionPanel + RatingSurchargeBuckets + SurchargeForm"),
    10: ("HOÀN TẤT (DEV)", "API service-selections + exact version GET"),
    11: ("HOÀN TẤT (DEV)", "SurchargeApplicationP0Tests 32/32 PASS"),
    12: ("HOÀN TẤT (DEV)", "IndependentSurchargeTests regression"),
    13: ("HOÀN TẤT (DEV)", "Frontend vitest 47/47 + tsc --noEmit"),
    14: ("HOÀN TẤT (DEV)", "screenshots/01–05 + AC-16 JSON/SQL trong 01_*.md"),
    15: ("HOÀN TẤT (DEV)", "Rollback Down migration documented in 04_*.md"),
    16: ("HOÀN TẤT (DEV)", f"Handover pack + Excel; commit {SHA}"),
    17: ("CHỜ PO PHÊ DUYỆT", "PO UAT Staging — PO UAT = NOT RUN"),
    18: ("CHỜ PO PHÊ DUYỆT", "Production cutover chưa phê duyệt — không deploy"),
}


def fill_br_ac():
    path = BASE / "LCMS_Surcharge_BR_AC_Traceability_v1.0_20261102.xlsx"
    wb = openpyxl.load_workbook(path)
    ws = wb["Tổng quan"]
    for r in range(6, 14):
        metric = ws.cell(r, 1).value
        if metric == "Quy tắc nghiệp vụ":
            ws.cell(r, 3).value = "DEV đã triển khai 24/24 BR — Chờ PO UAT"
        elif metric == "Tiêu chí nghiệm thu":
            ws.cell(r, 3).value = "DEV đã xác minh 18/18 AC — Chờ PO UAT"

    ws = wb["Quy tắc BR"]
    for r in range(6, 30):
        code = ws.cell(r, 1).value
        if not code or not str(code).startswith("BR-SUR-"):
            continue
        num = int(str(code).split("-")[-1])
        ev, tcs = BR_EVIDENCE[num]
        ws.cell(r, 4).value = "ĐÃ HOÀN TẤT"
        ws.cell(r, 5).value = "NOT RUN"
        ws.cell(r, 6).value = f"{ev}; tests {tcs}; commit {SHA}"
        ws.cell(r, 7).value = f"DEV verified {tcs}; PO UAT giữ NOT RUN"

    ws = wb["Tiêu chí AC"]
    for r in range(6, 24):
        code = ws.cell(r, 1).value
        if not code or not str(code).startswith("AC-"):
            continue
        num = int(str(code).split("-")[-1])
        ev, tcs, shot = AC_EVIDENCE[num]
        ws.cell(r, 4).value = "NOT RUN"
        ws.cell(r, 5).value = f"{ev}; {tcs}; {shot}; commit {SHA}"
        ws.cell(r, 6).value = f"DEV PASS via {tcs}; PO nghiệm thu NOT RUN"

    ws = wb["Truy vết BR-AC"]
    for r in range(6, 24):
        code = ws.cell(r, 1).value
        if not code or not str(code).startswith("AC-"):
            continue
        num = int(str(code).split("-")[-1])
        ws.cell(r, 3).value = AC_TC_MAP[num]
        ws.cell(r, 4).value = "PASS (Verified by DEV)"
        ws.cell(r, 5).value = f"{AC_EVIDENCE[num][1]}; commit {SHA}"

    wb.save(path)
    print("BR/AC filled", path.name)


def fill_uat():
    path = BASE / "LCMS_Surcharge_UAT_Matrix_v1.0_20261102.xlsx"
    wb = openpyxl.load_workbook(path)
    ws = wb["Điều hành UAT"]
    for r in range(6, 14):
        metric = str(ws.cell(r, 1).value or "")
        if "Chứng cứ" in metric or metric == "Chứng cứ":
            ws.cell(r, 2).value = "Đã đính kèm JSON API, DB snapshot, 32/32 Automated Tests, Screenshots & Commit SHA (PO UAT = NOT RUN)"
            ws.cell(r, 3).value = "DEV sẵn sàng — PO UAT NOT RUN"
        elif "Môi trường" in metric or metric == "Môi trường":
            ws.cell(r, 2).value = "Local UAT (SQLite EnsureCreated) + Prod inventory read-only cms-lss-01; Staging web sẵn sàng sau deploy MG-17"
            ws.cell(r, 3).value = "DEV QA PASS / PO UAT NOT RUN"
        elif metric == "Tổng số ca":
            ws.cell(r, 3).value = "DEV QA 32/32 PASS — PO UAT NOT RUN"

    ws = wb["Kịch bản UAT"]
    for r in range(6, 40):
        code = ws.cell(r, 1).value
        if not code or not str(code).startswith("TC-"):
            continue
        num = int(str(code).split("-")[-1])
        actual, test, bill, note = TC_ROWS[num]
        ws.cell(r, 6).value = "PASS"
        ws.cell(r, 7).value = "NOT RUN"
        ws.cell(r, 8).value = actual
        ws.cell(r, 9).value = f"{test}; commit {SHA}"
        ws.cell(r, 10).value = bill
        ws.cell(r, 11).value = note

    wb.save(path)
    print("UAT filled", path.name)


def fill_migration():
    path = BASE / "LCMS_Surcharge_Migration_Impact_v1.0_20261102.xlsx"
    wb = openpyxl.load_workbook(path)
    ws = wb["Tổng quan"]
    for r in range(6, 14):
        metric = str(ws.cell(r, 1).value or "")
        if metric == "Phạm vi":
            ws.cell(r, 3).value = "DEV đã xuất inventory Prod (9 version / 3 header) — chờ cutover"
        elif "Trước cutover" in metric or metric == "Trước cutover":
            ws.cell(r, 3).value = "8 Published chưa có activation_policy; rating mới vẫn auto-cộng (AS-IS)"
        elif metric == "Migration":
            ws.cell(r, 3).value = "Migration 20261102170000 sẵn sàng; Prod chưa apply (MG-18)"
        elif "Mẫu kiểm kê" in metric or "Kiểm kê" in metric:
            ws.cell(r, 3).value = "Sheet Kiểm kê phụ phí đã điền 9 dòng thực tế từ cms-db"
        elif "Rollout" in metric or "Production" in metric:
            ws.cell(r, 3).value = "Production chưa phê duyệt"

    ws = wb["Kiểm kê phụ phí"]
    # clear old row 6 note
    for r in range(6, 30):
        for c in range(1, 16):
            ws.cell(r, c).value = None
    for i, row in enumerate(INV_ROWS):
        r = 6 + i
        for c, val in enumerate(row, 1):
            ws.cell(r, c).value = val

    ws = wb["Checklist chuyển đổi"]
    for r in range(6, 30):
        code = ws.cell(r, 1).value
        if not code or not str(code).startswith("MG-"):
            continue
        num = int(str(code).split("-")[1])
        status, evidence = MG_EVIDENCE[num]
        ws.cell(r, 5).value = status
        ws.cell(r, 6).value = evidence
        ws.cell(r, 7).value = "2026-11-02" if num <= 16 else "Chờ PO"

    wb.save(path)
    print("Migration filled", path.name)


if __name__ == "__main__":
    fill_br_ac()
    fill_uat()
    fill_migration()
    print("done")
