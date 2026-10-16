"""
Generates high-resolution App Store and Google Play Store screenshots for LSS LCMS Mobile.
Resolution: 1080 x 2400 px (Standard 9:20 Store Aspect Ratio)
Color Palette: LSS Navy (#0F172A), Slate (#1E293B, #334155), Blue Accent (#1677E8, #2563EB), Green (#0AAE7B), Warning (#FF7A00), Danger (#F32945)
"""

import os
from PIL import Image, ImageDraw, ImageFont

OUTPUT_DIR = os.path.abspath("docs/screenshots")
os.makedirs(OUTPUT_DIR, exist_ok=True)

WIDTH = 1080
HEIGHT = 2400

# Color palette
C_BG_GRAD_TOP = (15, 23, 42)      # Deep slate navy #0F172A
C_BG_GRAD_BOT = (30, 41, 59)      # Slate #1E293B
C_PHONE_FRAME = (15, 23, 42)
C_PHONE_BORDER = (51, 65, 85)
C_SCREEN_BG = (248, 250, 252)     # #F8FAFC
C_WHITE = (255, 255, 255)
C_NAVY = (15, 23, 42)
C_TEXT_DARK = (15, 23, 42)
C_TEXT_MUTED = (100, 116, 139)
C_TEXT_SUBTLE = (148, 163, 184)
C_BLUE = (22, 119, 232)
C_BLUE_LIGHT = (239, 246, 255)
C_BLUE_BORDER = (191, 219, 254)
C_GREEN = (10, 174, 123)
C_GREEN_LIGHT = (220, 247, 235)
C_ORANGE = (255, 122, 0)
C_ORANGE_LIGHT = (255, 240, 216)
C_RED = (243, 41, 69)
C_RED_LIGHT = (255, 232, 236)
C_CARD_BORDER = (226, 232, 240)

def get_font(size, bold=False):
    font_names = ["segoeuib.ttf" if bold else "segoeui.ttf", "arialbd.ttf" if bold else "arial.ttf"]
    for fn in font_names:
        p = os.path.join("C:/Windows/Fonts", fn)
        if os.path.exists(p):
            try:
                return ImageFont.truetype(p, size)
            except Exception:
                pass
    return ImageFont.load_default()

F_BANNER_BADGE = get_font(26, bold=True)
F_BANNER_TITLE = get_font(52, bold=True)
F_BANNER_SUB = get_font(30, bold=False)

F_STATUS = get_font(22, bold=True)
F_HEADER = get_font(32, bold=True)
F_TITLE = get_font(38, bold=True)
F_SUBTITLE = get_font(24, bold=False)
F_LABEL = get_font(22, bold=True)
F_BODY = get_font(24, bold=False)
F_BODY_SM = get_font(20, bold=False)
F_BTN = get_font(26, bold=True)
F_KPI_NUM = get_font(42, bold=True)
F_TAG = get_font(20, bold=True)

def create_base_canvas(badge_text, title_text, sub_text, bg_accent=C_BLUE):
    img = Image.new("RGBA", (WIDTH, HEIGHT), C_BG_GRAD_TOP)
    draw = ImageDraw.Draw(img)

    # Vertical gradient for background
    for y in range(HEIGHT):
        ratio = y / HEIGHT
        r = int(C_BG_GRAD_TOP[0] * (1 - ratio) + C_BG_GRAD_BOT[0] * ratio)
        g = int(C_BG_GRAD_TOP[1] * (1 - ratio) + C_BG_GRAD_BOT[1] * ratio)
        b = int(C_BG_GRAD_TOP[2] * (1 - ratio) + C_BG_GRAD_BOT[2] * ratio)
        draw.line([(0, y), (WIDTH, y)], fill=(r, g, b, 255))

    # Decorative top glow
    draw.ellipse([WIDTH//2 - 400, -200, WIDTH//2 + 400, 300], fill=(bg_accent[0], bg_accent[1], bg_accent[2], 30))

    # Top Banner Header
    top_y = 70

    # Badge Pill
    badge_w = int(draw.textlength(badge_text, font=F_BANNER_BADGE) + 36)
    badge_x = (WIDTH - badge_w) // 2
    draw.rounded_rectangle([badge_x, top_y, badge_x + badge_w, top_y + 44], radius=22, fill=(bg_accent[0], bg_accent[1], bg_accent[2], 60), outline=bg_accent, width=2)
    draw.text((badge_x + 18, top_y + 8), badge_text, font=F_BANNER_BADGE, fill=C_WHITE)

    # Title
    top_y += 66
    title_w = draw.textlength(title_text, font=F_BANNER_TITLE)
    draw.text(((WIDTH - title_w) // 2, top_y), title_text, font=F_BANNER_TITLE, fill=C_WHITE)

    # Subtitle
    top_y += 72
    sub_w = draw.textlength(sub_text, font=F_BANNER_SUB)
    draw.text(((WIDTH - sub_w) // 2, top_y), sub_text, font=F_BANNER_SUB, fill=C_TEXT_SUBTLE)

    # Phone Frame positioning
    px1 = 85
    py1 = 310
    px2 = WIDTH - 85
    py2 = HEIGHT + 40   # phone bleeds naturally out of screen bottom

    # Phone drop shadow
    for i in range(15):
        alpha = int(40 - i * 2.5)
        if alpha > 0:
            draw.rounded_rectangle([px1 - i*2, py1 - i, px2 + i*2, py2 + i], radius=58 + i, outline=(0, 0, 0, alpha), width=2)

    # Outer phone body
    draw.rounded_rectangle([px1, py1, px2, py2], radius=58, fill=C_PHONE_FRAME, outline=C_PHONE_BORDER, width=4)

    # Inner Screen Frame
    sx1 = px1 + 14
    sy1 = py1 + 14
    sx2 = px2 - 14
    sy2 = py2
    draw.rounded_rectangle([sx1, sy1, sx2, sy2], radius=46, fill=C_SCREEN_BG)

    # Dynamic Island
    di_w = 200
    di_h = 34
    di_x = (sx1 + sx2 - di_w) // 2
    di_y = sy1 + 14
    draw.rounded_rectangle([di_x, di_y, di_x + di_w, di_y + di_h], radius=17, fill=(10, 15, 26))
    draw.ellipse([di_x + di_w - 38, di_y + 8, di_x + di_w - 20, di_y + 26], fill=(20, 30, 45)) # camera dot

    # Phone Status Bar
    draw.text((sx1 + 42, sy1 + 18), "09:41", font=F_STATUS, fill=C_TEXT_DARK)

    # Status Icons on Right (5G, Wifi, Battery)
    stat_rx = sx2 - 50
    # Battery icon
    draw.rounded_rectangle([stat_rx - 40, sy1 + 22, stat_rx - 12, sy1 + 38], radius=4, outline=C_TEXT_DARK, width=2)
    draw.rounded_rectangle([stat_rx - 37, sy1 + 25, stat_rx - 17, sy1 + 35], radius=2, fill=C_TEXT_DARK)
    draw.rectangle([stat_rx - 11, sy1 + 26, stat_rx - 9, sy1 + 34], fill=C_TEXT_DARK)
    # 5G text
    draw.text((stat_rx - 85, sy1 + 20), "5G", font=F_STATUS, fill=C_TEXT_DARK)

    return img, draw, sx1, sy1 + 65, sx2, sy2

def draw_bottom_nav(draw, sx1, sx2, active_idx=0):
    nav_h = 95
    ny1 = HEIGHT - nav_h - 20
    ny2 = HEIGHT - 10
    draw.rectangle([sx1, ny1, sx2, ny2], fill=C_WHITE, outline=C_CARD_BORDER, width=1)

    items = ["Dashboard", "Bills", "Chi phí", "Quét mã", "Mô-đun"]
    icons = ["📊", "🧾", "💰", "📷", "⚙️"]
    item_w = (sx2 - sx1) / len(items)

    for i, (name, icon) in enumerate(zip(items, icons)):
        ix = sx1 + i * item_w + item_w / 2
        is_active = (i == active_idx)
        text_color = C_BLUE if is_active else C_TEXT_MUTED

        icon_w = draw.textlength(icon, font=F_LABEL)
        draw.text((ix - icon_w/2, ny1 + 14), icon, font=F_LABEL, fill=text_color)

        tw = draw.textlength(name, font=F_BODY_SM)
        draw.text((ix - tw/2, ny1 + 50), name, font=F_BODY_SM, fill=text_color)

    # Home bar indicator
    hx1 = (sx1 + sx2) // 2 - 80
    hx2 = (sx1 + sx2) // 2 + 80
    draw.rounded_rectangle([hx1, HEIGHT - 12, hx2, HEIGHT - 6], radius=3, fill=C_TEXT_MUTED)

# -------------------------------------------------------------
# SCREEN 1: ĐĂNG NHẬP & PHÂN QUYỀN ĐA VAI TRÒ
# -------------------------------------------------------------
def make_screen_1():
    img, draw, sx1, sy, sx2, sy2 = create_base_canvas(
        "LSS LCMS MOBILE • BẢO MẬT",
        "Đăng nhập & Phân quyền Đa vai trò",
        "Kiểm soát truy cập theo thẩm quyền dữ liệu (Data Scope)",
        bg_accent=C_BLUE
    )

    pad = 28
    cx1 = sx1 + pad
    cx2 = sx2 - pad
    y = sy + 25

    # Brand badge
    draw.rounded_rectangle([cx1, y, cx1 + 90, y + 42], radius=10, fill=C_NAVY)
    draw.text((cx1 + 18, y + 8), "LSS", font=F_HEADER, fill=C_WHITE)

    y += 58
    draw.text((cx1, y), "Đăng nhập CMS", font=F_TITLE, fill=C_NAVY)
    y += 48
    draw.text((cx1, y), "Hệ thống Quản lý Chi phí & Lợi nhuận Logistics", font=F_SUBTITLE, fill=C_TEXT_MUTED)
    y += 45

    # Main Card
    card_h = 390
    draw.rounded_rectangle([cx1, y, cx2, y + card_h], radius=18, fill=C_WHITE, outline=C_CARD_BORDER, width=2)

    cy = y + 25
    draw.text((cx1 + 24, cy), "Tài khoản công việc (Email)", font=F_LABEL, fill=C_NAVY)
    cy += 32
    draw.rounded_rectangle([cx1 + 24, cy, cx2 - 24, cy + 62], radius=10, fill=(248, 250, 252), outline=(203, 213, 225), width=2)
    draw.text((cx1 + 42, cy + 18), "admin@cms.local", font=F_BODY, fill=C_NAVY)

    cy += 84
    draw.text((cx1 + 24, cy), "Mật khẩu bảo mật", font=F_LABEL, fill=C_NAVY)
    draw.text((cx2 - 170, cy), "Ẩn mật khẩu", font=F_BODY_SM, fill=C_BLUE)
    cy += 32
    draw.rounded_rectangle([cx1 + 24, cy, cx2 - 24, cy + 62], radius=10, fill=(248, 250, 252), outline=(203, 213, 225), width=2)
    draw.text((cx1 + 42, cy + 18), "••••••••••••", font=F_BODY, fill=C_NAVY)

    cy += 84
    draw.rounded_rectangle([cx1 + 24, cy, cx2 - 24, cy + 66], radius=12, fill=C_NAVY)
    btn_w = draw.textlength("Đăng nhập an toàn", font=F_BTN)
    draw.text(((cx1 + cx2 - btn_w)//2, cy + 18), "Đăng nhập an toàn", font=F_BTN, fill=C_WHITE)

    y += card_h + 30

    # Role Selector Heading
    draw.text((cx1, y), "Chọn nhanh vai trò (Role-Adaptive Demo):", font=F_LABEL, fill=C_NAVY)
    y += 38

    roles = [
        ("Quản trị (Admin)", "admin@cms.local", "Toàn quyền điều hành, phê duyệt, xem biên lợi nhuận", C_NAVY),
        ("Kiểm soát tài chính (Controller)", "controller@cms.local", "Duyệt ngoại lệ, kiểm soát chênh lệch, khóa sổ", C_BLUE),
        ("Kế toán chi phí (CostAccountant)", "cost@cms.local", "Quản lý Chi phí vận tải & Công nợ AP nhà xe", C_ORANGE),
        ("Tài xế vận chuyển (Driver)", "driver@cms.local", "Nhận chuyến, định vị GPS lộ trình, chụp POD", C_GREEN),
    ]

    for title, mail, desc, color in roles:
        draw.rounded_rectangle([cx1, y, cx2, y + 104], radius=14, fill=C_WHITE, outline=C_CARD_BORDER, width=2)
        # Left color strip
        draw.rounded_rectangle([cx1, y, cx1 + 8, y + 104], radius=4, fill=color)

        draw.text((cx1 + 24, y + 14), title, font=F_HEADER, fill=C_NAVY)
        draw.text((cx1 + 24, y + 48), mail, font=F_BODY_SM, fill=C_BLUE)
        draw.text((cx1 + 24, y + 74), desc, font=F_BODY_SM, fill=C_TEXT_MUTED)
        y += 118

    # Privacy link footer
    y += 10
    ft = "Chính sách về Quyền riêng tư — Chuẩn Google Play & App Store"
    ft_w = draw.textlength(ft, font=F_BODY_SM)
    draw.text(((sx1 + sx2 - ft_w)//2, y), ft, font=F_BODY_SM, fill=C_BLUE)

    img.save(os.path.join(OUTPUT_DIR, "01_mobile_login_roles.png"))
    print("Saved 01_mobile_login_roles.png")

# -------------------------------------------------------------
# SCREEN 2: BẢNG ĐIỀU KHIỂN TÀI CHÍNH (DASHBOARD)
# -------------------------------------------------------------
def make_screen_2():
    img, draw, sx1, sy, sx2, sy2 = create_base_canvas(
        "CHỈ SỐ THỜI GIAN THỰC",
        "Bảng điều khiển Tài chính Logistics",
        "Kiểm soát dòng tiền, doanh thu & biên lợi nhuận vận tải",
        bg_accent=(10, 174, 123)
    )

    pad = 28
    cx1 = sx1 + pad
    cx2 = sx2 - pad
    y = sy + 15

    # Header Strip
    draw.text((cx1, y), "Bảng điều khiển", font=F_TITLE, fill=C_NAVY)
    # Online badge
    draw.rounded_rectangle([cx2 - 180, y + 4, cx2, y + 40], radius=18, fill=C_GREEN_LIGHT, outline=C_GREEN, width=1)
    draw.text((cx2 - 165, y + 10), "● Đồng bộ online", font=F_TAG, fill=C_GREEN)

    y += 54
    draw.text((cx1, y), "Thuê bao: LSS Global Logistics • Kỳ T10/2026", font=F_SUBTITLE, fill=C_TEXT_MUTED)
    y += 45

    # 3 Big KPI Cards
    # KPI 1: Chi phí
    draw.rounded_rectangle([cx1, y, cx2, y + 140], radius=16, fill=C_WHITE, outline=C_CARD_BORDER, width=2)
    draw.text((cx1 + 24, y + 18), "TỔNG CHI PHÍ VẬN TẢI PHÁT SINH", font=F_TAG, fill=C_TEXT_MUTED)
    draw.text((cx1 + 24, y + 50), "842.500.000 ₫", font=F_KPI_NUM, fill=C_NAVY)
    draw.rounded_rectangle([cx2 - 190, y + 70, cx2 - 24, y + 108], radius=8, fill=C_RED_LIGHT)
    draw.text((cx2 - 175, y + 78), "▲ +4.2% so dự toán", font=F_TAG, fill=C_RED)
    y += 156

    # KPI 2 & 3 in 2 columns
    col_w = (cx2 - cx1 - 16) // 2
    # Doanh thu
    draw.rounded_rectangle([cx1, y, cx1 + col_w, y + 130], radius=16, fill=C_WHITE, outline=C_CARD_BORDER, width=2)
    draw.text((cx1 + 20, y + 18), "DOANH THU CƯỚC", font=F_TAG, fill=C_TEXT_MUTED)
    draw.text((cx1 + 20, y + 54), "1.125.000.000 ₫", font=get_font(28, bold=True), fill=C_BLUE)
    draw.text((cx1 + 20, y + 94), "34 chuyến hoàn tất", font=F_BODY_SM, fill=C_TEXT_MUTED)

    # Lợi nhuận
    draw.rounded_rectangle([cx1 + col_w + 16, y, cx2, y + 130], radius=16, fill=C_WHITE, outline=C_CARD_BORDER, width=2)
    draw.text((cx1 + col_w + 36, y + 18), "BIÊN LỢI NHUẬN GỘP", font=F_TAG, fill=C_TEXT_MUTED)
    draw.text((cx1 + col_w + 36, y + 54), "+25.1%", font=get_font(34, bold=True), fill=C_GREEN)
    draw.text((cx1 + col_w + 36, y + 94), "✓ Vượt mục tiêu 20%", font=F_BODY_SM, fill=C_GREEN)
    y += 150

    # Phân bổ chi phí vận tải (Cost Breakdown)
    draw.rounded_rectangle([cx1, y, cx2, y + 175], radius=16, fill=C_WHITE, outline=C_CARD_BORDER, width=2)
    draw.text((cx1 + 24, y + 18), "CƠ CẤU CHI PHÍ VẬN TẢI THEO DANH MỤC", font=F_LABEL, fill=C_NAVY)

    # Progress bar breakdown
    by = y + 58
    bw = cx2 - cx1 - 48
    draw.rounded_rectangle([cx1 + 24, by, cx1 + 24 + bw*0.62, by + 26], radius=6, fill=C_BLUE)
    draw.rounded_rectangle([cx1 + 24 + bw*0.62 + 4, by, cx1 + 24 + bw*0.80, by + 26], radius=6, fill=C_ORANGE)
    draw.rounded_rectangle([cx1 + 24 + bw*0.80 + 4, by, cx1 + 24 + bw*0.92, by + 26], radius=6, fill=C_GREEN)
    draw.rounded_rectangle([cx1 + 24 + bw*0.92 + 4, by, cx1 + 24 + bw, by + 26], radius=6, fill=C_RED)

    # Legend
    ly = by + 46
    legends = [("Chặng chính 62%", C_BLUE), ("Phí BOT 18%", C_ORANGE), ("Nâng hạ 12%", C_GREEN), ("Khác 8%", C_RED)]
    lx = cx1 + 24
    for lname, lcol in legends:
        draw.ellipse([lx, ly + 4, lx + 14, ly + 18], fill=lcol)
        draw.text((lx + 20, ly), lname, font=F_BODY_SM, fill=C_TEXT_MUTED)
        lx += int(draw.textlength(lname, font=F_BODY_SM) + 40)

    y += 195

    # Chuyến hàng & Bill gần đây
    draw.text((cx1, y), "Chuyến hàng & Chặng vận chuyển gần đây", font=F_LABEL, fill=C_NAVY)
    y += 36

    shipments = [
        ("BILL-2026-10-0089", "Hải Phòng ➔ Bắc Ninh (29C-882.11)", "4.850.000 ₫", "Đang chạy • GPS Bật", C_GREEN),
        ("BILL-2026-10-0088", "Cát Lái ➔ KCN VSIP 1 (51D-492.30)", "2.800.000 ₫", "Đã đối soát xong", C_BLUE),
        ("BILL-2026-10-0087", "Đà Nẵng ➔ Quảng Ngãi (43C-112.94)", "3.450.000 ₫", "Chờ duyệt phụ phí", C_ORANGE),
    ]

    for bcode, broute, bcost, bstat, bcolor in shipments:
        draw.rounded_rectangle([cx1, y, cx2, y + 108], radius=14, fill=C_WHITE, outline=C_CARD_BORDER, width=2)
        draw.text((cx1 + 22, y + 14), bcode, font=F_HEADER, fill=C_NAVY)
        draw.text((cx2 - 180, y + 14), bcost, font=F_HEADER, fill=C_NAVY)
        draw.text((cx1 + 22, y + 50), broute, font=F_BODY_SM, fill=C_TEXT_MUTED)

        # Status badge
        badge_w = int(draw.textlength(bstat, font=F_TAG) + 24)
        draw.rounded_rectangle([cx1 + 22, y + 74, cx1 + 22 + badge_w, y + 100], radius=6, fill=C_SCREEN_BG, outline=bcolor, width=1)
        draw.text((cx1 + 34, y + 78), bstat, font=F_TAG, fill=bcolor)
        y += 122

    draw_bottom_nav(draw, sx1, sx2, active_idx=0)
    img.save(os.path.join(OUTPUT_DIR, "02_mobile_dashboard_kpi.png"))
    print("Saved 02_mobile_dashboard_kpi.png")

# -------------------------------------------------------------
# SCREEN 3: QUẢN LÝ BILL & CHI PHÍ CHẶNG (BILLS & MOVEMENTS)
# -------------------------------------------------------------
def make_screen_3():
    img, draw, sx1, sy, sx2, sy2 = create_base_canvas(
        "KIỂM SOÁT 3 TRẠNG THÁI",
        "Quản lý Bill & Chi phí Chặng",
        "Dự tính (Expected) ➔ Xác nhận ➔ Thực tế (Actual)",
        bg_accent=(22, 119, 232)
    )

    pad = 28
    cx1 = sx1 + pad
    cx2 = sx2 - pad
    y = sy + 15

    draw.text((cx1, y), "Danh sách Bill Vận tải", font=F_TITLE, fill=C_NAVY)
    y += 52

    # Search Box
    draw.rounded_rectangle([cx1, y, cx2, y + 58], radius=12, fill=C_WHITE, outline=C_CARD_BORDER, width=2)
    draw.text((cx1 + 20, y + 16), "🔍 Tìm theo mã Bill, HAWB, số xe...", font=F_BODY, fill=C_TEXT_SUBTLE)
    y += 74

    # Filter chips
    chips = [("Tất cả (42)", True), ("Chờ xác nhận (5)", False), ("Chênh lệch (3)", False), ("Đã đối soát (34)", False)]
    chx = cx1
    for ctext, is_active in chips:
        cw = int(draw.textlength(ctext, font=F_TAG) + 30)
        draw.rounded_rectangle([chx, y, chx + cw, y + 42], radius=10, fill=C_NAVY if is_active else C_WHITE, outline=C_CARD_BORDER if not is_active else C_NAVY, width=2)
        draw.text((chx + 15, y + 10), ctext, font=F_TAG, fill=C_WHITE if is_active else C_TEXT_MUTED)
        chx += cw + 10

    y += 62

    # Detailed Bill 1 (Focused expanded bill)
    draw.rounded_rectangle([cx1, y, cx2, y + 420], radius=18, fill=C_WHITE, outline=C_BLUE, width=3)

    # Header row
    draw.rounded_rectangle([cx1 + 18, y + 18, cx1 + 160, y + 54], radius=8, fill=C_BLUE_LIGHT)
    draw.text((cx1 + 28, y + 24), "BILL VẬN TẢI", font=F_TAG, fill=C_BLUE)
    draw.text((cx1 + 180, y + 20), "BILL-2026-10-0089", font=F_HEADER, fill=C_NAVY)
    draw.text((cx2 - 160, y + 24), "● Đang chạy", font=F_TAG, fill=C_GREEN)

    by = y + 70
    draw.text((cx1 + 24, by), "Tuyến: Hải Phòng (Cảng Đình Vũ) ➔ Bắc Ninh (Yên Phong)", font=F_BODY, fill=C_NAVY)
    by += 32
    draw.text((cx1 + 24, by), "Xe: 29C-882.11 (Đầu kéo Container 40ft) • Tài xế: Nguyễn Văn Hùng", font=F_BODY_SM, fill=C_TEXT_MUTED)

    # 3 Cost stages box
    by += 44
    draw.rounded_rectangle([cx1 + 20, by, cx2 - 20, by + 130], radius=12, fill=(248, 250, 252), outline=C_CARD_BORDER, width=1)

    cy = by + 16
    draw.text((cx1 + 36, cy), "DỰ TÍNH (EXPECTED)", font=F_TAG, fill=C_TEXT_MUTED)
    draw.text((cx1 + 36, cy + 30), "4.500.000 ₫", font=F_HEADER, fill=C_NAVY)

    draw.text((cx1 + 280, cy), "XÁC NHẬN (CONFIRMED)", font=F_TAG, fill=C_TEXT_MUTED)
    draw.text((cx1 + 280, cy + 30), "4.500.000 ₫", font=F_HEADER, fill=C_NAVY)

    draw.text((cx2 - 250, cy), "THỰC TẾ (ACTUAL)", font=F_TAG, fill=C_RED)
    draw.text((cx2 - 250, cy + 30), "4.850.000 ₫", font=F_HEADER, fill=C_RED)

    # Variance alert
    draw.rounded_rectangle([cx1 + 36, cy + 74, cx2 - 36, cy + 112], radius=6, fill=C_RED_LIGHT)
    draw.text((cx1 + 48, cy + 82), "⚠️ Chênh lệch: +350.000 ₫ (Phụ phí lưu bãi quá giờ tại Đình Vũ)", font=F_BODY_SM, fill=C_RED)

    # Action buttons inside card
    by += 150
    draw.rounded_rectangle([cx1 + 20, by, cx1 + 280, by + 56], radius=10, fill=C_BLUE)
    draw.text((cx1 + 48, by + 15), "📍 Xem lộ trình GPS", font=F_BTN, fill=C_WHITE)

    draw.rounded_rectangle([cx1 + 295, by, cx2 - 20, by + 56], radius=10, fill=C_SCREEN_BG, outline=C_BLUE, width=2)
    draw.text((cx1 + 340, by + 15), "📄 Xem chứng từ POD (2)", font=F_BTN, fill=C_BLUE)

    y += 445

    # Bill 2 (Standard card)
    draw.rounded_rectangle([cx1, y, cx2, y + 240], radius=16, fill=C_WHITE, outline=C_CARD_BORDER, width=2)
    draw.text((cx1 + 24, y + 20), "BILL-2026-10-0088", font=F_HEADER, fill=C_NAVY)
    draw.text((cx2 - 180, y + 20), "2.800.000 ₫", font=F_HEADER, fill=C_BLUE)
    draw.text((cx1 + 24, y + 60), "Tuyến: Cát Lái ➔ KCN VSIP 1 (Bình Dương)", font=F_BODY, fill=C_NAVY)
    draw.text((cx1 + 24, y + 95), "Xe: 51D-492.30 • Xe tải 8 tấn • HAWB: SGN-VSIP-8821", font=F_BODY_SM, fill=C_TEXT_MUTED)
    draw.rounded_rectangle([cx1 + 24, y + 145, cx1 + 260, y + 185], radius=8, fill=C_GREEN_LIGHT)
    draw.text((cx1 + 40, y + 153), "✓ Đã đối soát & khớp 100%", font=F_TAG, fill=C_GREEN)

    draw_bottom_nav(draw, sx1, sx2, active_idx=1)
    img.save(os.path.join(OUTPUT_DIR, "03_mobile_bills_costs.png"))
    print("Saved 03_mobile_bills_costs.png")

# -------------------------------------------------------------
# SCREEN 4: QUÉT MÃ VẬN ĐƠN & CHỤP CHỨNG TỪ POD (SCANNER)
# -------------------------------------------------------------
def make_screen_4():
    img, draw, sx1, sy, sx2, sy2 = create_base_canvas(
        "CHỨNG TỪ SỐ HIỆN TRƯỜNG",
        "Quét mã Vận đơn & Chụp ảnh POD",
        "Nhận diện mã HAWB/MAWB & biên bản giao nhận tức thì",
        bg_accent=(255, 122, 0)
    )

    pad = 28
    cx1 = sx1 + pad
    cx2 = sx2 - pad
    y = sy + 15

    draw.text((cx1, y), "Quét mã Vận đơn & POD", font=F_TITLE, fill=C_NAVY)
    draw.text((cx2 - 160, y + 8), "⚡ Flash Bật", font=F_TAG, fill=C_ORANGE)
    y += 52

    # Camera Viewfinder Box
    cam_h = 480
    draw.rounded_rectangle([cx1, y, cx2, y + cam_h], radius=20, fill=(15, 23, 42), outline=(30, 41, 59), width=3)

    # Simulated Camera Scene (Night/Warehouse background)
    vw = cx2 - cx1
    draw.rectangle([cx1 + 40, y + 40, cx2 - 40, y + cam_h - 40], fill=(20, 32, 50))

    # Scanner target box with Green Laser
    tx1 = cx1 + 80
    ty1 = y + 80
    tx2 = cx2 - 80
    ty2 = y + cam_h - 80

    # Corner brackets (green)
    c_len = 50
    # Top-Left
    draw.line([(tx1, ty1), (tx1 + c_len, ty1)], fill=C_GREEN, width=6)
    draw.line([(tx1, ty1), (tx1, ty1 + c_len)], fill=C_GREEN, width=6)
    # Top-Right
    draw.line([(tx2, ty1), (tx2 - c_len, ty1)], fill=C_GREEN, width=6)
    draw.line([(tx2, ty1), (tx2, ty1 + c_len)], fill=C_GREEN, width=6)
    # Bottom-Left
    draw.line([(tx1, ty2), (tx1 + c_len, ty2)], fill=C_GREEN, width=6)
    draw.line([(tx1, ty2), (tx1, ty2 - c_len)], fill=C_GREEN, width=6)
    # Bottom-Right
    draw.line([(tx2, ty2), (tx2 - c_len, ty2)], fill=C_GREEN, width=6)
    draw.line([(tx2, ty2), (tx2, ty2 - c_len)], fill=C_GREEN, width=6)

    # Simulated QR code pattern in center
    qcx = (tx1 + tx2) // 2
    qcy = (ty1 + ty2) // 2
    draw.rectangle([qcx - 70, qcy - 70, qcx + 70, qcy + 70], fill=(240, 240, 240))
    draw.rectangle([qcx - 55, qcy - 55, qcx - 25, qcy - 25], fill=(10, 15, 26))
    draw.rectangle([qcx + 25, qcy - 55, qcx + 55, qcy - 25], fill=(10, 15, 26))
    draw.rectangle([qcx - 55, qcy + 25, qcx - 25, qcy + 55], fill=(10, 15, 26))

    # Laser scan line
    draw.line([(tx1 + 10, qcy), (tx2 - 10, qcy)], fill=C_GREEN, width=4)

    # Scanner label
    scan_lbl = "ĐANG QUÉT MÃ VẬN ĐƠN (HAWB / MAWB)..."
    sl_w = draw.textlength(scan_lbl, font=F_TAG)
    draw.text(((cx1 + cx2 - sl_w)//2, y + cam_h - 60), scan_lbl, font=F_TAG, fill=C_GREEN)

    y += cam_h + 24

    # Recognized Document Result Card
    draw.rounded_rectangle([cx1, y, cx2, y + 360], radius=18, fill=C_WHITE, outline=C_CARD_BORDER, width=2)

    ry = y + 20
    draw.rounded_rectangle([cx1 + 20, ry, cx1 + 240, ry + 36], radius=6, fill=C_GREEN_LIGHT)
    draw.text((cx1 + 32, ry + 8), "✓ ĐÃ NHẬN DIỆN MÃ", font=F_TAG, fill=C_GREEN)

    ry += 48
    draw.text((cx1 + 24, ry), "Mã Vận đơn: HAWB-8829104", font=F_TITLE, fill=C_NAVY)
    ry += 44
    draw.text((cx1 + 24, ry), "Đơn vị giao nhận: Công ty TNHH Tiếp vận LSS Toàn Cầu", font=F_BODY, fill=C_NAVY)
    ry += 32
    draw.text((cx1 + 24, ry), "Loại chứng từ: Biên bản Bàn giao & Ký nhận (POD) 2 liên", font=F_BODY_SM, fill=C_TEXT_MUTED)
    ry += 30
    draw.text((cx1 + 24, ry), "Tọa độ GPS: 20.8449° N, 106.6881° E • 09:41:25", font=F_BODY_SM, fill=C_TEXT_MUTED)

    # Camera Capture Action Buttons
    ry += 52
    draw.rounded_rectangle([cx1 + 20, ry, cx2 - 20, ry + 64], radius=12, fill=C_NAVY)
    cb_w = draw.textlength("📸 Chụp ảnh Chứng từ & Tải lên Ngay", font=F_BTN)
    draw.text(((cx1 + cx2 - cb_w)//2, ry + 18), "📸 Chụp ảnh Chứng từ & Tải lên Ngay", font=F_BTN, fill=C_WHITE)

    draw_bottom_nav(draw, sx1, sx2, active_idx=3)
    img.save(os.path.join(OUTPUT_DIR, "04_mobile_scanner_pod.png"))
    print("Saved 04_mobile_scanner_pod.png")

# -------------------------------------------------------------
# SCREEN 5: PHÊ DUYỆT CHI PHÍ & CHÊNH LỆCH (APPROVALS)
# -------------------------------------------------------------
def make_screen_5():
    img, draw, sx1, sy, sx2, sy2 = create_base_canvas(
        "QUY TRÌNH MAKER-CHECKER",
        "Phê duyệt Chi phí & Kiểm soát Chênh lệch",
        "Xác thực sinh trắc học Face ID an toàn trước khi duyệt",
        bg_accent=(243, 41, 69)
    )

    pad = 28
    cx1 = sx1 + pad
    cx2 = sx2 - pad
    y = sy + 15

    draw.text((cx1, y), "Hàng đợi Phê duyệt", font=F_TITLE, fill=C_NAVY)
    draw.rounded_rectangle([cx2 - 160, y + 4, cx2, y + 40], radius=18, fill=C_RED_LIGHT, outline=C_RED, width=1)
    draw.text((cx2 - 145, y + 10), "3 yêu cầu chờ", font=F_TAG, fill=C_RED)
    y += 54

    draw.text((cx1, y), "Phân tách nhiệm vụ (SoD): Người tạo không thể tự duyệt", font=F_SUBTITLE, fill=C_TEXT_MUTED)
    y += 44

    # High Priority Approval Request Card
    draw.rounded_rectangle([cx1, y, cx2, y + 460], radius=18, fill=C_WHITE, outline=C_RED, width=3)

    ay = y + 20
    draw.rounded_rectangle([cx1 + 20, ay, cx1 + 220, ay + 36], radius=6, fill=C_RED_LIGHT)
    draw.text((cx1 + 32, ay + 8), "⚠️ VƯỢT DỰ TOÁN +29%", font=F_TAG, fill=C_RED)
    draw.text((cx2 - 180, ay + 6), "Mã #AP-99042", font=F_TAG, fill=C_TEXT_MUTED)

    ay += 50
    draw.text((cx1 + 24, ay), "Phụ phí lưu bãi quá giờ tại Cảng Đình Vũ", font=F_HEADER, fill=C_NAVY)
    ay += 36
    draw.text((cx1 + 24, ay), "Xe 29C-882.11 • Chuyến HPH-BN-09 • Tài xế: Nguyễn Văn Hùng", font=F_BODY_SM, fill=C_TEXT_MUTED)

    # Comparison Grid
    ay += 40
    draw.rounded_rectangle([cx1 + 20, ay, cx2 - 20, ay + 110], radius=12, fill=(248, 250, 252), outline=C_CARD_BORDER, width=1)

    cy = ay + 16
    draw.text((cx1 + 36, cy), "CHI PHÍ ĐỊNH MỨC", font=F_TAG, fill=C_TEXT_MUTED)
    draw.text((cx1 + 36, cy + 30), "1.200.000 ₫", font=F_HEADER, fill=C_NAVY)

    draw.text((cx2 - 260, cy), "YÊU CẦU DUYỆT THỰC TẾ", font=F_TAG, fill=C_RED)
    draw.text((cx2 - 260, cy + 30), "1.550.000 ₫", font=F_HEADER, fill=C_RED)

    draw.text((cx1 + 36, cy + 74), "Lý do: Cảng ùn tắc 4.5h do tàu trễ, phát sinh phí lưu bãi theo biên bản", font=F_BODY_SM, fill=C_TEXT_MUTED)

    # Attached receipt preview
    ay += 126
    draw.rounded_rectangle([cx1 + 20, ay, cx2 - 20, ay + 64], radius=10, fill=C_SCREEN_BG, outline=C_CARD_BORDER, width=1)
    draw.text((cx1 + 36, ay + 18), "📎 Chứng từ đính kèm: [Ảnh hóa đơn bãi Đình Vũ.jpg]", font=F_BODY_SM, fill=C_BLUE)
    draw.text((cx2 - 120, ay + 18), "Xem ảnh", font=F_TAG, fill=C_BLUE)

    # Approve / Reject buttons
    ay += 84
    btn_w = (cx2 - cx1 - 50) // 2
    # Reject button
    draw.rounded_rectangle([cx1 + 20, ay, cx1 + 20 + btn_w, ay + 64], radius=12, fill=C_SCREEN_BG, outline=C_RED, width=2)
    draw.text((cx1 + 60, ay + 18), "✕ Từ chối chi", font=F_BTN, fill=C_RED)

    # Approve button
    draw.rounded_rectangle([cx1 + 30 + btn_w, ay, cx2 - 20, ay + 64], radius=12, fill=C_GREEN)
    draw.text((cx1 + 70 + btn_w, ay + 18), "✓ Phê duyệt (Face ID)", font=F_BTN, fill=C_WHITE)

    y += 485

    # Request 2
    draw.rounded_rectangle([cx1, y, cx2, y + 220], radius=16, fill=C_WHITE, outline=C_CARD_BORDER, width=2)
    draw.text((cx1 + 24, y + 18), "Phí cầu đường cao tốc Hà Nội - Hải Phòng", font=F_HEADER, fill=C_NAVY)
    draw.text((cx2 - 170, y + 18), "840.000 ₫", font=F_HEADER, fill=C_NAVY)
    draw.text((cx1 + 24, y + 54), "Xe 15C-341.22 • 02 lượt vé ePass điện tử", font=F_BODY_SM, fill=C_TEXT_MUTED)
    draw.rounded_rectangle([cx1 + 24, y + 95, cx1 + 200, y + 130], radius=6, fill=C_GREEN_LIGHT)
    draw.text((cx1 + 36, y + 102), "✓ Khớp vé VETC 100%", font=F_TAG, fill=C_GREEN)

    draw_bottom_nav(draw, sx1, sx2, active_idx=4)
    img.save(os.path.join(OUTPUT_DIR, "05_mobile_approvals_control.png"))
    print("Saved 05_mobile_approvals_control.png")

# -------------------------------------------------------------
# SCREEN 6: CHÍNH SÁCH QUYỀN RIÊNG TƯ & STORE COMPLIANCE
# -------------------------------------------------------------
def make_screen_6():
    img, draw, sx1, sy, sx2, sy2 = create_base_canvas(
        "TIÊU CHUẨN GOOGLE PLAY & APPLE",
        "Chính sách Quyền riêng tư & Bảo vệ Dữ liệu",
        "Bảo vệ dữ liệu định vị GPS, tài chính & quyền xóa tài khoản",
        bg_accent=(16, 37, 74)
    )

    pad = 28
    cx1 = sx1 + pad
    cx2 = sx2 - pad
    y = sy + 15

    draw.text((cx1, y), "Quyền riêng tư Dữ liệu", font=F_TITLE, fill=C_NAVY)
    draw.rounded_rectangle([cx2 - 160, y + 4, cx2, y + 40], radius=8, fill=C_BLUE_LIGHT, outline=C_BLUE, width=1)
    draw.text((cx2 - 145, y + 10), "🌐 Bản Web", font=F_TAG, fill=C_BLUE)
    y += 50

    draw.text((cx1, y), "Phiên bản v1.0 • Hiệu lực: 16/10/2026 • LSS Logistics", font=F_SUBTITLE, fill=C_TEXT_MUTED)
    y += 40

    # Store Review Highlight Card
    draw.rounded_rectangle([cx1, y, cx2, y + 310], radius=18, fill=C_BLUE_LIGHT, outline=C_BLUE_BORDER, width=2)
    draw.text((cx1 + 24, y + 18), "🛡️ Tóm tắt Tuân thủ App Store & Google Play", font=F_HEADER, fill=C_BLUE)

    sy_box = y + 60
    points = [
        ("Định vị GPS Chặng xe:", "Chỉ ghi nhận khi bắt đầu nhận chuyến và ngắt hoàn toàn khi giao hàng xong. Tuyệt đối không theo dõi ngoài giờ làm việc."),
        ("Quyền Camera:", "Chỉ dùng để quét mã vận đơn HAWB và chụp ảnh chứng từ giao nhận (POD) hiện trường."),
        ("Sinh trắc học:", "Xử lý 100% cục bộ trên Secure Enclave / Keystore thiết bị, không lưu trữ trên máy chủ."),
        ("Xóa tài khoản:", "Hỗ trợ gửi yêu cầu xóa tài khoản trực tiếp trên ứng dụng hoặc email, xử lý trong 7 ngày.")
    ]

    for ptitle, pdesc in points:
        draw.text((cx1 + 24, sy_box), f"• {ptitle}", font=F_LABEL, fill=C_NAVY)
        sy_box += 26
        draw.text((cx1 + 38, sy_box), pdesc, font=F_BODY_SM, fill=C_TEXT_MUTED)
        sy_box += 34

    y += 330

    # Detailed Section Card
    draw.rounded_rectangle([cx1, y, cx2, y + 360], radius=18, fill=C_WHITE, outline=C_CARD_BORDER, width=2)
    draw.text((cx1 + 24, y + 20), "Cam kết Bảo mật & Quyền lợi người dùng", font=F_HEADER, fill=C_NAVY)

    by = y + 64
    commitments = [
        ("🔒 Tuyệt đối không thương mại hóa dữ liệu:", "LSS cam kết không bán hoặc chia sẻ thông tin cá nhân và chi phí cho bên thứ ba."),
        ("🏢 Cách ly dữ liệu khách hàng (Tenant Isolation):", "Dữ liệu được cô lập logic nghiêm ngặt theo mã định danh doanh nghiệp."),
        ("⚡ Mã hóa truyền tải & lưu trữ:", "100% kết nối qua HTTPS/TLS 1.3 và cơ chế khóa chứng chỉ SSL Pinning."),
        ("⚖️ Tuân thủ quy định kế toán:", "Dữ liệu hóa đơn, chứng từ được lưu giữ theo Luật Kế toán Việt Nam.")
    ]

    for ctitle, cdesc in commitments:
        draw.text((cx1 + 24, by), ctitle, font=F_LABEL, fill=C_NAVY)
        by += 28
        draw.text((cx1 + 24, by), cdesc, font=F_BODY_SM, fill=C_TEXT_MUTED)
        by += 44

    y += 380

    # Account Deletion Action Box
    draw.rounded_rectangle([cx1, y, cx2, y + 160], radius=16, fill=C_RED_LIGHT, outline=C_RED, width=2)
    draw.text((cx1 + 24, y + 18), "Yêu cầu Xóa tài khoản & Dữ liệu cá nhân", font=F_HEADER, fill=C_RED)
    draw.text((cx1 + 24, y + 54), "Người dùng có thể yêu cầu xóa tài khoản tại Cài đặt hoặc gửi email đến:", font=F_BODY_SM, fill=C_NAVY)
    draw.text((cx1 + 24, y + 84), "privacy@lsslogistics.vn (Phản hồi & xử lý trong vòng 07 ngày làm việc)", font=F_LABEL, fill=C_BLUE)

    draw.rounded_rectangle([cx1 + 24, y + 112, cx2 - 24, y + 148], radius=6, fill=C_WHITE, outline=C_RED, width=1)
    draw.text((cx1 + 140, y + 120), "Gửi yêu cầu xóa tài khoản & dữ liệu", font=F_LABEL, fill=C_RED)

    draw_bottom_nav(draw, sx1, sx2, active_idx=4)
    img.save(os.path.join(OUTPUT_DIR, "06_mobile_privacy_compliance.png"))
    print("Saved 06_mobile_privacy_compliance.png")

if __name__ == "__main__":
    make_screen_1()
    make_screen_2()
    make_screen_3()
    make_screen_4()
    make_screen_5()
    make_screen_6()
    print("All 6 store screenshots generated successfully!")
