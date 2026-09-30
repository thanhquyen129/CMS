# -*- coding: utf-8 -*-
"""Định giá LCMS — xuất từ canvas 22/09/2026."""
from pathlib import Path

from docx import Document
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Cm, Pt, RGBColor

OUT = Path(r"c:\A1\git\cms\docs\dinh-gia\LCMS_DinhGia_2026-09-22.docx")


def shade(cell, hex_color: str) -> None:
    tcPr = cell._tc.get_or_add_tcPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:fill"), hex_color)
    shd.set(qn("w:val"), "clear")
    tcPr.append(shd)


def run(p, text, *, bold=False, size=11, color=None, italic=False):
    r = p.add_run(text)
    r.bold = bold
    r.italic = italic
    r.font.size = Pt(size)
    r.font.name = "Calibri"
    r._element.rPr.rFonts.set(qn("w:eastAsia"), "Calibri")
    if color:
        r.font.color.rgb = RGBColor(*color)
    return r


def para(doc, text, *, bold=False, size=11, italic=False, space_after=6):
    p = doc.add_paragraph()
    run(p, text, bold=bold, size=size, italic=italic)
    p.paragraph_format.space_after = Pt(space_after)
    p.paragraph_format.space_before = Pt(0)
    return p


def table(doc, headers, rows, hdr_color="1F4E79", highlight_row=None):
    t = doc.add_table(rows=1 + len(rows), cols=len(headers))
    t.style = "Table Grid"
    t.alignment = WD_TABLE_ALIGNMENT.CENTER
    for i, h in enumerate(headers):
        cell = t.rows[0].cells[i]
        cell.paragraphs[0].clear()
        run(cell.paragraphs[0], h, bold=True, size=9, color=(255, 255, 255))
        shade(cell, hdr_color)
    for ri, row in enumerate(rows, start=1):
        for ci, v in enumerate(row):
            cell = t.rows[ri].cells[ci]
            cell.paragraphs[0].clear()
            bold = highlight_row is not None and ri - 1 == highlight_row
            run(cell.paragraphs[0], str(v), size=9, bold=bold)
        fill = "E8F0E8" if highlight_row is not None and ri - 1 == highlight_row else ("F5F5F5" if ri % 2 == 0 else None)
        if fill:
            for ci in range(len(headers)):
                shade(t.rows[ri].cells[ci], fill)
    doc.add_paragraph()


def main():
    doc = Document()
    for s in doc.sections:
        s.top_margin = s.bottom_margin = Cm(1.6)
        s.left_margin = s.right_margin = Cm(1.6)
        footer = s.footer.paragraphs[0]
        footer.alignment = WD_ALIGN_PARAGRAPH.CENTER
        run(
            footer,
            "LCMS — Định giá tài sản phần mềm  ·  22/09/2026  ·  Không phải thẩm định độc lập",
            size=8,
            color=(100, 100, 100),
        )

    title = doc.add_paragraph()
    title.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run(title, "LCMS — Định giá tài sản phần mềm", bold=True, size=18, color=(31, 78, 121))

    sub = doc.add_paragraph()
    sub.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run(
        sub,
        "Ngày 22/09/2026  ·  Chưa có khách trả tiền  ·  Giá tài sản, không phải giá công ty",
        size=10,
        color=(80, 80, 80),
    )

    para(
        doc,
        "Lớp kiểm soát tài chính logistics neo Bill: chi phí, doanh thu, chứng từ, AP/AR, thanh toán, đối soát, chốt. "
        "Đã triển khai production và UAT spine 13/09/2026. Sản phẩm không thay sổ cái MISA, FAST hay Bravo.",
    )

    doc.add_heading("Kết luận", level=1)
    para(
        doc,
        "Giá hợp lý hôm nay: 2,6 tỷ VND (khoảng 102.000 USD theo tỷ giá kế hoạch 25.500). "
        "Đó là giá mua đứt mã nguồn, bộ spec và bản production đã qua UAT spine, khi người bán không kèm đội ngũ và chưa có hợp đồng thuê bao.",
        bold=True,
    )
    para(
        doc,
        "Công ty đang dùng nội bộ không nên nhận dưới 3,5 tỷ: xây lại sản phẩm tương đương mất khoảng 45 người-tháng.",
    )

    table(
        doc,
        ["Mốc", "Giá trị", "Ý nghĩa"],
        [
            ("Sàn thanh lý code", "1,6 tỷ VND", "Bán code; người mua tự kiểm lại đường tiền, không có người bàn giao"),
            ("Giá hợp lý hôm nay", "2,6 tỷ VND", "Mua đứt sản phẩm đang chạy, chưa có doanh thu khách"),
            ("Trần tránh xây lại", "4,2 tỷ VND", "Chi phí đội người làm lại. Nội bộ không nên nhận dưới khoảng 3,5 tỷ"),
            ("Thay thế kịch bản cao", "6,3 tỷ VND", "Nhà thầu giá cố định, 55 người-tháng, có bảo hành"),
        ],
        highlight_row=1,
    )

    doc.add_heading("Ba cách tính", level=1)
    table(
        doc,
        ["Phương pháp", "Thấp", "Giữa", "Cao", "Khi nào dùng"],
        [
            ("Chi phí thay thế", "2,6 tỷ", "4,2 tỷ", "6,3 tỷ", "Nhà thầu Việt Nam làm lại sản phẩm tương đương"),
            ("Tài sản hôm nay", "1,6 tỷ", "2,6 tỷ", "3,2 tỷ", "Mua bán code. Hệ số 0,38–0,76 lần chi phí thay thế"),
            ("Tránh tự xây", "3,5 tỷ", "4,2 tỷ", "5,0 tỷ", "Chủ đang vận hành, so với thuê đội 8–12 tháng"),
            ("Vốn hóa năm 2", "8 tỷ", "11 tỷ", "12 tỷ", "Chỉ khi có khoảng 15 tenant × 15 triệu/tháng. Không phải giá hôm nay"),
        ],
        highlight_row=1,
    )

    doc.add_heading("Vì sao chi phí thay thế là 4,2 tỷ", level=1)
    para(
        doc,
        "45 người-tháng × 93 triệu VND suất giao khoán blended (lead, backend, frontend, kiểm thử) tại Hà Nội và TP.HCM năm 2026. "
        "Kịch bản thấp: 32 người-tháng × 80 triệu khi spec đã khóa. Kịch bản cao: 55 người-tháng × 115 triệu, giá cố định có bảo hành.",
    )
    para(
        doc,
        "Lịch git chỉ từ 12/09/2026 đến 22/09/2026, 223 commit, hai tác giả. Đó là thời gian nén bằng AI, không phải công sức một đội người. "
        "Định giá lấy công sức đội người, vì người mua phải bảo trì bằng người.",
    )

    doc.add_heading("Người-tháng thay thế, kịch bản giữa", level=2)
    table(
        doc,
        ["Hạng mục", "Người-tháng"],
        [
            ("Định danh, thuê bao, phân quyền, phạm vi dữ liệu", "3,0"),
            ("Danh mục và tỷ giá", "2,5"),
            ("Tham chiếu đơn, Bill, shipment, chặng", "4,0"),
            ("Bảng giá và tính giá", "3,5"),
            ("Chi phí trực tiếp và phân bổ", "3,5"),
            ("Doanh thu và lợi nhuận", "2,5"),
            ("Chứng từ và khớp", "3,0"),
            ("Công nợ phải trả / phải thu", "3,0"),
            ("Thanh toán và thu tiền", "2,5"),
            ("Sao kê và đối soát", "2,5"),
            ("Chốt kỳ và khóa sổ", "2,0"),
            ("Kiểm soát, dashboard, báo cáo", "2,5"),
            ("Giao diện tiếng Việt, khoảng 72 màn", "6,5"),
            ("CI, triển khai, audit", "2,0"),
            ("Kiểm thử đường tiền", "2,0"),
            ("Cộng", "45,0"),
        ],
        highlight_row=15,
    )

    doc.add_heading("Vì sao hôm nay chỉ còn 2,6 tỷ", level=1)
    para(
        doc,
        "4,2 tỷ × 0,62. Phần giữ lại: spine tiền đã UAT trên production, 154 test, đa thuê bao, chốt kỳ bất biến, 22 ADR. "
        "Phần trừ: chưa có khách trả tiền, chưa có sổ cái, chưa chứng minh tách quyền Chi phí khác Doanh thu bằng hai user thật, "
        "giao diện bảng giá còn hở, sản phẩm mới chạy vài tuần.",
    )
    para(
        doc,
        "Sàn 1,6 tỷ là thanh lý: người mua phải tự kiểm lại đường tiền và không có người bàn giao. "
        "Trần 3,2 tỷ của phương pháp tài sản áp khi bên mua dùng ngay và không cần xây kênh bán.",
    )

    doc.add_heading("Căn cứ trong repo", level=1)
    para(doc, "Đếm ngày 22/09/2026, không gồm thư mục build, migration sinh tự động, node_modules.")
    table(
        doc,
        ["Chỉ số", "Số đo"],
        [
            ("Dòng C# sản phẩm", "38.255 (322 file)"),
            ("Dòng C# kiểm thử", "14.450"),
            ("Dòng TypeScript / TSX web", "41.442"),
            ("Dòng CSS", "4.308"),
            ("Màn hình page.tsx", "72"),
            ("Test Fact / Theory", "154"),
            ("Endpoint HTTP (ước lượng Map*)", "khoảng 250"),
            ("Migration EF", "32"),
            ("ADR", "22"),
            ("File domain", "80"),
            ("Commit git", "223 (12–22/09/2026)"),
            ("Production", "cms-sg-01, UAT spine A2–A11 đã đánh dấu 16/09/2026"),
        ],
    )

    doc.add_heading("Độ chín theo lát nghiệp vụ", level=1)
    para(
        doc,
        "Phần trăm là mức đủ để người làm nghề xong việc trên lát đó, chấm từ code và checklist go-live. "
        "Không phải độ phủ pixel so với mockup.",
        italic=True,
        size=10,
    )
    table(
        doc,
        ["Lát nghiệp vụ", "Hoàn thiện vận hành"],
        [
            ("Chốt kỳ", "90%"),
            ("Định danh / thuê bao", "90%"),
            ("Chi phí và phân bổ", "85%"),
            ("AP / AR", "85%"),
            ("Chứng từ", "85%"),
            ("Thanh toán / thu tiền", "85%"),
            ("Danh mục", "85%"),
            ("Đối soát ngân hàng", "80%"),
            ("Doanh thu", "80%"),
            ("Tham chiếu Bill", "75%"),
            ("Tính giá", "70%"),
            ("Báo cáo", "70%"),
            ("Giao diện sát mockup", "65%"),
            ("Đổ sổ cái ERP", "25%"),
            ("Khách trả tiền", "0%"),
        ],
    )

    doc.add_heading("Những gì số này không gồm", level=1)
    table(
        doc,
        ["Khoản", "Tình trạng", "Ảnh hưởng giá"],
        [
            ("Hợp đồng, logo, ARR", "Chưa có", "Không cộng bội số doanh thu"),
            ("Sổ cái, hóa đơn điện tử, thuế", "Ngoài sản phẩm", "Khách vẫn cần phần mềm kế toán"),
            ("TMS, GPS, e-POD", "Cố ý không làm (H-002)", "Không định giá như một bộ TMS"),
            ("Kênh bán và người triển khai", "Chưa có", "Giá công ty chỉ hiện khi có người bán được hàng"),
        ],
    )

    doc.add_heading("Kịch bản tăng giá, chưa dùng để chốt", level=1)
    para(
        doc,
        "Nếu năm thứ hai có 15 forwarder trả 15 triệu đồng mỗi tháng thì ARR khoảng 2,7 tỷ. "
        "Vốn hóa sớm 3–5 lần ARR rơi vào 8–12 tỷ. Điều kiện: có người bán, có triển khai, và giữ được tenant. "
        "Thiếu điều kiện đó thì kịch bản này không cộng vào giá hôm nay.",
    )
    para(
        doc,
        "Giả định giá thuê bao 15 triệu/tháng là mức kế hoạch cho forwarder tầm trung có tổ tài chính, "
        "không phải báo giá đã ký. Suất 80–115 triệu mỗi người-tháng là giả định thị trường Việt Nam 2026.",
    )

    doc.add_heading("Giới hạn", level=1)
    para(
        doc,
        "Tài liệu này ước tính thương mại từ repo tại ngày 22/09/2026. Không phải báo cáo thẩm định độc lập, "
        "không phải ý kiến kiểm toán, và không phải cam kết giá bán. Tỷ giá kế hoạch dùng để quy USD là 25.500 VND/USD.",
        italic=True,
        size=10,
    )

    OUT.parent.mkdir(parents=True, exist_ok=True)
    doc.save(OUT)
    print(OUT)


if __name__ == "__main__":
    main()
