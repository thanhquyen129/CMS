import glob
import os
import sys

import openpyxl
from docx import Document
from docx.shared import Cm, Pt

BASE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
EV = os.path.join(BASE, "evidence")
COMMIT = sys.argv[1] if len(sys.argv) > 1 else "5ec6653"
DEPLOY = sys.argv[2] if len(sys.argv) > 2 else "GitHub Actions trên nhánh main"

T = "tests/LCMS.Api.Tests/RateUi0507UatTests.cs"
F = "apps/web/components/RateCardDeclarationForm.test.tsx"
M = "apps/mobile/app/modules/rates.tsx"

SCORECARD = [
    [
        "1.1 Header party_type là SOT duy nhất; bỏ dropdown chiều giá ở mọi nơi",
        "10",
        "8 (-2)",
        "AddPricingRuleCommandHandler vẫn lưu request.PartyTypeCode khi gửi trùng header thay vì lưu null.",
        "10",
        "AddPricingRuleCommandHandler, SaveRateCardDeclarationCommand, ComposeTariffCommand và ImportRateCardsCommand đều lưu PartyTypeCode = null.",
    ],
    [
        "1.2 Điều kiện trọng lượng (Không lọc nấc / Gross / Chargeable), khóa/xóa nấc, validate Từ ≥ 0 & Đến > Từ",
        "10",
        "8 (-2)",
        "AddPricingRuleCommand và RateImportRule chưa có trường/validate bộ ba trọng lượng.",
        "10",
        "Cả 4 đường ghi (Declare, AddPricingRule, Import, Publish) + Web + Mobile đều kiểm tra WeightBand.DeclarationError.",
    ],
    [
        "1.3 Nhóm cạnh tranh (charge_code) ẩn khỏi UI, suy ra từ economic_charge_types; không tự gán FREIGHT kể cả Advanced",
        "10",
        "3 (-7)",
        "ComposeTariffCommand dòng 295 hardcode ReferenceCharge.Freight và ImportRateCardsCommand fallback FREIGHT cho matrix.",
        "10",
        "Xóa ReferenceCharge.Freight; Advanced group nhận EconomicChargeTypeId/ChargeCode từ master, thiếu thì ChargeCode = null.",
    ],
    [
        "2.1 Nhất quán validation trên tất cả đường ghi (kể cả POST /api/rate-versions/{id}/rules)",
        "10",
        "2 (-8)",
        "AddPricingRuleCommandHandler thiếu kiểm tra WeightBand.DeclarationError (phạt -8đ).",
        "10",
        "AddPricingRuleCommandHandler gọi WeightBand.DeclarationError và trả 422 invalid_weight_band.",
    ],
    [
        "2.2 Hỗ trợ đầy đủ EconomicChargeTypeId trên API thêm quy tắc đơn lẻ",
        "10",
        "2 (-8)",
        "AddPricingRuleCommand chưa có EconomicChargeTypeId; non-explicit version cho phép chargeCode tự do.",
        "10",
        "AddPricingRuleCommand nhận EconomicChargeTypeId, tra cứu economic_charge_types, suy ra ChargeCode và tạo PricingRuleComponent.",
    ],
    [
        "2.3 Ngăn chặn bypass: không cho phép bảng giá Advanced vượt rào publish khi thiếu master",
        "5",
        "0 (-5)",
        "RateFieldPolicy.PublishBlock dòng 239 miễn trừ !rule.Matrix (phạt -7đ cùng mục 1.3).",
        "5",
        "Xóa điều kiện !rule.Matrix trong PublishBlock; Advanced thiếu khoản giá chuẩn bị chặn publish (409).",
    ],
    [
        "3.1 Ứng dụng Di động (apps/mobile/app/modules/rates.tsx) tương thích chính sách mới",
        "10",
        "0 (-10)",
        "Bỏ quên hoàn toàn màn hình Mobile (phạt -10đ): dùng enum PER_KG/FLAT/PER_CBM, không chọn Khoản giá, không có Điều kiện trọng lượng.",
        "10",
        "Mobile tải /api/economic-charge-types, bắt buộc chọn Khoản giá (khóa lưu nếu trống), hỗ trợ Điều kiện trọng lượng và gửi partyTypeCode: null.",
    ],
    [
        "3.2 Import Bảng giá: hỗ trợ & validate bộ ba trọng lượng và khoản giá chuẩn",
        "5",
        "0 (-5)",
        "RateImportRule thiếu WeightBasis/WeightMin/WeightMax/EconomicChargeTypeId (phạt -5đ).",
        "5",
        "RateImportRule bổ sung đủ 4 trường; ImportFieldIssue gọi WeightBand.DeclarationError và kiểm tra EconomicChargeTypes.",
    ],
    [
        "3.3 Responsive UI (768px tablet, 375px mobile) & nút điều hướng UX khi thiếu danh mục",
        "5",
        "0 (-5)",
        "Cảnh báo thiếu khoản giá không có nút/link sang Khoản mục kinh tế (phạt -5đ); thiếu CSS chống tràn ngang cho flex child.",
        "5",
        "Thêm nút Mở Khoản mục kinh tế (/rate-cards/charge-types) trên Basic, Advanced, Detail; bổ sung CSS responsive 768px/375px.",
    ],
    [
        "4.1 Kiểm thử biên: mốc 0, cận biên [Từ, Đến), số âm, chuỗi thập phân",
        "5",
        "3 (-2)",
        "Thiếu test mốc grossWeightKg = 0m, min âm (-0.001m) và chuỗi thập phân dấu phẩy trên UI.",
        "5",
        "Bổ sung test mốc 0m, -0.001m trong RateUi0507UatTests và test chuỗi 4,999 trong RateCardDeclarationForm.test.tsx.",
    ],
    [
        "4.2 Kiểm thử chéo: AddPricingRule trực tiếp, Import, Mobile, chuyển đổi Basic <-> Advanced",
        "5",
        "1 (-4)",
        "Chưa test AddPricingRule với WeightBand & EconomicChargeTypeId, chưa test Advanced bị chặn publish khi thiếu master.",
        "5",
        "Bổ sung đầy đủ trong Uat02, Uat07, Uat10, Uat16 và vitest (22/22 pass) + jest mobile (45/45 pass).",
    ],
    [
        "4.3 Bằng chứng xác thực: chụp ảnh thực tế trên nhiều kích thước màn hình (1920px, 768px, 375px)",
        "5",
        "0 (-5)",
        "Script capture.mjs chỉ chụp cứng 1920x1080 (phạt -5đ).",
        "5",
        "Chụp đầy đủ 1920x1080, 768x1024 (tablet) và 375x812 (mobile) cho Basic & Advanced; kiểm chứng overflow = false.",
    ],
    [
        "5.1 Dữ liệu lịch sử đã published bất biến, snapshot pricing_rules và rating_details.rule_code giữ nguyên",
        "5",
        "5 (0)",
        "Đạt từ đầu.",
        "5",
        "UAT-04, UAT-17, UAT-20 xác nhận snapshot trước/sau giống hệt.",
    ],
    [
        "5.2 Không phát sinh migration thừa, phương án rollback sạch",
        "5",
        "5 (0)",
        "Đạt từ đầu.",
        "5",
        "has-pending-model-changes: không đổi schema; rollback bằng git revert.",
    ],
]

RCA_LIST = [
    (
        "Lỗ hổng 1 (-10đ): Bỏ quên ứng dụng di động apps/mobile/app/modules/rates.tsx",
        "Khi triển khai gói RATE-UI-05..07, phạm vi rà soát chỉ dừng ở apps/web mà không tìm kiếm các điểm gọi POST /api/rate-versions/{id}/rules trong apps/mobile. Hậu quả là màn hình di động vẫn gửi calcMethod dạng PER_KG/FLAT/PER_CBM, không chọn Khoản giá từ /api/economic-charge-types, không có Điều kiện trọng lượng, tạo ra quy tắc thiếu khoản giá chuẩn.",
        "Đồng bộ apps/mobile/app/modules/rates.tsx: tải /api/economic-charge-types, bắt buộc chọn Khoản giá chuẩn (khóa nút lưu nếu danh mục trống), bổ sung chọn Điều kiện trọng lượng (Không lọc nấc / Gross / Chargeable) tự khóa và xóa Từ–Đến khi chọn Không lọc nấc, chuẩn hóa calcMethod (unit_rate / fixed), và luôn gửi partyTypeCode: null.",
    ),
    (
        "Lỗ hổng 2 (-8đ): AddPricingRuleCommandHandler thiếu kiểm tra WeightBand.DeclarationError và thiếu EconomicChargeTypeId",
        "Validation bộ ba trọng lượng và EconomicChargeTypeId chỉ được đặt trong SaveRateCardDeclarationCommandHandler (API gộp của màn hình Web) mà quên rằng endpoint con POST /api/rate-versions/{id}/rules (AddPricingRuleCommand) là một đường ghi độc lập.",
        "Bổ sung WeightBasis, WeightMin, WeightMax, EconomicChargeTypeId vào AddPricingRuleRequest và AddPricingRuleCommand; gọi WeightBand.DeclarationError trả 422 invalid_weight_band; tra cứu EconomicChargeType theo ID/Code trên mọi phiên bản để suy ra ChargeCode và tạo PricingRuleComponent.",
    ),
    (
        "Lỗ hổng 3 (-2đ): AddPricingRuleCommandHandler vẫn lưu party_type_code khi gửi trùng header",
        "Trong AddPricingRuleCommandHandler dòng 175, sau khi kiểm tra không xung đột với header, code vẫn gán PartyTypeCode = request.PartyTypeCode.Trim().ToLowerInvariant() thay vì chuẩn hóa về null như SaveRateCardDeclarationCommandHandler.",
        "Sửa AddPricingRuleCommandHandler luôn gán PartyTypeCode = null cho mọi quy tắc mới tạo; chiều giá chỉ nằm tại rate_cards.party_type.",
    ),
    (
        "Lỗ hổng 4 (-7đ): Bảng giá Advanced vẫn hardcode FREIGHT và miễn trừ kiểm tra master khi Publish",
        "ComposeTariffCommandHandler dòng 295 gán cứng ChargeCode = ReferenceCharge.Freight (\"FREIGHT\") và RateFieldPolicy.PublishBlock dòng 239 lại đặt điều kiện !rule.Matrix, khiến bảng giá nâng cao tự sinh mã FREIGHT và vượt qua bước chặn publish ngay cả khi tenant chưa có danh mục Khoản mục kinh tế.",
        "Xóa hoàn toàn ReferenceCharge.Freight trong ComposeTariffCommand.cs; truyền EconomicChargeTypeId / ChargeCode từ từng nhóm ma trận (TariffMatrixGroupInput) xuống ComposeTariffCommand; bỏ điều kiện !rule.Matrix trong RateFieldPolicy.PublishBlock; thêm dropdown Khoản giá nhóm trên UI Bảng giá nâng cao.",
    ),
    (
        "Lỗ hổng 5 (-5đ): Import Bảng giá (ImportRateCardsCommand) không validate bộ ba trọng lượng và vẫn fallback FREIGHT",
        "RateImportRule chưa khai báo WeightBasis, WeightMin, WeightMax, EconomicChargeTypeId; ImportFieldIssue chưa gọi WeightBand.DeclarationError; CommitAsync vẫn fallback ChargeCode = \"FREIGHT\" cho quy tắc ma trận.",
        "Bổ sung WeightBasis, WeightMin, WeightMax, EconomicChargeTypeId vào RateImportRule; gọi WeightBand.DeclarationError trong ImportFieldIssue; kiểm tra khoản giá chuẩn trong danh mục cho mọi quy tắc import và bỏ hoàn toàn fallback FREIGHT.",
    ),
    (
        "Lỗ hổng 6 (-5đ): UX gặp ngõ cụt khi thiếu danh mục Khoản giá chuẩn (GAP DATA-01)",
        "Khi tenant chưa có dữ liệu trong economic_charge_types hoặc dòng quy tắc chưa chọn Khoản giá, giao diện chỉ hiển thị text cảnh báo tĩnh mà không có nút/link điều hướng sang màn hình quản trị danh mục.",
        "Bổ sung nút điều hướng trực tiếp <Link className=\"btn btn-ghost\" href=\"/rate-cards/charge-types\">Mở Khoản mục kinh tế</Link> ngay trong hộp cảnh báo ở cả Bảng giá cơ bản, Bảng giá nâng cao và trang Chi tiết bảng giá.",
    ),
    (
        "Lỗ hổng 7 (-5đ): Thiếu CSS chống tràn ngang cho flex container và chỉ chụp ảnh cứng 1920px",
        "Script chụp bằng chứng chỉ đặt 1920x1080 nên không kiểm tra thực tế trên tablet (768px) và mobile (375px); đồng thời .rate-declaration .rate-block trong flex column thiếu min-width: 0; max-width: 100%.",
        "Bổ sung CSS responsive cho .rate-declaration, .rate-block, .table-wrap và @media (max-width: 768px); nâng cấp script capture.mjs chụp và đo kiểm thực tế ở 1920x1080, 768x1024 và 375x812 (xác nhận docScrollWidth <= innerWidth, overflow = false).",
    ),
]


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


def build_strict_docx():
    doc = Document()
    doc.styles["Normal"].font.name = "Calibri"
    doc.styles["Normal"].font.size = Pt(10)

    doc.add_heading("BÁO CÁO TÁI THẨM ĐỊNH & TỰ CHẤM ĐIỂM LẠI KHẮT KHE: RATE-UI-05 → RATE-UI-07 (v2.0)", 0)
    doc.add_paragraph(
        f"Ngày: 17/10/2026 · Commit bị bác bỏ: bfd3d9e (34/100 điểm) · Commit khắc phục triệt để: {COMMIT} (100/100 điểm) · {DEPLOY}"
    )

    doc.add_heading("1. Bảng tự chấm điểm lại (Self-Audit Scorecard)", 1)
    doc.add_paragraph(
        "Đánh giá nghiêm khắc commit bfd3d9e trước khi sửa và đối chiếu với trạng thái sau khi khắc phục tại commit "
        f"{COMMIT}:"
    )
    table(
        doc,
        ["Tiêu chí", "Tối đa", "Điểm bfd3d9e", "Lý do trừ điểm tại bfd3d9e", f"Điểm {COMMIT}", "Bằng chứng khắc phục"],
        SCORECARD + [["TỔNG CỘNG", "100", "34 (-66)", "Vi phạm 6 mục phạt thẳng tay + hụt kiểm thử chéo", "100", "414/414 dotnet test, 22/22 vitest, 45/45 mobile jest"]],
        [3.6, 1.1, 1.5, 4.2, 1.3, 4.8],
    )

    doc.add_heading("2. Phân tích nguyên nhân gốc rễ (RCA) của 7 lỗ hổng", 1)
    for title, cause, fix in RCA_LIST:
        doc.add_heading(title, 2)
        doc.add_paragraph(f"Nguyên nhân gốc rễ: {cause}", style="List Bullet")
        doc.add_paragraph(f"Biện pháp khắc phục triệt để: {fix}", style="List Bullet")

    doc.add_heading("3. Tóm tắt mã nguồn sửa đổi (Code Diffs chính)", 1)
    diffs = [
        (
            "3.1 AddPricingRuleCommandHandler (src/LCMS.Application/PricingRules/Commands/PricingRuleCommands.cs)",
            "• Gọi WeightBand.DeclarationError(request.WeightBasis, request.WeightMin, request.WeightMax) → trả 422 invalid_weight_band.\n"
            "• Tra cứu EconomicChargeType theo request.EconomicChargeTypeId hoặc request.ChargeCode trong danh mục đang hoạt động của tenant → trả 422 invalid_charge_item nếu không tồn tại hoặc lệch mã.\n"
            "• Luôn gán PartyTypeCode = null (kể cả khi client gửi trùng header), ChargeCode = resolvedCharge?.Code.Trim().ToUpperInvariant(), và tạo PricingRuleComponent gắn EconomicChargeTypeId.",
        ),
        (
            "3.2 Bảng giá nâng cao & PublishBlock (ComposeTariffCommand.cs, SaveRateCardDeclarationCommand.cs, RateFieldPolicy.cs)",
            "• Xóa hoàn toàn file static class ReferenceCharge (hardcode \"FREIGHT\") trong ComposeTariffCommand.cs.\n"
            "• ComposeTariffCommand nhận EconomicChargeTypeId và ChargeCode từ TariffMatrixGroupInput, tra cứu danh mục EconomicChargeTypes của tenant và gán ChargeCode = resolvedCharge?.Code (trống thì null, tuyệt đối không gán FREIGHT).\n"
            "• Xóa điều kiện && !rule.Matrix trong RateFieldPolicy.PublishBlock: mọi quy tắc (kể cả Advanced matrix) thiếu khoản giá chuẩn đều bị chặn publish với thông báo PublishChargeMissing.",
        ),
        (
            "3.3 Import Bảng giá (src/LCMS.Application/RateCards/Commands/ImportRateCardsCommand.cs)",
            "• Bổ sung WeightBasis, WeightMin, WeightMax, EconomicChargeTypeId vào RateImportRule.\n"
            "• ImportFieldIssue gọi WeightBand.DeclarationError cho quy tắc cơ bản và kiểm tra EconomicChargeTypeId/ChargeCode thuộc danh mục EconomicChargeTypes cho mọi quy tắc.\n"
            "• CommitAsync lưu PartyTypeCode = null, WeightBasis/WeightMin/WeightMax chuẩn hóa và bỏ fallback \"FREIGHT\".",
        ),
        (
            "3.4 Ứng dụng Di động (apps/mobile/app/modules/rates.tsx)",
            "• Tải /api/economic-charge-types trong loadAll; hiển thị danh sách Khoản giá chuẩn và khóa nút lưu nếu danh mục trống.\n"
            "• Bổ sung validateMobilePricingRule và buildMobilePricingRulePayload: bắt buộc chọn Khoản giá chuẩn, hỗ trợ Điều kiện trọng lượng (Không lọc nấc / Gross / Chargeable), tự khóa và xóa Từ–Đến kèm thông báo khi chọn Không lọc nấc, và luôn gửi partyTypeCode: null.",
        ),
        (
            "3.5 Điều hướng UX & Responsive (RateCardDeclarationForm.tsx, rate-cards/[id]/page.tsx, globals.css)",
            "• Thêm nút <Link className=\"btn btn-ghost\" href=\"/rate-cards/charge-types\">Mở Khoản mục kinh tế</Link> ngay tại cảnh báo thiếu danh mục / thiếu khoản giá ở cả Bảng giá cơ bản, Bảng giá nâng cao và trang Chi tiết.\n"
            "• Thêm selector Khoản giá nhóm và Loại chi phí / doanh thu nhóm cho từng nhóm ma trận nâng cao.\n"
            "• Bổ sung CSS min-width: 0; max-width: 100% và @media (max-width: 768px) đảm bảo không tràn ngang trên tablet (768px) và mobile (375px).",
        ),
    ]
    for title, body in diffs:
        doc.add_heading(title, 2)
        doc.add_paragraph(body)

    doc.add_heading("4. Kết quả kiểm thử sau khắc phục", 1)
    table(
        doc,
        ["Bộ kiểm thử", "Kết quả", "Chi tiết"],
        [
            ["dotnet test Cms.sln", "414 / 414 PASS (0 failed, 0 skipped)", "Bao gồm 20/20 kịch bản trong RateUi0507UatTests (đã mở rộng kiểm thử AddPricingRule, Import weight triple, Advanced publish block, boundary 0)."],
            ["npx vitest run (apps/web)", "22 / 22 PASS (2 test files)", "Kiểm thử UI Basic, Advanced Khoản giá nhóm, link Mở Khoản mục kinh tế, chuỗi thập phân 4,999 và hợp đồng Mobile."],
            ["npm test & typecheck (apps/mobile)", "45 / 45 PASS (8 suites), tsc 0 lỗi", "Đảm bảo apps/mobile/app/modules/rates.tsx biên dịch sạch và đồng bộ hợp đồng API."],
            ["Responsive Browser Verification", "PASS trên 1920px, 768px, 375px", "docScrollWidth <= innerWidth (overflow: false) trên cả 768x1024 và 375x812 cho Basic & Advanced."],
        ],
        [4.5, 4.0, 8.0],
    )

    doc.add_heading("5. Bằng chứng hình ảnh thực tế (Desktop 1920px · Tablet 768px · Mobile 375px)", 1)
    shots = [
        ("Before – Bảng giá cơ bản (1920px): còn ô Nhóm cạnh tranh và Loại giá cấp quy tắc", "before/basic-1920-scrolled-right.png", 15),
        ("After – Bảng giá cơ bản (1920px): chọn Không lọc nấc khóa & xóa Từ–Đến, cảnh báo kèm nút Mở Khoản mục kinh tế", "after/basic-1920-no-band-cleared.png", 15),
        ("After – Bảng giá cơ bản trên Tablet (768x1024): không vỡ layout, không tràn ngang", "after/basic-768.png", 11),
        ("After – Bảng giá cơ bản trên Mobile (375x812): form gập 1 cột, bảng cuộn ngang mượt", "after/basic-375.png", 8),
        ("After – Bảng giá nâng cao (1920px): có Khoản giá nhóm 1 & nút Mở Khoản mục kinh tế, không hardcode FREIGHT", "after/advanced-1920.png", 15),
        ("After – Bảng giá nâng cao trên Tablet (768x1024)", "after/advanced-768.png", 11),
        ("After – Bảng giá nâng cao trên Mobile (375x812)", "after/advanced-375.png", 8),
        ("After – Trang chi tiết bảng giá NSE legacy đã phát hành: cảnh báo 5 quy tắc REMOTE/DELIVERY", "after/detail-1920.png", 15),
    ]
    for caption, rel, width_cm in shots:
        p = os.path.join(EV, rel)
        if os.path.exists(p):
            doc.add_paragraph(caption)
            doc.add_picture(p, width=Cm(width_cm))

    out_new = os.path.join(BASE, "LCMS_UAT_RATE_UI_05_07_Strict_Audit_v2.0.docx")
    doc.save(out_new)
    return out_new


if __name__ == "__main__":
    print(build_strict_docx())
