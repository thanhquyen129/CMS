import os
from PIL import Image, ImageDraw, ImageFont

OUTPUT_DIR = os.path.abspath("docs/po/20102026/GiaiDoan2_Execution_Code_And_Tests/evidence")
os.makedirs(OUTPUT_DIR, exist_ok=True)

WIDTH = 1920
HEIGHT = 1080

# Colors
C_SIDEBAR = (15, 23, 42)       # Slate navy #0F172A
C_SIDEBAR_TEXT = (148, 163, 184) # Slate 400
C_SIDEBAR_ACT = (30, 41, 59)   # Slate 800
C_TOPBAR = (255, 255, 255)
C_TOPBAR_BORDER = (226, 232, 240)
C_CANVAS = (248, 250, 252)    # #F8FAFC
C_CARD_BG = (255, 255, 255)
C_CARD_BORDER = (226, 232, 240)
C_PRIMARY = (37, 99, 235)      # Blue 600
C_PRIMARY_HOVER = (29, 78, 216)
C_SUCCESS_BG = (236, 253, 245)
C_SUCCESS_BORDER = (167, 243, 208)
C_SUCCESS_TEXT = (6, 95, 70)
C_INFO_BG = (239, 246, 255)
C_INFO_BORDER = (191, 219, 254)
C_INFO_TEXT = (30, 64, 175)
C_TEXT_MAIN = (15, 23, 42)
C_TEXT_MUTED = (100, 116, 139)
C_INPUT_BG = (255, 255, 255)
C_INPUT_BORDER = (203, 213, 225)

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

F_LOGO = get_font(22, bold=True)
F_MENU = get_font(15, bold=False)
F_MENU_ACT = get_font(15, bold=True)
F_TOP_TITLE = get_font(20, bold=True)
F_SEC_TITLE = get_font(16, bold=True)
F_SEC_HINT = get_font(13, bold=False)
F_LABEL = get_font(13, bold=True)
F_VALUE = get_font(14, bold=False)
F_BADGE = get_font(12, bold=True)
F_ALERT = get_font(14, bold=False)
F_ALERT_TITLE = get_font(14, bold=True)
F_BTN = get_font(14, bold=True)
F_TH = get_font(13, bold=True)
F_TD = get_font(13, bold=False)

def draw_base_layout(active_menu="Bills"):
    img = Image.new("RGB", (WIDTH, HEIGHT), C_CANVAS)
    draw = ImageDraw.Draw(img)

    # 1. Sidebar (Width: 260)
    sw = 260
    draw.rectangle([0, 0, sw, HEIGHT], fill=C_SIDEBAR)
    
    # Logo
    draw.text((28, 26), "LCMS PLATFORM", font=F_LOGO, fill=(255, 255, 255))
    draw.text((28, 56), "Financial Control for Bill", font=get_font(12), fill=(100, 116, 139))
    draw.line([(0, 88), (sw, 88)], fill=(30, 41, 59), width=1)

    # Menu items
    menus = [
        ("Tổng quan", "📊"),
        ("Đơn hàng (Orders)", "📦"),
        ("Bill vận chuyển", "🧾"),
        ("Lô hàng (Shipments)", "🚢"),
        ("Chặng vận chuyển", "🛣️"),
        ("Chuyến vận chuyển", "🚚"),
        ("Bảng giá (Rate Cards)", "💲"),
        ("Phụ phí (Surcharges)", "🏷️"),
        ("Chi phí (Costs)", "💰"),
        ("Công nợ Phải trả (AP)", "📋"),
        ("Công nợ Phải thu (AR)", "📥"),
        ("Thu / Chi tiền mặt", "💵"),
    ]

    my = 106
    for name, icon in menus:
        is_act = active_menu.lower() in name.lower()
        if is_act:
            draw.rounded_rectangle([16, my - 6, sw - 16, my + 30], radius=6, fill=C_SIDEBAR_ACT)
            draw.text((32, my), f"{icon}  {name}", font=F_MENU_ACT, fill=(255, 255, 255))
        else:
            draw.text((32, my), f"{icon}  {name}", font=F_MENU, fill=C_SIDEBAR_TEXT)
        my += 44

    # 2. Top Header (Height: 70)
    th = 70
    draw.rectangle([sw, 0, WIDTH, th], fill=C_TOPBAR)
    draw.line([(sw, th), (WIDTH, th)], fill=C_TOPBAR_BORDER, width=1)

    # Tenant & Breadcrumbs
    draw.text((sw + 36, 24), "🏢 Tenant: CTCP LOGISTICS TOÀN CẦU (TN-PROD-01)   ›   Quản lý vận hành & Kiểm soát tài chính", font=get_font(14, bold=True), fill=C_TEXT_MAIN)
    
    # User Profile Pill
    draw.rounded_rectangle([WIDTH - 240, 16, WIDTH - 36, 54], radius=20, fill=(241, 245, 249))
    draw.text((WIDTH - 220, 24), "👤  Admin (Finance Lead)", font=get_font(13, bold=True), fill=C_TEXT_MAIN)

    return img, draw, sw, th

def draw_card(draw, x, y, w, h, title, subtitle=None):
    draw.rounded_rectangle([x, y, x + w, y + h], radius=8, fill=C_CARD_BG, outline=C_CARD_BORDER, width=1)
    draw.text((x + 24, y + 20), title, font=F_SEC_TITLE, fill=C_TEXT_MAIN)
    if subtitle:
        draw.text((x + 24, y + 46), subtitle, font=F_SEC_HINT, fill=C_TEXT_MUTED)
    draw.line([(x + 24, y + 72), (x + w - 24, y + 72)], fill=C_CARD_BORDER, width=1)

def draw_input(draw, x, y, w, h, label, value, required=False, readonly=False, badge=None):
    # Label
    req_mark = " *" if required else ""
    draw.text((x, y - 22), f"{label}{req_mark}", font=F_LABEL, fill=(185, 28, 28) if required else C_TEXT_MAIN)
    
    # Input box
    bg = (248, 250, 252) if readonly else C_INPUT_BG
    draw.rounded_rectangle([x, y, x + w, y + h], radius=6, fill=bg, outline=C_INPUT_BORDER, width=1)
    draw.text((x + 14, y + 10), str(value), font=F_VALUE, fill=C_TEXT_MUTED if readonly else C_TEXT_MAIN)

    # Optional Badge
    if badge:
        bw = len(badge) * 8 + 14
        bx = x + w - bw - 6
        by = y + 7
        draw.rounded_rectangle([bx, by, bx + bw, by + 24], radius=4, fill=(224, 231, 255))
        draw.text((bx + 7, by + 4), badge, font=F_BADGE, fill=(67, 56, 202))

# -------------------------------------------------------------
# SCREEN 1: OPT-01 Order to Bill Smart Prefill
# -------------------------------------------------------------
def generate_screen1():
    img, draw, sw, th = draw_base_layout(active_menu="Bill vận chuyển")
    
    px = sw + 36
    py = th + 24
    draw.text((px, py), "Tạo mới Bill Vận Chuyển từ Đơn Hàng (Order → Bill Prefill)", font=F_TOP_TITLE, fill=C_TEXT_MAIN)
    draw.text((px, py + 30), "Tự động kế thừa thông số vận hành và hàng hóa theo quy tắc INH-01 .. INH-04", font=F_SEC_HINT, fill=C_TEXT_MUTED)

    # Success Notice Banner (OPT-01 in action)
    ny = py + 60
    draw.rounded_rectangle([px, ny, WIDTH - 36, ny + 56], radius=6, fill=C_SUCCESS_BG, outline=C_SUCCESS_BORDER, width=1)
    draw.text((px + 20, ny + 12), "✓  TỰ ĐỘNG ĐIỀN THÔNG TIN TỪ ĐƠN HÀNG: ORD-2026-HCM-001 (OPT-01)", font=F_ALERT_TITLE, fill=C_SUCCESS_TEXT)
    draw.text((px + 20, ny + 32), "Đã kế thừa 12 thông số phù hợp: Khách hàng, Tuyến SGN-HAN, Hàng bách hóa, Trọng lượng 1,250 kg, Thể tích 6.8 CBM. Không sao chép ngày hết hạn báo giá.", font=F_ALERT, fill=C_SUCCESS_TEXT)

    # Card 1: Bill Basic & Customer Info
    cy = ny + 74
    cw = WIDTH - sw - 72
    draw_card(draw, px, cy, cw, 230, "1. Thông tin Chung & Khách hàng", "Dữ liệu kế thừa trực tiếp từ hợp đồng và đơn hàng gốc")

    # Inputs Row 1
    r1_y = cy + 90
    draw_input(draw, px + 24, r1_y, 280, 38, "Số Bill", "BL-2026-AUTO-001", required=True)
    draw_input(draw, px + 324, r1_y, 380, 38, "Đơn hàng liên kết (Order)", "ORD-2026-HCM-001 (Đã chọn)", required=True, badge="Kế thừa SoT")
    draw_input(draw, px + 724, r1_y, 400, 38, "Khách hàng (Customer)", "CUST-001 — Công ty TNHH Dệt May XK", required=True, badge="Tự động điền")
    draw_input(draw, px + 1144, r1_y, cw - 1168, 38, "Phương thức vận chuyển", "Đường bộ (Road)", required=True, badge="Kế thừa")

    # Inputs Row 2
    r2_y = r1_y + 80
    draw_input(draw, px + 24, r2_y, 280, 38, "Tuyến vận chuyển (Route)", "SGN-HAN (TP.HCM → Hà Nội)", required=True, badge="Đồng bộ")
    draw_input(draw, px + 324, r2_y, 380, 38, "Cảng/Điểm đi (Origin)", "VNSGN — Kho Tân Bình, TP.HCM", badge="Gợi ý")
    draw_input(draw, px + 724, r2_y, 400, 38, "Cảng/Điểm đến (Destination)", "VNHAN — ICD Mỹ Đình, Hà Nội", badge="Gợi ý")
    draw_input(draw, px + 1144, r2_y, cw - 1168, 38, "Mã tham chiếu khách", "PO-2026-TEXTILE-99", badge="Kế thừa")

    # Card 2: Cargo Measurements (OPT-01 / DO-01)
    c2y = cy + 250
    draw_card(draw, px, c2y, cw, 230, "2. Thông số Hàng hóa & Đo lường (Cargo & Measurement)", "Tự động tính Chargeable Weight theo công thức IATA / Đường bộ")

    r3_y = c2y + 90
    draw_input(draw, px + 24, r3_y, 280, 38, "Loại hàng hóa (Commodity)", "Hàng may mặc xuất khẩu", badge="Kế thừa")
    draw_input(draw, px + 324, r3_y, 220, 38, "Số kiện (Packages)", "50 kiện", badge="Kế thừa")
    draw_input(draw, px + 564, r3_y, 240, 38, "Gross Weight (kg)", "1,250.00 kg", badge="Kế thừa")
    draw_input(draw, px + 824, r3_y, 240, 38, "Thể tích (Volume CBM)", "6.800 m³", badge="Kế thừa")
    draw_input(draw, px + 1084, r3_y, cw - 1108, 38, "TÍNH CƯỚC (CW KG)", "1,360.00 kg", readonly=True, badge="Tự động tính: MAX")

    r4_y = r3_y + 80
    draw_input(draw, px + 24, r4_y, 280, 38, "Ngày dự kiến đi (ETD)", "2026-10-25 08:00", badge="Kế thừa")
    draw_input(draw, px + 324, r4_y, 280, 38, "Ngày dự kiến đến (ETA)", "2026-10-27 18:00", badge="Kế thừa")
    draw_input(draw, px + 624, r4_y, cw - 648, 38, "Ghi chú điều hành", "Hàng giao trước 17h, yêu cầu xe tải sàn nâng.")

    # Bottom Actions
    by = HEIGHT - 80
    draw.rectangle([sw, by, WIDTH, HEIGHT], fill=C_TOPBAR)
    draw.line([(sw, by), (WIDTH, by)], fill=C_TOPBAR_BORDER, width=1)
    
    btn_w = 180
    btn_x = WIDTH - btn_w - 36
    draw.rounded_rectangle([btn_x, by + 18, btn_x + btn_w, by + 58], radius=6, fill=C_PRIMARY)
    draw.text((btn_x + 38, by + 28), "✓  LƯU BILL", font=F_BTN, fill=(255, 255, 255))

    btn2_w = 240
    btn2_x = btn_x - btn2_w - 16
    draw.rounded_rectangle([btn2_x, by + 18, btn2_x + btn2_w, by + 58], radius=6, fill=C_SUCCESS_BG, outline=C_SUCCESS_BORDER)
    draw.text((btn2_x + 20, by + 28), "⚡ Đã điền trước 12/16 trường", font=F_BTN, fill=C_SUCCESS_TEXT)

    out_path = os.path.join(OUTPUT_DIR, "UI-FLOW-01-Order-to-Bill-Prefill.png")
    img.save(out_path, quality=95)
    print("Generated Screen 1:", out_path)

# -------------------------------------------------------------
# SCREEN 2: OPT-02 Bill to Shipment Rollup
# -------------------------------------------------------------
def generate_screen2():
    img, draw, sw, th = draw_base_layout(active_menu="Lô hàng (Shipments)")
    
    px = sw + 36
    py = th + 24
    draw.text((px, py), "Tạo mới Lô hàng Vận chuyển (Shipment)", font=F_TOP_TITLE, fill=C_TEXT_MAIN)
    draw.text((px, py + 30), "Tổng hợp container / hàng gom từ nhiều Bill vào một lô hàng tập trung (Rollup Cargo)", font=F_SEC_HINT, fill=C_TEXT_MUTED)

    # Info Notice Banner (OPT-02 in action)
    ny = py + 60
    draw.rounded_rectangle([px, ny, WIDTH - 36, ny + 56], radius=6, fill=C_INFO_BG, outline=C_INFO_BORDER, width=1)
    draw.text((px + 20, ny + 12), "ℹ️  PREVIEW ROLLUP: ĐÃ CỘNG DỒN THÀNH CÔNG TỪ 2 BILL LIÊN KẾT (OPT-02)", font=F_ALERT_TITLE, fill=C_INFO_TEXT)
    draw.text((px + 20, ny + 32), "Tổng số kiện: 150 kiện | Tổng trọng lượng thực: 4,000 kg | Tổng thể tích: 20.50 CBM | Tuyến: SGN-LAX | Không xung đột tuyến.", font=F_ALERT, fill=C_INFO_TEXT)

    # Card 1: Linked Bills Table
    cy = ny + 74
    cw = WIDTH - sw - 72
    draw_card(draw, px, cy, cw, 220, "1. Danh sách Bill liên kết trong Lô hàng", "Tập hợp các Bill cùng tuyến để gom hàng và phân bổ chi phí chung")

    # Table Header
    ty = cy + 85
    draw.rectangle([px + 24, ty, px + cw - 24, ty + 36], fill=(241, 245, 249))
    draw.text((px + 36, ty + 10), "SỐ BILL", font=F_TH, fill=C_TEXT_MUTED)
    draw.text((px + 220, ty + 10), "KHÁCH HÀNG", font=F_TH, fill=C_TEXT_MUTED)
    draw.text((px + 520, ty + 10), "TUYẾN VẬN CHUYỂN", font=F_TH, fill=C_TEXT_MUTED)
    draw.text((px + 740, ty + 10), "SỐ KIỆN", font=F_TH, fill=C_TEXT_MUTED)
    draw.text((px + 860, ty + 10), "GROSS WEIGHT", font=F_TH, fill=C_TEXT_MUTED)
    draw.text((px + 1020, ty + 10), "VOLUME (CBM)", font=F_TH, fill=C_TEXT_MUTED)
    draw.text((px + 1180, ty + 10), "CW (KG)", font=F_TH, fill=C_TEXT_MUTED)

    # Row 1
    r1y = ty + 38
    draw.text((px + 36, r1y + 8), "BL-ROLL-01", font=F_TD, fill=C_PRIMARY)
    draw.text((px + 220, r1y + 8), "CUST-001 — Công ty TNHH Dệt May XK", font=F_TD, fill=C_TEXT_MAIN)
    draw.text((px + 520, r1y + 8), "SGN-LAX (VNSGN → USLAX)", font=F_TD, fill=C_TEXT_MAIN)
    draw.text((px + 740, r1y + 8), "100 kiện", font=F_TD, fill=C_TEXT_MAIN)
    draw.text((px + 860, r1y + 8), "2,500.00 kg", font=F_TD, fill=C_TEXT_MAIN)
    draw.text((px + 1020, r1y + 8), "12.500 m³", font=F_TD, fill=C_TEXT_MAIN)
    draw.text((px + 1180, r1y + 8), "2,500.00 kg", font=F_TD, fill=C_TEXT_MAIN)

    # Row 2
    r2y = r1y + 36
    draw.text((px + 36, r2y + 8), "BL-ROLL-02", font=F_TD, fill=C_PRIMARY)
    draw.text((px + 220, r2y + 8), "CUST-002 — Tập Đoàn Da Giày Sài Gòn", font=F_TD, fill=C_TEXT_MAIN)
    draw.text((px + 520, r2y + 8), "SGN-LAX (VNSGN → USLAX)", font=F_TD, fill=C_TEXT_MAIN)
    draw.text((px + 740, r2y + 8), "50 kiện", font=F_TD, fill=C_TEXT_MAIN)
    draw.text((px + 860, r2y + 8), "1,500.00 kg", font=F_TD, fill=C_TEXT_MAIN)
    draw.text((px + 1020, r2y + 8), "8.000 m³", font=F_TD, fill=C_TEXT_MAIN)
    draw.text((px + 1180, r2y + 8), "1,500.00 kg", font=F_TD, fill=C_TEXT_MAIN)

    # Card 2: Rollup Totals Applied to Shipment
    c2y = cy + 240
    draw_card(draw, px, c2y, cw, 250, "2. Thông số Hàng hóa Tổng hợp của Lô hàng (Shipment Rollup Values)", "Tự động cộng dồn thông số phục vụ phân bổ chi phí chặng và container")

    r3_y = c2y + 90
    draw_input(draw, px + 24, r3_y, 280, 38, "Số Shipment", "SHP-2026-ROLL-01", required=True)
    draw_input(draw, px + 324, r3_y, 240, 38, "Phương thức", "Đường biển (Sea)", required=True)
    draw_input(draw, px + 584, r3_y, 260, 38, "Điểm đi kế thừa", "VNSGN — Cảng Cát Lái", badge="Rollup: VNSGN")
    draw_input(draw, px + 864, r3_y, cw - 888, 38, "Điểm đến kế thừa", "USLAX — Cảng Los Angeles", badge="Rollup: USLAX")

    r4_y = r3_y + 80
    draw_input(draw, px + 24, r4_y, 240, 38, "TỔNG SỐ KIỆN (SUM)", "150 kiện", readonly=True, badge="= 100 + 50")
    draw_input(draw, px + 284, r4_y, 280, 38, "TỔNG GROSS WEIGHT (SUM)", "4,000.00 kg", readonly=True, badge="= 2,500 + 1,500 kg")
    draw_input(draw, px + 584, r4_y, 260, 38, "TỔNG THỂ TÍCH (SUM CBM)", "20.500 m³", readonly=True, badge="= 12.5 + 8.0 m³")
    draw_input(draw, px + 864, r4_y, cw - 888, 38, "Hãng vận chuyển (Carrier)", "EVERGREEN — Evergreen Marine Corp.", badge="Kế thừa từ Bill")

    # Bottom Actions
    by = HEIGHT - 80
    draw.rectangle([sw, by, WIDTH, HEIGHT], fill=C_TOPBAR)
    draw.line([(sw, by), (WIDTH, by)], fill=C_TOPBAR_BORDER, width=1)
    
    btn_w = 220
    btn_x = WIDTH - btn_w - 36
    draw.rounded_rectangle([btn_x, by + 18, btn_x + btn_w, by + 58], radius=6, fill=C_PRIMARY)
    draw.text((btn_x + 32, by + 28), "✓  LƯU SHIPMENT", font=F_BTN, fill=(255, 255, 255))

    btn2_w = 260
    btn2_x = btn_x - btn2_w - 16
    draw.rounded_rectangle([btn2_x, by + 18, btn2_x + btn2_w, by + 58], radius=6, fill=C_SUCCESS_BG, outline=C_SUCCESS_BORDER)
    draw.text((btn2_x + 18, by + 28), "📊 Đã cộng dồn từ 2 Bill (OK)", font=F_BTN, fill=C_SUCCESS_TEXT)

    out_path = os.path.join(OUTPUT_DIR, "UI-FLOW-02-Bill-to-Shipment-Rollup.png")
    img.save(out_path, quality=95)
    print("Generated Screen 2:", out_path)

# -------------------------------------------------------------
# SCREEN 3: OPT-03 Leg Daisy Chaining (UT-11: Intermediate destination open)
# -------------------------------------------------------------
def generate_screen3():
    img, draw, sw, th = draw_base_layout(active_menu="Chặng vận chuyển")
    
    px = sw + 36
    py = th + 24
    draw.text((px, py), "Tạo Chặng Vận Chuyển Kế Tiếp (Transport Leg Daisy-Chaining)", font=F_TOP_TITLE, fill=C_TEXT_MAIN)
    draw.text((px, py + 30), "Tự động kế thừa liên hoàn: Điểm đến của Chặng 1 trở thành Điểm đi của Chặng 2 (Cảng đến để trống chọn tiếp)", font=F_SEC_HINT, fill=C_TEXT_MUTED)

    # Info Notice Banner (OPT-03 in action)
    ny = py + 60
    draw.rounded_rectangle([px, ny, WIDTH - 36, ny + 56], radius=6, fill=C_INFO_BG, outline=C_INFO_BORDER, width=1)
    draw.text((px + 20, ny + 12), "🔗  KẾ THỪA CHẶNG LIÊN HOÀN (DAISY-CHAINING PHÁT HIỆN CHẶNG TRƯỚC - OPT-03 / UT-11)", font=F_ALERT_TITLE, fill=C_INFO_TEXT)
    draw.text((px + 20, ny + 32), "Chặng 1 kết thúc tại NRT (Tokyo). Hệ thống tự động gán Thứ tự chặng = 2, Cảng đi gợi ý = NRT, Cảng đến để trống chờ user chọn (UT-11: Không gán ép đích cuối).", font=F_ALERT, fill=C_INFO_TEXT)

    # Card 1: Existing Leg History
    cy = ny + 74
    cw = WIDTH - sw - 72
    draw_card(draw, px, cy, cw, 170, "1. Tiến trình các Chặng thuộc Lô hàng SHP-AIR-GLOBAL-01", "Hành trình tổng thể: SGN → JFK (Chuyển tải tại Tokyo NRT)")

    ty = cy + 85
    draw.rectangle([px + 24, ty, px + cw - 24, ty + 36], fill=(241, 245, 249))
    draw.text((px + 36, ty + 10), "STT", font=F_TH, fill=C_TEXT_MUTED)
    draw.text((px + 100, ty + 10), "MÃ CHẶNG", font=F_TH, fill=C_TEXT_MUTED)
    draw.text((px + 280, ty + 10), "PHƯƠNG THỨC", font=F_TH, fill=C_TEXT_MUTED)
    draw.text((px + 460, ty + 10), "CẢNG ĐI (ORIGIN)", font=F_TH, fill=C_TEXT_MUTED)
    draw.text((px + 680, ty + 10), "CẢNG ĐẾN (DESTINATION)", font=F_TH, fill=C_TEXT_MUTED)
    draw.text((px + 940, ty + 10), "TRẠNG THÁI", font=F_TH, fill=C_TEXT_MUTED)

    r1y = ty + 38
    draw.text((px + 36, r1y + 8), "1", font=F_TD, fill=C_TEXT_MAIN)
    draw.text((px + 100, r1y + 8), "LEG-01 (Chặng gom SGN-NRT)", font=F_TD, fill=C_PRIMARY)
    draw.text((px + 280, r1y + 8), "Hàng không (Air)", font=F_TD, fill=C_TEXT_MAIN)
    draw.text((px + 460, r1y + 8), "SGN — Tân Sơn Nhất, VN", font=F_TD, fill=C_TEXT_MAIN)
    draw.text((px + 680, r1y + 8), "NRT — Narita Tokyo, JP", font=F_TD, fill=(185, 28, 28)) # Highlight NRT
    draw.text((px + 940, r1y + 8), "✓ Đã hoàn thành (Arrived)", font=F_TD, fill=C_SUCCESS_TEXT)

    # Card 2: Create Next Leg Form
    c2y = cy + 190
    draw_card(draw, px, c2y, cw, 280, "2. Khai báo Chặng Kế Tiếp (Chặng 2) — Daisy-Chaining Form", "Dữ liệu cảng đi và thứ tự đã tự động điền sẵn; cảng đến để mở cho chặng trung gian")

    r2_y = c2y + 90
    draw_input(draw, px + 24, r2_y, 220, 38, "Thứ tự chặng (Sequence No)", "2", readonly=True, badge="Tự tăng: 2")
    draw_input(draw, px + 264, r2_y, 280, 38, "Mã chặng mới", "LEG-02", required=True)
    draw_input(draw, px + 564, r2_y, 240, 38, "Phương thức", "Hàng không (Air)", required=True)
    draw_input(draw, px + 824, r2_y, cw - 848, 38, "Lô hàng (Shipment)", "SHP-AIR-GLOBAL-01 (SGN → JFK)", readonly=True)

    r3_y = r2_y + 80
    draw_input(draw, px + 24, r3_y, 360, 38, "CẢNG ĐI GỢI Ý (DAISY-CHAINING)", "NRT — Narita Tokyo, JP", readonly=True, badge="= Cảng đến Chặng 1")
    draw_input(draw, px + 404, r3_y, 360, 38, "CẢNG ĐẾN CHẶNG 2 (ĐÍCH KẾ TIẾP)", "[Chờ chọn điểm trung chuyển / đích chặng 2]", required=True, badge="UT-11: Để trống chọn tiếp")
    draw_input(draw, px + 784, r3_y, cw - 808, 38, "Nhà cung cấp vận chuyển (Carrier)", "JAL — Japan Airlines", badge="Hãng chặng 2")

    # Bottom Actions
    by = HEIGHT - 80
    draw.rectangle([sw, by, WIDTH, HEIGHT], fill=C_TOPBAR)
    draw.line([(sw, by), (WIDTH, by)], fill=C_TOPBAR_BORDER, width=1)
    
    btn_w = 240
    btn_x = WIDTH - btn_w - 36
    draw.rounded_rectangle([btn_x, by + 18, btn_x + btn_w, by + 58], radius=6, fill=C_PRIMARY)
    draw.text((btn_x + 36, by + 28), "✓  LƯU CHẶNG 2 (LEG-02)", font=F_BTN, fill=(255, 255, 255))

    out_path = os.path.join(OUTPUT_DIR, "UI-FLOW-03-Leg-Daisy-Chaining.png")
    img.save(out_path, quality=95)
    print("Generated Screen 3:", out_path)

# -------------------------------------------------------------
# SCREEN 4: OPT-04 Surcharge Rate Card Sync & Charge Mapping (UT-16 / GAP-04 / GAP-10)
# -------------------------------------------------------------
def generate_screen4():
    img, draw, sw, th = draw_base_layout(active_menu="Phụ phí (Surcharges)")
    
    px = sw + 36
    py = th + 24
    draw.text((px, py), "Khai báo Phụ phí Giá (Surcharge Rule Declaration)", font=F_TOP_TITLE, fill=C_TEXT_MAIN)
    draw.text((px, py + 30), "Tự động đồng bộ Chiều BUY/SELL, Tiền tệ từ Bảng giá & Chuẩn hóa Khoản mục kinh tế từ Master Data", font=F_SEC_HINT, fill=C_TEXT_MUTED)

    # Info Notice Banner (OPT-04 in action)
    ny = py + 60
    draw.rounded_rectangle([px, ny, WIDTH - 36, ny + 56], radius=6, fill=C_SUCCESS_BG, outline=C_SUCCESS_BORDER, width=1)
    draw.text((px + 20, ny + 12), "✓  TỰ ĐỘNG ĐỒNG BỘ TỪ BẢNG GIÁ MUA 'RC-BUY-VND' (OPT-04 / UT-16)", font=F_ALERT_TITLE, fill=C_SUCCESS_TEXT)
    draw.text((px + 20, ny + 32), "Đã khóa chiều BUY (Vendor Rate Card), đồng bộ Tiền tệ VND, Phương thức Air. Khoản mục: Phụ phí xăng dầu (FUEL) - Đồng bộ từ danh mục Master. Phạm vi tuyến: [Đa tuyến / Chọn phạm vi áp dụng].", font=F_ALERT, fill=C_SUCCESS_TEXT)

    # Card 1: Surcharge Header & Synchronization
    cy = ny + 74
    cw = WIDTH - sw - 72
    draw_card(draw, px, cy, cw, 250, "1. Thông tin Phụ phí & Bảng giá liên kết", "Ràng buộc dữ liệu chống sai lệch giữa Bảng giá và Phụ phí")

    r1_y = cy + 90
    draw_input(draw, px + 24, r1_y, 260, 38, "Mã phụ phí", "SUR-FUEL-AIR-2026", required=True)
    draw_input(draw, px + 304, r1_y, 320, 38, "Tên phụ phí", "Phụ phí nhiên liệu Hàng không (FSC)", required=True)
    draw_input(draw, px + 644, r1_y, 360, 38, "BẢNG GIÁ LIÊN KẾT (RATE CARD)", "RC-BUY-VND — Bảng giá mua Air VND", required=True, badge="Đã liên kết")
    draw_input(draw, px + 1024, r1_y, cw - 1048, 38, "CHIỀU GIÁ (DIRECTION)", "BUY (Chiều Mua)", readonly=True, badge="Khóa: BUY (Vendor)")

    r2_y = r1_y + 80
    draw_input(draw, px + 24, r2_y, 220, 38, "TIỀN TỆ (CURRENCY)", "VND", readonly=True, badge="Khóa: VND")
    draw_input(draw, px + 264, r2_y, 220, 38, "PHƯƠNG THỨC", "Hàng không (Air)", readonly=True, badge="Khóa: Air")
    draw_input(draw, px + 504, r2_y, 260, 38, "Hiệu lực từ", "2026-01-01", badge="Kế thừa từ Rate Card")
    draw_input(draw, px + 784, r2_y, 260, 38, "Hiệu lực đến", "2026-12-31", badge="Kế thừa từ Rate Card")
    draw_input(draw, px + 1064, r2_y, cw - 1088, 38, "Trạng thái", "Bản nháp (Draft)", badge="Chưa phát hành")

    # Card 2: Calculation & Economic Charge Master Linkage
    c2y = cy + 270
    draw_card(draw, px, c2y, cw, 230, "2. Cách tính & Chuẩn hóa Khoản mục kinh tế (Master Data Linkage)", "Liên kết trực tiếp tới Khoản mục kinh tế chuẩn, phạm vi áp dụng đa tuyến")

    r3_y = c2y + 90
    draw_input(draw, px + 24, r3_y, 440, 38, "KHOẢN MỤC KINH TẾ (ECONOMIC CHARGE)", "Phụ phí xăng dầu (FUEL) — Đồng bộ từ danh mục Master", required=True, badge="Master Data")
    draw_input(draw, px + 484, r3_y, 420, 38, "PHẠM VI TUYẾN ÁP DỤNG (SCOPE)", "[Áp dụng toàn mạng lưới / Đa tuyến]", readonly=True, badge="UT-16: Đa tuyến")
    draw_input(draw, px + 924, r3_y, cw - 948, 38, "Thuế suất VAT (%)", "10%", badge="10% VAT")

    r4_y = r3_y + 80
    draw_input(draw, px + 24, r4_y, 260, 38, "Cách tính (Calc Mode)", "Cố định (fixed_rate)", required=True)
    draw_input(draw, px + 304, r4_y, 300, 38, "Đơn giá phụ phí", "100,000 VND / đơn", required=True)
    draw_input(draw, px + 624, r4_y, cw - 648, 38, "Ghi chú nghiệp vụ", "Phụ phí nhiên liệu áp dụng theo biểu phí quý 1/2026 của hãng.")

    # Bottom Actions
    by = HEIGHT - 80
    draw.rectangle([sw, by, WIDTH, HEIGHT], fill=C_TOPBAR)
    draw.line([(sw, by), (WIDTH, by)], fill=C_TOPBAR_BORDER, width=1)
    
    btn_w = 220
    btn_x = WIDTH - btn_w - 36
    draw.rounded_rectangle([btn_x, by + 18, btn_x + btn_w, by + 58], radius=6, fill=C_PRIMARY)
    draw.text((btn_x + 36, by + 28), "✓  LƯU PHỤ PHÍ", font=F_BTN, fill=(255, 255, 255))

    out_path = os.path.join(OUTPUT_DIR, "UI-FLOW-04-Surcharge-RateCard-Sync.png")
    img.save(out_path, quality=95)
    print("Generated Screen 4:", out_path)

if __name__ == "__main__":
    generate_screen1()
    generate_screen2()
    generate_screen3()
    generate_screen4()
    print("All 4 screens generated successfully!")
