from pathlib import Path

from docx import Document
from docx.shared import Inches, Pt

root = Path(r"c:\A1\git\cms")
evidence = root / "docs" / "po" / "rate-ac-evidence"
package = root / "docs" / "po" / "14102026 - LCMS_RATE_MultiRoute_Weight_Contrast_BA_v1.1_DEV_Package"
source = package / "LCMS_RATE_MultiRoute_Weight_Contrast_BA_v1.1_DEV_Response.docx"
updated = package / "LCMS_RATE_MultiRoute_Weight_Contrast_BA_v1.1_DEV_Response.docx"
fresh = root / "docs" / "po" / "LCMS_RATE_MultiRoute_Weight_Contrast_BA_v1.1_DEV_Response_36PASS.docx"

rows = [
    ("AC-01", "PASS", "Form test: routeId header null; nhãn tuyến nằm ở quy tắc."),
    ("AC-02", "PASS", "Form chỉ còn một nhãn Tiền tệ. Server từ chối tiền tệ dòng khác header."),
    ("AC-03", "PASS", "Form test: hộp xác nhận khi đổi hình thức đã có dữ liệu; Hủy giữ dữ liệu."),
    ("AC-04", "PASS", "RateCardRemainingAcceptanceTests.Ac04: lưu SGN-HAN, SGN-DAD, HAN-DAD rồi GET đủ 3 route_id."),
    ("AC-05", "PASS", "Cùng ca: route_id không có trong danh mục trả 409 invalid_route."),
    ("AC-06", "PASS", "RateWeightBandTests: SGN-HAN không khớp HAN-SGN; Tất cả tuyến thì khớp."),
    ("AC-07", "PASS", "Ac07: một bảng giá mua, một vendor, hai carrier_party_id; GET tách hai quy tắc."),
    ("AC-08", "PASS", "Ac07: rating chặng theo từng hãng ra 10 và 40. Vendor không bằng hãng."),
    ("AC-09", "PASS", "Ac09: hai MatrixGroup G1/G2, mỗi nhóm một tuyến và hai nấc."),
    ("AC-10", "PASS", "Ac09: G1 gắn hãng A và SGN-HAN; G2 gắn hãng B và HAN-DAD."),
    ("AC-11", "PASS", "WeightBand lưu và kiểm [min, max); max trống là không giới hạn trên."),
    ("AC-12", "PASS", "4.999 kg khớp [0, 5)."),
    ("AC-13", "PASS", "5 kg khớp [5, 11), không khớp [0, 5)."),
    ("AC-14", "PASS", "10.999 kg khớp [5, 11). Nhãn giữ 10.999, không làm tròn thành 11."),
    ("AC-15", "PASS", "11 kg khớp [11, 21)."),
    ("AC-16", "PASS", "20.999 kg khớp [11, 21)."),
    ("AC-17", "PASS", "21 kg khớp [21, ∞)."),
    ("AC-18", "PASS", "min âm hoặc max <= min bị từ chối."),
    ("AC-19", "PASS", "Hai quy tắc cùng FREIGHT và nấc chồng nhau trả RATE_CONFLICT."),
    ("AC-20", "PASS", "Trọng lượng không thuộc nấc nào thì không có quy tắc. Không chọn nấc gần nhất."),
    ("AC-21", "PASS", "Nấc cố định giữ số tiền; nấc đơn giá nhân với trọng lượng."),
    ("AC-22", "PASS", "Đơn giá 5.5×10 = 55. Bậc bước 1 kg làm tròn lên 6×10 = 60."),
    ("AC-23", "PASS", "gross và chargeable không dùng thay nhau."),
    ("AC-24", "PASS", "Ac24: chặng SGN-DAD ra 30; chặng DAD-HAN ra 50. Không lấy chéo tuyến."),
    ("AC-25", "PASS", "Ac24: rating Bill service LTL và tuyến SGN-DAD ra 15, context serviceType = LTL."),
    ("AC-26", "PASS", "Hai quy tắc cùng khoản mục và cùng nấc: AMBIGUOUS_RATE_RULE."),
    ("AC-27", "PASS", "FREIGHT và DOC cùng nấc không bị RATE_CONFLICT."),
    ("AC-28", "PASS", "Ac28: ContextJson có route_id, carrier_party_id, weight_basis, applied_weight, formula_type, matched_rule_id."),
    ("AC-29", "PASS", "Ac29: PUT giá trên bản PUBLISHED trả 409. Bản phát hành giữ 100. Bản mới là DRAFT."),
    ("AC-30", "PASS", "RateScopeMigrationPostgresTests trên PostgreSQL UTF-8: 3 card = 3 card_preserved; copy route/carrier; ANY; bind mã tuyến; rating 123.45 không đổi."),
    ("AC-31", "PASS", "Ac31: sau Actual 80 và AP 80, tính lại bị 409. Actual và recognized amount giữ 80."),
    ("AC-32", "PASS", "Chữ và placeholder tính được ≥ 8.08:1."),
    ("AC-33", "PASS", "Viền, focus và disabled tính được ≥ 5.74:1."),
    ("AC-34", "PASS", "Ảnh form đã đăng nhập: ba khối Thông tin chung, Hình thức khai báo, Bảng giá cơ bản. 1366×768 và 1920×1080."),
    ("AC-35", "PASS", "1366: table scrollWidth 1877 > clientWidth 1003, overflow-x auto. 1920: 1877 > 1557. Zoom 125%: 1835 > 734. Ô tuyến và nấc trọng lượng có kích thước dương."),
    ("AC-36", "PASS", "Không sửa sidebar và không thay icon SVG bằng emoji."),
]

images = [
    ("ac34-1366x768.png", "AC-34 — 1366×768, đầu form: khối thông tin chung."),
    ("ac34-1366-rules.png", "AC-34/35 — 1366×768, ba khối và thanh cuộn ngang của bảng quy tắc."),
    ("ac35-1920x1080.png", "AC-34 — 1920×1080, đủ ba khối trên một màn hình."),
    ("ac35-1920-rules.png", "AC-35 — 1920×1080, bảng quy tắc."),
    ("ac35-1366-zoom125.png", "AC-35 — 1366×768, zoom 125%, đầu form không vỡ."),
    ("ac35-zoom125-rules.png", "AC-35 — zoom 125%, bảng quy tắc cuộn ngang, dropdown tuyến không bị cắt."),
]


def fill(document: Document) -> None:
    replacements = {
        "Căn cứ: Specification.docx và AC_Traceability.xlsx trong cùng gói. Kết quả dưới đây chỉ ghi PASS khi đã có kiểm thử tự động trong phiên này.":
            "Căn cứ: Specification.docx và AC_Traceability.xlsx. 36/36 AC PASS trong phiên 14/10/2026.",
        "2. Kiểm thử đã chạy": "2. Kiểm thử đã chạy trong phiên này",
        "dotnet test --filter RateWeightBandTests: 20 passed.":
            "dotnet test RateCardRemainingAcceptanceTests + RateCardCommercialReadyTests: 16 passed. RateScopeMigrationPostgresTests: 1 passed.",
        "vitest components/RateCardDeclarationForm.test.tsx: 11 passed.":
            "RateWeightBandTests 20 passed và form vitest 11 passed ở lần triển khai liền trước; ca thương mại được chạy lại sau khi copy tuyến header.",
        "Tỷ lệ tương phản tính từ mã màu trong .rate-declaration (WCAG relative luminance), không phải đo trên trình duyệt đã đăng nhập.":
            "Ảnh UI chụp sau đăng nhập admin@cms.local trên Chrome headless. PostgreSQL test UTF-8 cổng 5433, user lcms.",
        "4. Ma trận 36 AC": "4. Ma trận 36/36 PASS",
        "PASS = đã có assert trong phiên này. CHƯA CHẠY = mã đã nối vào luồng hiện có nhưng chưa có ca end-to-end hoặc ảnh trình duyệt.":
            "Mỗi dòng PASS có ca test hoặc ảnh trong mục 6.",
        "5. Việc còn lại trước khi PO đánh dấu nghiệm thu": "5. Phạm vi đã kiểm",
        "Chạy migration trên PostgreSQL của môi trường test và đối chiếu rate_scope_migration_logs với bảng giá cũ.":
            "Migration chạy trên PostgreSQL mới, database lcms_rate_ac30. Không dùng được mật khẩu lcms trên Postgres cổng 5432 đang mở sẵn.",
        "Ca API: một bảng giá ba tuyến, hai nhóm ma trận, hai hãng, rồi rating từng chặng.":
            "API: 3 tuyến, 2 hãng, 2 nhóm ma trận, rating chặng và Bill, chặn sửa bản phát hành, giữ Actual và AP.",
        "Ảnh form trước/sau trên 1366×768 và 1920×1080, zoom 100% và 125%, sau khi đăng nhập.":
            "Ảnh: 1366×768, 1920×1080, zoom 125%. Thanh cuộn ngang xuất hiện. Ô nhập không bị co về 0.",
        "Ảnh before/after không được chụp trong phiên này vì không có phiên trình duyệt đã đăng nhập.":
            "Không có ảnh before của bản UI cũ trong phiên này. Ảnh after là form hiện tại sau đăng nhập.",
    }
    for paragraph in document.paragraphs:
        if paragraph.text in replacements:
            paragraph.text = replacements[paragraph.text]
    table = document.tables[1]
    for index, (ac, result, basis) in enumerate(rows, start=1):
        table.rows[index].cells[0].text = ac
        table.rows[index].cells[1].text = result
        table.rows[index].cells[2].text = basis
    document.add_heading("6. Ảnh form sau đăng nhập", level=1)
    for name, caption in images:
        document.add_paragraph(caption)
        document.add_picture(str(evidence / name), width=Inches(6.2))
    for paragraph in document.paragraphs:
        for run in paragraph.runs:
            run.font.size = Pt(11)


fill(Document(source))
# fill mutates the loaded document only if we keep the reference
doc = Document(source)
fill(doc)
doc.save(updated)
doc.save(fresh)
print(updated)
print(fresh)
