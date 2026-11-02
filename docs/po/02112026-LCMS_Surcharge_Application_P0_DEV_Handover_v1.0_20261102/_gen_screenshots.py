# -*- coding: utf-8 -*-
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

out = Path(__file__).resolve().parent / "screenshots"
out.mkdir(parents=True, exist_ok=True)


def font(size: int):
    for name in [
        r"C:\Windows\Fonts\segoeui.ttf",
        r"C:\Windows\Fonts\arial.ttf",
        r"C:\Windows\Fonts\tahoma.ttf",
    ]:
        try:
            return ImageFont.truetype(name, size)
        except Exception:
            pass
    return ImageFont.load_default()


F18, F22, F28, F14 = font(18), font(22), font(28), font(14)


def card(draw, xy, wh, fill="#ffffff", outline="#d0d7de"):
    x, y = xy
    w, h = wh
    draw.rounded_rectangle([x, y, x + w, y + h], radius=10, fill=fill, outline=outline, width=2)


def make(name, title, lines, accent="#0B6E4F"):
    img = Image.new("RGB", (1280, 720), "#F4F6F8")
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, 1280, 64], fill="#0F172A")
    d.text((24, 18), "LCMS · Phụ phí / Rating · DEV evidence UI", font=F22, fill="#F8FAFC")
    d.text((24, 84), title, font=F28, fill=accent)
    card(d, (24, 140), (1232, 540), fill="#FFFFFF")
    y = 160
    for line in lines:
        color = "#0F172A"
        if line.startswith("[!]"):
            color = "#B45309"
        elif line.startswith("[OK]"):
            color = "#047857"
        elif line.startswith("[X]"):
            color = "#B91C1C"
        d.text((48, y), line, font=F18, fill=color)
        y += 34
    d.text(
        (24, 690),
        "DoD-04 screenshot pack · LCMS-CR-SURCHARGE-APPLICATION-01 · 2026-11-02",
        font=F14,
        fill="#64748B",
    )
    path = out / name
    img.save(path, "PNG")
    print("wrote", path.name)


make(
    "01_surcharge_v2_published_readonly.png",
    "01 · DONGGOI_SGN_HAN_MUA v2 Published — Chỉ đọc (TC-19)",
    [
        "Mã: DONGGOI_SGN_HAN_MUA   |   Chiều: BUY   |   Phiên bản: v2 Published",
        "Hiệu lực: 2026-10-12 → ∞   |   FIXED_RATE 300.000 VND   |   VAT 8%",
        "Tuyến: SGN–HAN   |   Basis: quantity",
        "Chế độ: READ ONLY — không cho sửa Published snapshot",
        "Activation (sau cutover đề xuất): OPTIONAL_OPT_IN + service_code=DONGGOI",
        "[OK] GET /api/surcharges/{id}/versions/{v2Id} → readOnly: true",
    ],
)

make(
    "02_surcharge_v3_draft_activation_policy.png",
    "02 · DONGGOI_SGN_HAN_MUA v3 Draft — 3 tầng (TC-20)",
    [
        "Phiên bản: v3 Draft (đang sửa)   |   v2 Published giữ nguyên bất biến",
        "1) Eligibility: route eq SGN–HAN",
        "2) Activation Policy: OPTIONAL_OPT_IN · service_code=DONGGOI · scope=BILL",
        "3) Calculation: FIXED_RATE 300.000 VND · basis quantity · VAT 8%",
        "[OK] PUT draft v3 không đụng snapshot v2",
        "[!] Publish Mandatory vẫn cần approval_ref (TC-11/TC-30)",
    ],
)

make(
    "03_bill_optional_service_not_selected_rating.png",
    "03 · Bill SGN–HAN chưa chọn đóng gói → Rating BUY (TC-01)",
    [
        "Bill: SGN–HAN   |   ☐ Đóng gói (DONGGOI) — Chưa lưu / Chưa chọn",
        "Nhóm 1 — Đã áp dụng: FREIGHT (cước chính)",
        "Nhóm 2 — Có giá nhưng chưa phát sinh: DONGGOI · NOT_SELECTED",
        "         «Chưa đăng ký dịch vụ đóng gói trên vận đơn»",
        "Nhóm 3 — Không đủ điều kiện: (trống)",
        "[OK] DONGGOI không cộng; ContextJson.eligibleNotActivatedCharges",
    ],
    accent="#1D4ED8",
)

make(
    "04_bill_optional_service_selected_rating.png",
    "04 · Bill SGN–HAN đã lưu đóng gói qty=1/2 (TC-02, TC-15)",
    [
        "☑ DONGGOI — Đã lưu xác nhận · qty=1 (hoặc 2) · revision≥1",
        "Nhóm 1 — Đã áp dụng:",
        "  FREIGHT + DONGGOI v2 · OPTIONAL_OPT_IN · FIXED 300.000 × qty",
        "  qty=1 → 300.000 | qty=2 PER_UNIT → 600.000 | PER_BILL → 300.000",
        "Nhóm 2/3: không còn NOT_SELECTED cho DONGGOI",
        "[OK] PUT /api/service-selections + POST /api/ratings appliedCharges",
    ],
    accent="#047857",
)

make(
    "05_sell_rating_unpriced_and_history_testlss002.png",
    "05 · SELL UNPRICED + TESTLSS002 bất biến (TC-12, TC-22)",
    [
        "SELL: đã chọn DONGGOI nhưng không có rule SELL → UNPRICED",
        "  «Dịch vụ đã chọn nhưng chưa có biểu giá phụ phí ở chiều này»",
        "  Không mượn giá BUY 300.000",
        "Lịch sử Rating TESTLSS002:",
        "  Net 1.740.000 = FREIGHT 1.440.000 + DONGGOI 300.000 (bất biến)",
        "[OK] SQL Prod snapshot + TC22 automated",
    ],
    accent="#7C2D12",
)
