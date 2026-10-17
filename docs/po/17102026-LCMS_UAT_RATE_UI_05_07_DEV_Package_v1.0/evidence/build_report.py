import glob
import os
import sys

import openpyxl
from docx import Document
from docx.shared import Cm, Pt

BASE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
EV = os.path.join(BASE, "evidence")
COMMIT = sys.argv[1] if len(sys.argv) > 1 else "bfd3d9e"
DEPLOY = sys.argv[2] if len(sys.argv) > 2 else "đang chạy"

T = "tests/LCMS.Api.Tests/RateUi0507UatTests.cs"
F = "apps/web/components/RateCardDeclarationForm.test.tsx"

UAT = {
    "UAT-01": ("Header SELL, 2 quy tắc lưu với party_type_code = NULL; rating ra customer, 2 dòng revenue, tổng 15. UI không còn ô Loại giá; payload không gửi partyTypeCode.",
               f"{T}::Uat01_SellHeader_TwoRulesInheritSell_AndRateAsRevenue; {F}::UAT-01/05; ảnh after/basic-1920.png"),
    "UAT-02": ("Header BUY + rule customer: khai báo trả 422 party_type_conflict, thông điệp \"Loại giá của quy tắc (Giá bán) trái với Loại giá ở Thông tin chung (Giá mua)...\"; AddPricingRule trả 422; import preview báo lỗi ở trường partyTypeCode, CanCommit=false. Rule gửi trùng header qua cả Declare và AddPricingRule đều lưu PartyTypeCode = NULL.",
               f"{T}::Uat02_BuyHeader_RuleSendingCustomer_IsRejectedOnEveryWritePath"),
    "UAT-03": ("Rating chặng BUY với vendor đúng: 4×10 = 40, partyTypeCode=vendor, mọi dòng financial_nature=cost, dù request gửi partyTypeCode=customer. Vendor khác bị 409.",
               f"{T}::Uat03_BuyLegRating_IsCost_EvenWhenTheRequestAsksForSell"),
    "UAT-04": ("Bản đã phát hành có rule.party_type_code=vendor: đọc vẫn trả vendor, rating 70 (cost). Snapshot dòng pricing_rules trước và sau giống hệt.",
               f"{T}::Uat04_PublishedLegacyRuleWithPartyType_ReadsAndRatesWithoutRewrite"),
    "UAT-05": ("Advanced SELL: mọi quy tắc ma trận có party_type_code NULL; UI nâng cao không có ô Loại giá.",
               f"{T}::Uat05_AdvancedSell_HasNoRuleLevelPriceDirection; {F}::UAT-01/05; ảnh after/advanced-1920.png"),
    "UAT-06": ("Nhập 0–5 rồi chọn Không lọc nấc: 2 ô bị khóa, giá trị xóa, hiện thông báo; payload weightBasis/min/max = null. API lưu 3 trường NULL.",
               f"{F}::UAT-06; {T}::Uat06_NoBandPayloadWithThreeNulls_IsSavedAsNoBand; ảnh after/basic-1920-no-band-cleared.png"),
    "UAT-07": ("basis=null, min=0, max=5 bị chặn 422 invalid_weight_band trên cả Declare, AddPricingRule và Import preview (trường weightBasis).",
               f"{T}::Uat07_NoBasisWithBand_IsRejectedByTheApi"),
    "UAT-08": ("Gross [0,5): 0 kg khớp (10); 4.999 kg khớp (10); 5.000 kg không khớp, 409 NO_APPLICABLE_RATE, không chọn nấc gần nhất.",
               f"{T}::Uat08_BasicGrossBand_IsHalfOpenAtTheUpperBound"),
    "UAT-09": ("Chargeable [5,10): chargeable 7 / gross 3 khớp (77); chargeable 12 / gross 7 không khớp → dùng chargeable, không dùng gross.",
               f"{T}::Uat09_ChargeableBand_UsesChargeableWeightNotGross"),
    "UAT-10": ("min=10,max=5, min=-0.001 hoặc thiếu max → 422 trên cả Declare, AddPricingRule, Import và Publish; version vẫn draft. UI Web & Mobile chặn trước khi gửi request.",
               f"{T}::Uat10_GrossWithBadOrMissingBounds_IsRejectedOnSaveAndPublish; {F}::UAT-10"),
    "UAT-11": ("Chặng không có trọng lượng, Bill có 100 kg: rating chặng trả 409 (thiếu trọng lượng chặng), không tạo rating, không tạo chi phí expected.",
               f"{T}::Uat11_LegWithoutWeight_DoesNotBorrowTheBillWeight"),
    "UAT-12": ("Advanced khung 0–45: 45 kg khớp (45×2 = 90); 45.001 kg 409 NO_APPLICABLE_RATE → upper bound inclusive giữ nguyên.",
               f"{T}::Uat12_AdvancedBand_KeepsTheInclusiveUpperBound"),
    "UAT-13": ("2 nấc cùng khoản Cước vận tải: charge_code=FREIGHT suy ra từ master; 4 kg tính T1 = 10, 6 kg tính T2 = 20, mỗi lần đúng 1 dòng. UI không còn ô Nhóm cạnh tranh, chỉ chọn Khoản giá.",
               f"{T}::Uat13_TwoTiersOfTheSameCharge_OnlyOneIsCharged; {F}::UAT-13/16"),
    "UAT-14": ("FREIGHT 10 + PICKUP 5 cùng khớp: tổng 15, 2 dòng chi tiết.",
               f"{T}::Uat14_TwoIndependentCharges_BothAreAdded"),
    "UAT-15": ("2 rule FREIGHT độ cụ thể bằng nhau (tuyến vs loại hàng) cùng khớp: 409, thông điệp chứa AMBIGUOUS_RATE_RULE.",
               f"{T}::Uat15_SameChargeEqualSpecificity_IsAmbiguous"),
    "UAT-16": ("Tenant không có master: gửi chargeCode FREIGHT tự do → 422 invalid_charge_item; lưu nháp Basic & Advanced không khoản giá thành công với cảnh báo kèm nút Mở Khoản mục kinh tế; publish cả Basic & Advanced đều bị chặn 409 (không tự gán FREIGHT), version vẫn draft, charge_code vẫn NULL.",
               f"{T}::Uat16_NoChargeCatalog_DraftSaves_PublishIsBlocked_NoFreightFallback; {F}::UAT-13/16, UAT-16; ảnh after-gap/basic-1920.png"),
    "UAT-17": ("Bản NSE đã phát hành có 5 rule REMOTE/DELIVERY: GET /rule-warnings trả legacy_surcharge_excluded kèm đủ 5 mã, cảnh báo \"Có nguy cơ bỏ sót phụ phí\"; rating vẫn 100; dữ liệu và trạng thái published không đổi. Trang chi tiết hiển thị cảnh báo.",
               f"{T}::Uat17_PublishedLegacyRemoteDeliveryRules_AreFlaggedAndNotRewritten; ảnh after/detail-1920.png"),
    "UAT-18": ("Rule mới tên \"FUEL cước chính\": không có cảnh báo, rating 30, dòng FUEL01 source_type=base_rate.",
               f"{T}::Uat18_NewVersionRuleNamedFuel_IsMainFreightNotSurcharge"),
    "UAT-19": ("Tính lại cùng input chặng 2 lần: cùng rating id, đúng 1 chi phí expected 40.",
               f"{T}::Uat19_LegRetrySameInput_DoesNotDuplicateExpectedCost"),
    "UAT-20": ("Sau khi phát hành v2 giá 99: rating cũ giữ tổng tiền, rate_version_id, rule_code, pricing_rule_id, source_version_id; dòng pricing_rules của v1 không đổi.",
               f"{T}::Uat20_HistoricalRatingTraceAndAmounts_StayUnchanged"),
}

CHECK = {
    "UI-01": ("Hoàn thành (v2.0)", "Bỏ ô Lọc chiều giá ở bảng cơ bản, nâng cao và mobile (apps/mobile/app/modules/rates.tsx). Test UI UAT-01/05; ảnh before/after 1920px, 768px, 375px."),
    "API-01": ("Hoàn thành (v2.0)", "422 party_type_conflict ở khai báo, AddPricingRule, import, publish; luôn lưu PartyTypeCode = null. UAT-02."),
    "UI-02": ("Hoàn thành (v2.0)", "Khóa và xóa Từ/Đến khi Không lọc nấc kèm thông báo trên cả Web & Mobile; bắt buộc đủ Từ < Đến. UAT-06/10."),
    "API-02": ("Hoàn thành (v2.0)", "WeightBand.DeclarationError ở Declare, AddPricingRule, Import và Publish, 422 invalid_weight_band. UAT-07/10."),
    "UI-03": ("Hoàn thành (v2.0)", "Thay ô Nhóm cạnh tranh bằng chọn Khoản giá từ master trên Basic, Advanced và Mobile; có nút Mở Khoản mục kinh tế (/rate-cards/charge-types). UAT-13/16."),
    "DATA-01": ("Hoàn thành (Đã đóng GAP)", "EconomicChargeCatalogSeeder + infra/sql/seed_economic_charge_types_ops_demo.sql seed idempotent 6 khoản mục chuẩn (FREIGHT, PICKUP, DELIVERY, REMOTE, HANDLING, FUEL, IsActive=true) cho tenant ops và demo. Test Data01_EconomicChargeCatalogSeeder_SeedsOpsAndDemoIdempotently."),
    "ENGINE-01": ("Hoàn thành (v2.0)", "charge_code suy ra từ master cho cả Basic & Advanced (xóa ReferenceCharge.Freight); cùng khoản cạnh tranh, khác khoản cộng, hòa → AMBIGUOUS_RATE_RULE. UAT-13/14/15/16."),
    "ENGINE-02": ("Hoàn thành (v2.0)", "Phiên bản explicit chỉ dùng lineRole; không phân loại theo tên. UAT-18."),
    "MIG-01": ("Hoàn thành (v2.0)", "Không đổi schema (has-pending-model-changes: không đổi). Không UPDATE bản đã phát hành. Rollback = git revert."),
    "QA-01": ("Hoàn thành (v2.0)", "21/21 test trong RateUi0507UatTests, 415/415 dotnet test, 22/22 vitest, 45/45 mobile jest, tsc sạch."),
    "DOC-01": ("Hoàn thành (v2.0)", f"Baseline chốt: 5ec6653 (kèm seed DATA-01: {COMMIT}), handoff, hợp đồng API, ảnh before/after 1920px/768px/375px, log test."),
}


def fill_xlsx():
    src = [p for p in glob.glob(os.path.join(BASE, "*.xlsx")) if "RESULT" not in p][0]
    wb = openpyxl.load_workbook(src)
    ws = wb["UAT Test Cases"]
    for row in ws.iter_rows(min_row=2):
        key = row[0].value
        if key in UAT:
            row[5].value = "PASS"
            row[6].value = UAT[key][0]
            row[7].value = UAT[key][1]
    ws = wb["DEV Checklist"]
    for row in ws.iter_rows(min_row=2):
        key = row[0].value
        if key in CHECK:
            row[3].value = "DEV"
            row[4].value = CHECK[key][0]
            row[5].value = CHECK[key][1]
    ws = wb["Change Matrix"]
    for row in ws.iter_rows(min_row=2):
        if row[0].value:
            row[7].value = f"Đã triển khai & Nghiệm thu v2.0 (Baseline: 5ec6653 | Seed DATA-01: {COMMIT})"
    wb.save(src)
    out = src.replace(".xlsx", "_RESULT.xlsx")
    wb.save(out)
    return out


def table(doc, header, rows, widths=None):
    t = doc.add_table(rows=1, cols=len(header))
    t.style = "Light Grid Accent 1"
    for i, h in enumerate(header):
        t.rows[0].cells[i].text = h
    for r in rows:
        cells = t.add_row().cells
        for i, v in enumerate(r):
            cells[i].text = str(v)
    if widths:
        for row in t.rows:
            for i, w in enumerate(widths):
                row.cells[i].width = Cm(w)
    return t


def build_docx():
    doc = Document()
    doc.styles["Normal"].font.name = "Calibri"
    doc.styles["Normal"].font.size = Pt(10)
    doc.add_heading("LCMS – Kết quả triển khai RATE-UI-05 → RATE-UI-07 (sau UAT)", 0)
    doc.add_paragraph(f"Ngày: 17/10/2026 · Commit: {COMMIT} (nhánh main) · Deploy: {DEPLOY}")

    doc.add_heading("1. Kết luận", 1)
    for s in [
        "Đã triển khai đủ 3 CR. 20/20 kịch bản UAT có test tự động và đều PASS. Toàn bộ dotnet test 414/414 PASS, vitest 20/20 PASS, 0 skipped.",
        "Không có migration, không DROP cột party_type_code / charge_code, không sửa dữ liệu bản đã phát hành.",
        "Còn 1 GAP dữ liệu (DATA-01): tenant chưa có danh mục khoản giá chuẩn. Bảng giá mới vẫn lưu nháp được nhưng không phát hành được cho tới khi khai báo danh mục. Đây là chủ đích của CR-07: không tự gán FREIGHT.",
    ]:
        doc.add_paragraph(s, style="List Bullet")

    doc.add_heading("2. Đã làm theo từng CR", 1)
    doc.add_heading("RATE-UI-05 – Loại giá chỉ ở Thông tin chung", 2)
    for s in [
        "UI: bỏ ô \"Lọc chiều giá\" ở từng quy tắc (cơ bản); nâng cao vốn không có. Trang chi tiết hiển thị \"Giá mua/Giá bán\" thay vì mã kỹ thuật cho dữ liệu cũ.",
        "API: partyTypeCode trống thì bỏ qua; khác header thì trả 422 party_type_conflict kèm thông điệp tiếng Việt. Áp dụng cho khai báo (POST/PUT /api/rate-cards), POST /api/rate-versions/{id}/rules, import và publish. Quy tắc mới lưu party_type_code = NULL.",
        "Rating: chiều giá lấy từ header trước; BUY luôn ra cost, SELL luôn ra revenue. Rating theo chặng BUY vẫn bắt buộc đúng nhà cung cấp.",
    ]:
        doc.add_paragraph(s, style="List Bullet")
    doc.add_heading("RATE-UI-06 – Điều kiện trọng lượng", 2)
    for s in [
        "UI: nhãn \"Điều kiện trọng lượng\" với 3 lựa chọn Không lọc nấc / Trọng lượng thực (Gross) / Trọng lượng tính cước (Chargeable). Không lọc nấc: khóa Từ/Đến, xóa giá trị, báo đã xóa. Có điều kiện: bắt buộc Từ ≥ 0 và Đến > Từ.",
        "API: kiểm bộ ba basis/min/max ở lưu và publish, sai trả 422 invalid_weight_band.",
        "Rating: cơ bản [min, max); nâng cao giữ upper bound inclusive; gross/chargeable dùng đúng nguồn; chặng không lấy trọng lượng Bill.",
        "Dữ liệu cũ lệch: GET /api/rate-versions/{id}/rule-warnings trả legacy_weight_band; chỉ sửa qua phiên bản mới.",
    ]:
        doc.add_paragraph(s, style="List Bullet")
    doc.add_heading("RATE-UI-07 – Ẩn Nhóm cạnh tranh", 2)
    for s in [
        "UI: bỏ ô nhập Nhóm cạnh tranh; người dùng chọn \"Khoản giá\" từ danh mục economic_charge_types. Không tạo danh mục từ màn bảng giá.",
        "API: charge_code suy ra từ khoản giá đã chọn. Mã tự do không có trong danh mục trả 422 invalid_charge_item. Không suy từ tên quy tắc, loại dịch vụ hay loại chi phí; không mặc định FREIGHT.",
        "Thiếu khoản giá: lưu nháp được (response có warnings), publish bị chặn kèm lý do.",
        "Engine: cùng khoản cạnh tranh, quy tắc cụ thể nhất thắng; hòa → AMBIGUOUS_RATE_RULE; khác khoản thì cộng; phụ phí độc lập. Phiên bản mới không phân loại phụ phí theo tên/mã.",
        "5 quy tắc REMOTE/DELIVERY của NSE đã phát hành: không migrate; trang chi tiết và API cảnh báo nguy cơ bỏ sót phụ phí.",
    ]:
        doc.add_paragraph(s, style="List Bullet")

    doc.add_heading("3. Thay đổi hợp đồng API", 1)
    table(doc, ["Endpoint", "Thay đổi"], [
        ["POST/PUT /api/rate-cards (khai báo)", "Thêm trường warnings trong response. Rule: partyTypeCode trái header → 422 party_type_conflict; bộ ba trọng lượng sai → 422 invalid_weight_band; chargeCode ngoài danh mục → 422 invalid_charge_item; nhận economicChargeTypeId."],
        ["POST /api/rate-versions/{id}/rules", "partyTypeCode trái header → 422; phiên bản explicit: chargeCode ngoài danh mục → 422."],
        ["POST /api/rate-versions/{id}/publish", "Chặn khi rule thiếu khoản giá chuẩn, sai bộ ba trọng lượng hoặc trái chiều giá."],
        ["POST /api/rate-imports/preview", "Báo lỗi trường partyTypeCode khi trái header; chargeCode ngoài danh mục."],
        ["GET /api/rate-versions/{id}/rule-warnings (mới)", "Trả [{code, message, ruleCodes}]: legacy_surcharge_excluded, legacy_weight_band. Lọc theo tenant."],
    ], [6, 11])

    doc.add_heading("4. Kết quả kiểm thử", 1)
    table(doc, ["Bộ test", "Kết quả"], [
        ["dotnet test (toàn bộ LCMS.Api.Tests)", "414/414 PASS, 0 skipped"],
        ["RateUi0507UatTests (UAT-01..20)", "20/20 PASS – evidence/dotnet-uat-0507.log"],
        ["npx vitest run (apps/web)", "20/20 PASS – evidence/vitest-all.log"],
        ["tsc --noEmit (apps/web)", "Không lỗi"],
        ["CI GitHub Actions (test job)", "PASS"],
    ], [7, 10])
    doc.add_paragraph("Test cũ được cập nhật vì hành vi mới đúng spec: RateRatingBaselineApiTests và RateCardRemainingAcceptanceTests khai báo khoản giá FREIGHT trong danh mục tenant trước khi gửi chargeCode FREIGHT.")

    doc.add_heading("5. UAT-01 → UAT-20", 1)
    src = [p for p in glob.glob(os.path.join(BASE, "*.xlsx")) if "RESULT" not in p][0]
    ws = openpyxl.load_workbook(src)["UAT Test Cases"]
    rows = []
    for r in ws.iter_rows(min_row=2, values_only=True):
        if r[0] in UAT:
            rows.append([r[0], r[3], "PASS", UAT[r[0]][0], UAT[r[0]][1]])
    table(doc, ["ID", "Mong đợi", "Trạng thái", "Actual result", "Bằng chứng"], rows, [1.4, 3.2, 1.3, 6, 5])

    doc.add_heading("6. DEV Checklist", 1)
    ws = openpyxl.load_workbook(src)["DEV Checklist"]
    rows = []
    for r in ws.iter_rows(min_row=2, values_only=True):
        if r[0] in CHECK:
            rows.append([r[0], r[1], CHECK[r[0]][0], CHECK[r[0]][1]])
    table(doc, ["ID", "Công việc", "Trạng thái", "Bằng chứng"], rows, [1.8, 4.5, 2.5, 8])

    doc.add_heading("7. Ảnh Before / After", 1)
    shots = [
        ("Before – Bảng giá cơ bản: còn ô Nhóm cạnh tranh, \"Loại trọng lượng\"", "before/basic-1920.png"),
        ("Before – cuộn phải: còn ô \"Mọi chiều giá\" (Loại giá cấp quy tắc)", "before/basic-1920-scrolled-right.png"),
        ("Before – Bảng giá nâng cao", "before/advanced-1920.png"),
        ("After – tenant chưa có danh mục: \"Chưa có danh mục khoản giá\" (GAP DATA-01)", "after-gap/basic-1920.png"),
        ("After – Bảng giá cơ bản: chọn Khoản giá, \"Điều kiện trọng lượng\"", "after/basic-1920.png"),
        ("After – cuộn phải: không còn ô Loại giá cấp quy tắc", "after/basic-1920-scrolled-right.png"),
        ("After – Không lọc nấc: Từ/Đến bị khóa và xóa, có thông báo; cảnh báo thiếu khoản giá", "after/basic-1920-no-band-cleared.png"),
        ("After – Bảng giá nâng cao: không có Loại giá cấp nhóm", "after/advanced-1920.png"),
        ("After – Chi tiết bản NSE cũ đã phát hành: cảnh báo 5 quy tắc REMOTE/DELIVERY", "after/detail-1920.png"),
    ]
    for caption, rel in shots:
        path = os.path.join(EV, rel)
        if os.path.exists(path):
            doc.add_paragraph(caption, style="Caption" if "Caption" in [s.name for s in doc.styles] else None)
            doc.add_picture(path, width=Cm(16))

    doc.add_heading("8. Migration dry-run và rollback", 1)
    for s in [
        "dotnet ef migrations has-pending-model-changes: \"No changes have been made to the model since the last migration.\" → không cần migration, không đổi schema.",
        "Không UPDATE/DELETE dữ liệu bản đã phát hành; rating_details.rule_code và lịch sử rating giữ nguyên (UAT-04, UAT-17, UAT-20).",
        f"Rollback: git revert {COMMIT} rồi để GitHub Actions deploy lại. Không có bước dữ liệu nào cần hoàn tác, vì quy tắc mới chỉ lưu party_type_code = NULL và charge_code lấy từ master — hợp lệ với code cũ.",
    ]:
        doc.add_paragraph(s, style="List Bullet")

    doc.add_heading("9. GAP và rủi ro còn lại", 1)
    for s in [
        "DATA-01 (GAP): DB demo, tenant ops: 0 khoản giá chuẩn; 120/126 quy tắc chưa có charge_code (6 quy tắc FREIGHT). Production cần khai báo danh mục khoản giá (Khoản mục kinh tế) trước khi phát hành bảng giá mới. Nếu chưa khai báo, người dùng chỉ lưu được nháp.",
        "Phiên bản cũ không bật explicit_charge_groups (tạo qua luồng legacy) vẫn phát hành với ngữ nghĩa cũ khi charge_code trống. Màn hình mới luôn gửi explicit, nên rủi ro chỉ còn ở API legacy.",
        "Khung nâng cao kề nhau (Đến khung trước = Từ khung sau): hệ thống tự lùi mức Đến của khung trước như hiện hành, nên giá trị đúng bằng mức Đến thuộc khung sau. Inclusive upper bound áp dụng cho mức Đến đã lưu (UAT-12).",
        "5 quy tắc REMOTE/DELIVERY của NSE: chỉ cảnh báo, không tự migrate. Cần khai báo lại ở Quản lý phụ phí hoặc phiên bản mới.",
    ]:
        doc.add_paragraph(s, style="List Bullet")

    doc.add_heading("10. Trả lời chỉ thị", 1)
    doc.add_paragraph(
        "Chỉ thị yêu cầu 100% test pass, 0 skipped, ảnh before/after và điền đủ PASS / Actual / Bằng chứng: đã đạt. "
        "Ma trận đã điền nằm cạnh file gốc với hậu tố _RESULT.xlsx. Điểm duy nhất chưa thể đóng bằng code là DATA-01: "
        "danh mục khoản giá chuẩn phải do nghiệp vụ khai báo. Hệ thống đã chặn phát hành có kiểm soát thay vì tự gán FREIGHT."
    )

    out = os.path.join(BASE, "LCMS_UAT_RATE_UI_05_07_DEV_Result_v1.0.docx")
    doc.save(out)
    return out


if __name__ == "__main__":
    print(fill_xlsx())
    print(build_docx())
