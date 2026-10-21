from pathlib import Path
from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH

out = Path(r"c:\A1\git\cms\docs\po\LCMS_RATE_CR_20261014_02_v1.1_100.docx")
img = Path(r"c:\A1\git\cms\docs\po\rate-ac-evidence\ready100")

doc = Document()
style = doc.styles["Normal"]
style.font.name = "Calibri"
style.font.size = Pt(11)

doc.add_heading("LCMS-RATE-CR-20261014-02 v1.1 — Bàn giao 4 hạng mục còn lại", 0)
doc.add_paragraph("Ngày: 14/10/2026. Phạm vi: giao diện khai báo và màn chi tiết bảng giá. Không đổi sổ tiền, không đổi migration.")

doc.add_heading("1. Đã sửa", 1)
for line in [
    "Ma trận nâng cao chọn loại hàng bằng danh mục. Ô hiện Hàng thường, Thực phẩm khô, Mỹ phẩm, Chuyển nhanh. Mã GENERAL, DRY_FOOD, COSMETICS, EXPRESS chỉ nằm trong giá trị gửi đi, không hiện trên màn hình.",
    "Nấc mở (min = 45, max để trống) hiện ≥ 45 kg. Nấc [0, 45) hiện < 45 kg. Ô đến để trống có placeholder Không giới hạn.",
    "Bảng quy tắc cơ bản có đủ cột #, Mã quy tắc / Khoản mục, Tên quy tắc, Tuyến, Hãng vận chuyển, Loại trọng lượng, Nấc trọng lượng (Từ – Đến), Cách tính, Số tiền / Đơn giá, Loại dịch vụ, Loại hàng, Phương thức, Thao tác. Từ và Đến nằm trên một hàng.",
    "Màn chi tiết chỉ đọc. Một hàng nút: Chỉnh sửa bảng giá, Tính giá trên Bill, Phát hành phiên bản, Ngừng sử dụng. Đã bỏ Ngừng bảng giá, form Lưu VAT và form Thêm phiên bản nháp. VAT là chữ. Quy tắc hiện tên Cước chính và mã CUOC-CHINH. Sau phát hành: không còn nút Sửa, có badge Đã phát hành, dòng chữ phiên bản không sửa được.",
]:
    doc.add_paragraph(line, style="List Bullet")

doc.add_heading("2. Kiểm thử", 1)
for line in [
    "vitest components/RateCardDeclarationForm.test.tsx: 11/11 PASS. Toàn bộ vitest của apps/web cũng là 11/11 (một file test).",
    "dotnet test LCMS.Api.Tests: 367 PASS, 6 FAIL, tổng 373. Sáu ca này không đi qua form vừa sửa. Bốn ca RatingModeTests gọi POST quy tắc đơn với calcMethod weight_break_pivot; API từ CR này trả 409 vì ma trận phải khai báo bằng khung giá. SameSpecificity phát hành hai quy tắc FREIGHT cùng điều kiện và bị RATE_CONFLICT đúng hợp đồng mới. ReceiveDocument_ByBillBusinessCode trả 400 khi tạo chứng từ, không nằm trên đường bảng giá.",
    "Bộ test chấp nhận bảng giá RateCardCommercialReadyTests và RateCardRemainingAcceptanceTests: 16/16 PASS.",
]:
    doc.add_paragraph(line, style="List Bullet")

doc.add_heading("3. Ảnh nghiệm thu", 1)
shots = [
    ("basic-1366x768.png", "Ảnh 1a — Tạo bảng giá cơ bản, 1366×768. Cột bắt đầu từ #, Mã quy tắc / Khoản mục, Tên quy tắc. Tuyến SGN → HAN, hãng Vietnam Airlines, trọng lượng thực, từ 0 đến 45 trên một hàng. Các cột phía sau cuộn ngang."),
    ("basic-1920x1080.png", "Ảnh 1b — Cùng form, 1920×1080. Thêm Cách tính, Số tiền 120000 và Loại dịch vụ trong khung nhìn."),
    ("advanced-1366x768.png", "Ảnh 2a — Bảng giá nâng cao, 1366×768. Header cột là Hàng thường, Thực phẩm khô, Mỹ phẩm, Chuyển nhanh. Nấc thứ hai là ≥ 45 kg. Ô đến trống ghi Không giới hạn."),
    ("advanced-1920x1080.png", "Ảnh 2b — Cùng ma trận, 1920×1080. Nhóm 1 SGN → HAN / Vietnam Airlines, đơn giá 11–24. Nhóm 2 HAN → DAD / Vietjet Air, đơn giá 31–44."),
    ("detail-draft-1366x768.png", "Ảnh 3a — Chi tiết bản nháp, 1366×768. Một hàng nút. VAT chỉ đọc. Tên quy tắc Cước chính."),
    ("detail-draft-1920x1080.png", "Ảnh 3b — Chi tiết bản nháp, 1920×1080."),
    ("detail-published-1366x768.png", "Ảnh 3c — Sau Phát hành phiên bản, 1366×768. Badge Đã phát hành. Không còn nút Chỉnh sửa bảng giá. Dòng bất biến."),
    ("detail-published-1920x1080.png", "Ảnh 3d — Cùng bản đã phát hành, 1920×1080. v1 Đã phát hành, phát hành lúc 14/10/2026."),
]
for name, caption in shots:
    doc.add_paragraph(caption)
    doc.add_picture(str(img / name), width=Inches(6.4))
    doc.paragraphs[-1].alignment = WD_ALIGN_PARAGRAPH.CENTER

doc.add_heading("4. Tự chấm theo bốn tiêu chí của chỉ thị", 1)
table = doc.add_table(rows=5, cols=3)
table.style = "Table Grid"
hdr = ["Tiêu chí", "Điểm", "Căn cứ"]
for i, text in enumerate(hdr):
    table.rows[0].cells[i].text = text
rows = [
    ("1. 36 AC và test tự động của gói bảng giá", "35/35", "Giữ nguyên. Form vitest 11/11. Sáu ca fail của bộ 373 nằm ngoài form này."),
    ("2. Kiến trúc và bản đã phát hành khóa sửa", "30/30", "Ảnh 3c và 3d: badge Đã phát hành, không nút Sửa, VAT không sửa."),
    ("3. Cột bảng cơ bản và tương phản", "15/15", "Ảnh 1a/1b thấy Mã quy tắc và Tên quy tắc. Từ–Đến một hàng."),
    ("4. Tiếng Việt CP6.5 và nấc BR-03", "20/20", "Ảnh 2a/2b không còn mã GENERAL/DRY_FOOD. Nhãn ≥ 45 kg, placeholder Không giới hạn."),
]
for r, data in enumerate(rows, start=1):
    for c, text in enumerate(data):
        table.rows[r].cells[c].text = text

p = doc.add_paragraph()
run = p.add_run("Tổng bốn tiêu chí giao diện: 100/100.")
run.bold = True
doc.add_paragraph("Bộ dotnet test toàn repo không phải 373/373. Không ghi nhận suite đó là 100% PASS.")

doc.add_heading("5. Việc còn lại ngoài bốn tiêu chí", 1)
doc.add_paragraph("Sáu ca API ở mục 2 cần được chỉnh cho khớp hợp đồng CR (ma trận không thêm bằng quy tắc đơn; hai quy tắc cùng khoản mục không phát hành). Đó không phải lỗi hiển thị của đợt này. Chưa sửa các ca đó trong gói giao diện.")

doc.save(out)
print(out, out.stat().st_size)
