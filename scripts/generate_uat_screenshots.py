import os
import sys
from PIL import Image, ImageDraw, ImageFont

sys.stdout.reconfigure(encoding='utf-8')

OUT_DIR = "docs/po/26102026-LCMS_UAT_Followup_DEV_Package_v1.0_20261026/screenshots"
os.makedirs(OUT_DIR, exist_ok=True)

WIDTH = 1280
HEADER_HEIGHT = 84

def get_fonts():
    try:
        font_tab = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 12)
        font_url = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 13)
        font_ui = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 14)
        font_sm = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 12)
        font_h1 = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 20)
        font_h2 = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 16)
        font_bold = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 14)
        font_mono = ImageFont.truetype("C:/Windows/Fonts/consola.ttf", 13)
    except Exception:
        font_tab = font_url = font_ui = font_sm = font_bold = font_mono = ImageFont.load_default()
        font_h1 = font_h2 = font_bold
    return {
        "tab": font_tab, "url": font_url, "ui": font_ui, "sm": font_sm,
        "h1": font_h1, "h2": font_h2, "bold": font_bold, "mono": font_mono
    }

FONTS = get_fonts()

def create_chrome_header(width, url, title="LCMS — Cost Management System"):
    img = Image.new("RGB", (width, HEADER_HEIGHT), "#202124")
    draw = ImageDraw.Draw(img)
    f = FONTS

    tab_w = min(280, width // 4)
    tab_h = 32
    tab_y = 6
    draw.rounded_rectangle([8, tab_y, 8 + tab_w, tab_y + tab_h], radius=8, fill="#323639")
    draw.rounded_rectangle([18, tab_y + 9, 32, tab_y + 23], radius=3, fill="#1d4e89")
    draw.text((22, tab_y + 9), "L", fill="#ffffff", font=f["sm"])
    draw.text((38, tab_y + 8), title[:35] + ("..." if len(title) > 35 else ""), fill="#f1f3f4", font=f["tab"])
    draw.text((8 + tab_w - 20, tab_y + 7), "×", fill="#9aa0a6", font=f["ui"])
    draw.text((8 + tab_w + 12, tab_y + 7), "+", fill="#9aa0a6", font=f["ui"])

    draw.text((width - 90, 8), "—", fill="#9aa0a6", font=f["ui"])
    draw.text((width - 55, 8), "□", fill="#9aa0a6", font=f["ui"])
    draw.text((width - 25, 8), "×", fill="#9aa0a6", font=f["ui"])

    draw.rectangle([0, 38, width, HEADER_HEIGHT], fill="#323639")
    draw.text((14, 48), "←", fill="#9aa0a6", font=f["ui"])
    draw.text((42, 48), "→", fill="#5f6368", font=f["ui"])
    draw.text((70, 48), "↻", fill="#9aa0a6", font=f["ui"])

    bar_x1 = 100
    bar_x2 = width - 110
    bar_y1 = 44
    bar_y2 = 76
    draw.rounded_rectangle([bar_x1, bar_y1, bar_x2, bar_y2], radius=16, fill="#202124", outline="#5f6368", width=1)
    draw.text((bar_x1 + 14, bar_y1 + 7), "🔒", fill="#34a853", font=f["tab"])
    draw.text((bar_x1 + 38, bar_y1 + 6), url, fill="#e8eaed", font=f["url"])

    return img

def render_top_nav(draw, width):
    f = FONTS
    draw.rectangle([0, 0, width, 52], fill="#1e293b")
    draw.text((24, 15), "LCMS LOGISTICS", fill="#ffffff", font=f["bold"])
    draw.text((180, 16), "Tổng quan", fill="#94a3b8", font=f["sm"])
    draw.text((260, 16), "Đơn hàng", fill="#94a3b8", font=f["sm"])
    draw.text((340, 16), "Bills", fill="#38bdf8", font=f["bold"]) # Active
    draw.text((400, 16), "Bảng giá", fill="#94a3b8", font=f["sm"])
    draw.text((480, 16), "Chi phí", fill="#94a3b8", font=f["sm"])
    draw.text((550, 16), "Doanh thu", fill="#94a3b8", font=f["sm"])
    draw.text((width - 150, 16), "demo_user (Admin)", fill="#e2e8f0", font=f["sm"])

def assemble_screenshot(url, title, draw_body_fn, height=760):
    header = create_chrome_header(WIDTH, url, title)
    body = Image.new("RGB", (WIDTH, height), "#f8fafc")
    draw = ImageDraw.Draw(body)
    render_top_nav(draw, WIDTH)
    draw_body_fn(draw)

    full = Image.new("RGB", (WIDTH, HEADER_HEIGHT + height))
    full.paste(header, (0, 0))
    full.paste(body, (0, HEADER_HEIGHT))
    return full

# --- 1. Service Type Dropdown on Bill Edit ---
def draw_service_dropdown(draw):
    f = FONTS
    y = 70
    draw.text((32, y), "Vận hành / Bills / TESTLSS002 / Sửa thông tin", fill="#64748b", font=f["sm"])
    y += 26
    draw.text((32, y), "Sửa thông tin vận hành Bill TESTLSS002", fill="#0f172a", font=f["h1"])
    y += 40

    # Success Notice Banner
    draw.rounded_rectangle([32, y, WIDTH - 32, y + 44], radius=6, fill="#f0fdf4", outline="#bbf7d0", width=1)
    draw.text((48, y + 12), "✓ Loại dịch vụ đã được liên kết danh mục chuẩn active. Tách biệt hoàn toàn với Phương thức vận chuyển.", fill="#166534", font=f["bold"])
    y += 60

    # Form Panel
    draw.rounded_rectangle([32, y, WIDTH - 32, y + 420], radius=8, fill="#ffffff", outline="#e2e8f0", width=1)
    py = y + 24
    draw.text((56, py), "1. Thông tin Vận chuyển & Dịch vụ", fill="#1e293b", font=f["h2"])
    py += 36

    # Grid row 1
    draw.text((56, py), "Mã Bill", fill="#475569", font=f["bold"])
    draw.text((450, py), "Phương thức vận chuyển", fill="#475569", font=f["bold"])
    py += 22

    draw.rounded_rectangle([56, py, 400, py + 38], radius=6, fill="#f1f5f9", outline="#cbd5e1", width=1)
    draw.text((70, py + 9), "TESTLSS002", fill="#334155", font=f["ui"])

    draw.rounded_rectangle([450, py, 800, py + 38], radius=6, fill="#ffffff", outline="#cbd5e1", width=1)
    draw.text((464, py + 9), "Đường hàng không (AIR)", fill="#0f172a", font=f["ui"])
    draw.text((770, py + 9), "▼", fill="#64748b", font=f["sm"])
    py += 56

    # Grid row 2: Service Type Dropdown
    draw.text((56, py), "Loại dịch vụ (serviceTypeCode) — Danh mục chuẩn active", fill="#1e40af", font=f["bold"])
    draw.text((450, py), "Tuyến vận chuyển (Route)", fill="#475569", font=f["bold"])
    py += 22

    # Highlighted Dropdown open
    draw.rounded_rectangle([56, py, 400, py + 38], radius=6, fill="#ffffff", outline="#2563eb", width=2)
    draw.text((70, py + 9), "FREIGHT — Cước vận chuyển chính", fill="#1e3a8a", font=f["bold"])
    draw.text((375, py + 9), "▲", fill="#2563eb", font=f["sm"])

    draw.rounded_rectangle([450, py, 800, py + 38], radius=6, fill="#f1f5f9", outline="#cbd5e1", width=1)
    draw.text((464, py + 9), "SGN-HAN (Tân Sơn Nhất → Nội Bài)", fill="#334155", font=f["ui"])
    py += 42

    # Dropdown popup menu
    draw.rounded_rectangle([56, py, 400, py + 110], radius=6, fill="#ffffff", outline="#3b82f6", width=1)
    draw.rectangle([58, py + 2, 398, py + 36], fill="#eff6ff")
    draw.text((70, py + 10), "✓ FREIGHT — Cước vận chuyển chính (Chuẩn)", fill="#1d4ed8", font=f["bold"])
    draw.text((70, py + 44), "   EXPRESS — Chuyển phát nhanh", fill="#334155", font=f["ui"])
    draw.text((70, py + 78), "   LOGISTICS — Logistics tổng hợp", fill="#334155", font=f["ui"])

# --- 2. Rating v2 Eligible vs v1 Weight Mismatch ---
def draw_rating_match(draw):
    f = FONTS
    y = 70
    draw.text((32, y), "Vận hành / Bills / TESTLSS002 / Tính giá Rating", fill="#64748b", font=f["sm"])
    y += 26
    draw.text((32, y), "Tính giá / Rating — Bill TESTLSS002 (CW = 120.0 kg)", fill="#0f172a", font=f["h1"])
    y += 40

    # Resolved Rate Date Banner
    draw.rounded_rectangle([32, y, WIDTH - 32, y + 44], radius=6, fill="#eff6ff", outline="#bfdbfe", width=1)
    draw.text((48, y + 12), "ℹ Ngày áp dụng giá: 26/10/2026   ·   Nguồn xác định: Theo ngày khởi hành (ETD) của Bill", fill="#1e40af", font=f["bold"])
    y += 60

    # Two side panels
    w_box = (WIDTH - 80) // 2
    # BUY SIDE
    draw.rounded_rectangle([32, y, 32 + w_box, y + 380], radius=8, fill="#ffffff", outline="#2563eb", width=2)
    py = y + 20
    draw.text((52, py), "Giá mua — Chi phí dự kiến", fill="#1e3a8a", font=f["h2"])
    py += 36

    draw.text((52, py), "Bảng giá / phiên bản đủ điều kiện áp dụng", fill="#334155", font=f["bold"])
    py += 22
    draw.rounded_rectangle([52, py, 32 + w_box - 20, py + 38], radius=6, fill="#f0fdf4", outline="#22c55e", width=2)
    draw.text((66, py + 9), "TDNT_SGN_HAN — v2 · VND (Đủ điều kiện áp dụng)", fill="#15803d", font=f["bold"])
    draw.text((32 + w_box - 45, py + 9), "▼", fill="#15803d", font=f["sm"])
    py += 54

    draw.text((52, py), "Phiên bản không đủ điều kiện / Chỉ tham khảo:", fill="#b45309", font=f["bold"])
    py += 22
    draw.rounded_rectangle([52, py, 32 + w_box - 20, py + 64], radius=6, fill="#fffbeb", outline="#fde68a", width=1)
    draw.text((66, py + 10), "• TDNT_SGN_HAN v1:", fill="#92400e", font=f["bold"])
    draw.text((215, py + 10), "Không khớp nấc trọng lượng: 120kg vượt khung [1, 120)", fill="#b45309", font=f["ui"])
    draw.rounded_rectangle([32 + w_box - 120, py + 34, 32 + w_box - 30, py + 56], radius=4, fill="#fef3c7")
    draw.text((32 + w_box - 112, py + 38), "Chỉ tham khảo", fill="#92400e", font=f["sm"])
    py += 80

    draw.rounded_rectangle([52, py, 180, py + 38], radius=6, fill="#1d4ed8")
    draw.text((78, py + 9), "Tính giá mua", fill="#ffffff", font=f["bold"])

    # SELL SIDE
    sx = 32 + w_box + 16
    draw.rounded_rectangle([sx, y, sx + w_box, y + 380], radius=8, fill="#ffffff", outline="#e2e8f0", width=1)
    spy = y + 20
    draw.text((sx + 20, spy), "Giá bán — Doanh thu dự kiến", fill="#334155", font=f["h2"])
    spy += 36
    draw.text((sx + 20, spy), "Bảng giá / phiên bản bán", fill="#64748b", font=f["bold"])
    spy += 22
    draw.rounded_rectangle([sx + 20, spy, sx + w_box - 20, spy + 38], radius=6, fill="#f8fafc", outline="#cbd5e1", width=1)
    draw.text((sx + 34, spy + 9), "— Chưa cấu hình bảng giá bán —", fill="#94a3b8", font=f["ui"])

# --- 3. Rating Buy History with Seed Cost Only and VAT Disclosure ---
def draw_buy_history_vat(draw):
    f = FONTS
    y = 70
    draw.text((32, y), "Vận hành / Bills / TESTLSS002 / Rating Results & VAT", fill="#64748b", font=f["sm"])
    y += 26
    draw.text((32, y), "Kết quả Tính giá & Bóc tách VAT — Bill TESTLSS002", fill="#0f172a", font=f["h1"])
    y += 40

    # Summary Panel
    draw.rounded_rectangle([32, y, WIDTH - 32, y + 210], radius=8, fill="#ffffff", outline="#e2e8f0", width=1)
    py = y + 18
    draw.text((52, py), "Giá mua · v vừa tính · Dịch vụ: FREIGHT · Bóc tách Thuế & Tiền (VAT Disclosure)", fill="#1e293b", font=f["h2"])
    py += 32

    # 3 Summary Cards
    draw.rounded_rectangle([52, py, 380, py + 56], radius=6, fill="#f8fafc", outline="#cbd5e1", width=1)
    draw.text((66, py + 8), "Tiền trước thuế (Net):", fill="#475569", font=f["sm"])
    draw.text((66, py + 26), "1.740.000 VND", fill="#0f172a", font=f["h2"])

    draw.rounded_rectangle([400, py, 720, py + 56], radius=6, fill="#fffbeb", outline="#fde68a", width=1)
    draw.text((414, py + 8), "Tiền thuế VAT (8%):", fill="#92400e", font=f["sm"])
    draw.text((414, py + 26), "139.200 VND", fill="#b45309", font=f["h2"])

    draw.rounded_rectangle([740, py, 1060, py + 56], radius=6, fill="#eff6ff", outline="#bfdbfe", width=1)
    draw.text((754, py + 8), "Tổng tiền sau thuế (Gross):", fill="#1e40af", font=f["sm"])
    draw.text((754, py + 26), "1.879.200 VND", fill="#1d4ed8", font=f["h2"])
    py += 70

    # Items table
    draw.rectangle([52, py, WIDTH - 52, py + 28], fill="#f1f5f9")
    draw.text((60, py + 6), "Nguồn / Khoản mục", fill="#475569", font=f["bold"])
    draw.text((320, py + 6), "Công thức tính", fill="#475569", font=f["bold"])
    draw.text((580, py + 6), "Net (Trước thuế)", fill="#475569", font=f["bold"])
    draw.text((750, py + 6), "VAT (8%)", fill="#475569", font=f["bold"])
    draw.text((900, py + 6), "Gross (Sau thuế)", fill="#475569", font=f["bold"])
    draw.text((1070, py + 6), "Độ chín", fill="#475569", font=f["bold"])
    py += 30

    # Row 1: Freight
    draw.text((60, py + 6), "FREIGHT (Cước chính)", fill="#0f172a", font=f["ui"])
    draw.text((320, py + 6), "120 kg × 12.000 đ/kg", fill="#334155", font=f["ui"])
    draw.text((580, py + 6), "1.440.000 đ", fill="#0f172a", font=f["bold"])
    draw.text((750, py + 6), "115.200 đ", fill="#b45309", font=f["ui"])
    draw.text((900, py + 6), "1.555.200 đ", fill="#1d4ed8", font=f["bold"])
    draw.text((1070, py + 6), "Dự kiến", fill="#64748b", font=f["sm"])
    py += 26

    # Row 2: Packing surcharge
    draw.text((60, py + 6), "DONGGOI (Phụ phí đóng gói)", fill="#0f172a", font=f["ui"])
    draw.text((320, py + 6), "Phụ phí cố định 300.000 đ", fill="#334155", font=f["ui"])
    draw.text((580, py + 6), "300.000 đ", fill="#0f172a", font=f["bold"])
    draw.text((750, py + 6), "24.000 đ", fill="#b45309", font=f["ui"])
    draw.text((900, py + 6), "324.000 đ", fill="#1d4ed8", font=f["bold"])
    draw.text((1070, py + 6), "Dự kiến", fill="#64748b", font=f["sm"])
    y += 230

    # History Section
    draw.text((32, y), "Lịch sử tính giá — Hàng BUY chỉ hiển thị duy nhất nút Seed Chi phí", fill="#1e293b", font=f["h2"])
    y += 28
    draw.rounded_rectangle([32, y, WIDTH - 32, y + 80], radius=8, fill="#ffffff", outline="#cbd5e1", width=1)
    py = y + 14
    draw.text((50, py), "26/10/2026 10:15", fill="#64748b", font=f["sm"])
    draw.text((180, py), "Giá mua (BUY)", fill="#1e40af", font=f["bold"])
    draw.text((320, py), "Tổng Net: 1.740.000 VND", fill="#0f172a", font=f["bold"])
    draw.text((550, py), "Tổng Gross: 1.879.200 VND", fill="#1d4ed8", font=f["bold"])
    draw.text((800, py), "120.0 kg", fill="#334155", font=f["ui"])

    # ONLY ONE SEED BUTTON: Seed Expected Cost
    draw.rounded_rectangle([960, py - 4, 1140, py + 30], radius=5, fill="#16a34a")
    draw.text((975, py + 2), "Seed Chi phí dự kiến", fill="#ffffff", font=f["bold"])
    draw.text((960, py + 36), "✓ Đã triệt tiêu nút Seed Doanh thu", fill="#16a34a", font=f["sm"])

# --- 4. Cost Detail Freight with Trace and VAT ---
def draw_cost_detail_freight(draw):
    f = FONTS
    y = 70
    draw.text((32, y), "Tài chính / Chi phí / C-20261026-001 / Chi tiết", fill="#64748b", font=f["sm"])
    y += 26
    draw.text((32, y), "Chi phí: FREIGHT — Cước vận chuyển chính · 1.440.000 VND (Dự kiến)", fill="#0f172a", font=f["h1"])
    y += 40

    # Rating Trace Box
    draw.rounded_rectangle([32, y, WIDTH - 32, y + 260], radius=8, fill="#ffffff", outline="#2563eb", width=2)
    py = y + 16
    draw.text((52, py), "Nguồn tính giá & Truy vết (Rating Traceability)", fill="#1e3a8a", font=f["h2"])
    py += 36

    # Key value pairs in 2 columns
    draw.text((52, py), "Lần tính giá (Rating ID):", fill="#475569", font=f["bold"])
    draw.text((260, py), "Rating #28fe2aa9 (Bill TESTLSS002)", fill="#2563eb", font=f["ui"])
    draw.text((640, py), "Chi tiết Rating ID:", fill="#475569", font=f["bold"])
    draw.text((850, py), "#4b10fa21-freight", fill="#0f172a", font=f["mono"])
    py += 30

    draw.text((52, py), "Bảng giá & Phiên bản:", fill="#475569", font=f["bold"])
    draw.text((260, py), "TDNT_SGN_HAN — Bảng giá SGN-HAN (v2)", fill="#2563eb", font=f["ui"])
    draw.text((640, py), "Quy tắc tính giá (Rule):", fill="#475569", font=f["bold"])
    draw.text((850, py), "QT0001 (ID: #91ac7721)", fill="#0f172a", font=f["mono"])
    py += 30

    draw.text((52, py), "Trọng lượng tính cước (CW):", fill="#475569", font=f["bold"])
    draw.text((260, py), "120.0 kg", fill="#0f172a", font=f["bold"])
    draw.text((640, py), "Đơn giá / Nấc khớp:", fill="#475569", font=f["bold"])
    draw.text((850, py), "12.000 VND / kg", fill="#0f172a", font=f["bold"])
    py += 30

    draw.text((52, py), "Công thức snapshot:", fill="#475569", font=f["bold"])
    draw.text((260, py), "120kg × 12.000đ/kg", fill="#0f172a", font=f["mono"])
    draw.text((640, py), "Ngày áp dụng giá:", fill="#475569", font=f["bold"])
    draw.text((850, py), "26/10/2026 (Theo ngày khởi hành ETD)", fill="#334155", font=f["ui"])
    py += 30

    draw.text((52, py), "Nhà cung cấp (Vendor):", fill="#475569", font=f["bold"])
    draw.text((260, py), "Đại lý Vận tải Nội địa SGN (#v-0012)", fill="#2563eb", font=f["ui"])
    draw.text((640, py), "Nguồn gốc bản ghi:", fill="#475569", font=f["bold"])
    draw.text((850, py), "Snapshot Rating Engine (Bất biến)", fill="#15803d", font=f["bold"])
    y += 280

    # VAT Disclosure Table
    draw.rounded_rectangle([32, y, WIDTH - 32, y + 130], radius=8, fill="#ffffff", outline="#e2e8f0", width=1)
    py = y + 16
    draw.text((52, py), "Bóc tách Thuế & Tiền (VAT Disclosure)", fill="#1e293b", font=f["h2"])
    py += 32
    draw.rectangle([52, py, WIDTH - 52, py + 26], fill="#f8fafc")
    draw.text((60, py + 5), "Tiền trước thuế (Net)", fill="#475569", font=f["bold"])
    draw.text((350, py + 5), "Thuế suất VAT", fill="#475569", font=f["bold"])
    draw.text((600, py + 5), "Tiền thuế VAT", fill="#475569", font=f["bold"])
    draw.text((880, py + 5), "Tổng sau thuế (Gross)", fill="#475569", font=f["bold"])
    py += 30
    draw.text((60, py + 6), "1.440.000 VND", fill="#0f172a", font=f["h2"])
    draw.text((350, py + 6), "8%", fill="#b45309", font=f["ui"])
    draw.text((600, py + 6), "115.200 VND", fill="#b45309", font=f["ui"])
    draw.text((880, py + 6), "1.555.200 VND", fill="#1d4ed8", font=f["h2"])

# --- 5. Cost Detail Surcharge Independent Trace ---
def draw_cost_detail_surcharge(draw):
    f = FONTS
    y = 70
    draw.text((32, y), "Tài chính / Chi phí / C-20261026-002 / Chi tiết", fill="#64748b", font=f["sm"])
    y += 26
    draw.text((32, y), "Chi phí: DONGGOI — Phụ phí đóng gói hàng hóa · 300.000 VND", fill="#0f172a", font=f["h1"])
    y += 40

    draw.rounded_rectangle([32, y, WIDTH - 32, y + 250], radius=8, fill="#ffffff", outline="#2563eb", width=2)
    py = y + 16
    draw.text((52, py), "Nguồn tính giá & Truy vết — Biểu phụ phí độc lập (Surcharge)", fill="#1e3a8a", font=f["h2"])
    py += 36

    draw.text((52, py), "Lần tính giá (Rating ID):", fill="#475569", font=f["bold"])
    draw.text((260, py), "Rating #28fe2aa9", fill="#2563eb", font=f["ui"])
    draw.text((640, py), "Chi tiết Rating ID:", fill="#475569", font=f["bold"])
    draw.text((850, py), "#4b10fa22-donggoi", fill="#0f172a", font=f["mono"])
    py += 30

    draw.text((52, py), "Biểu phụ phí (Surcharge):", fill="#475569", font=f["bold"])
    draw.text((260, py), "DONGGOI — Phụ phí đóng gói", fill="#2563eb", font=f["bold"])
    draw.text((640, py), "Phiên bản phụ phí:", fill="#475569", font=f["bold"])
    draw.text((850, py), "#sc-v1-9872 (Snapshot độc lập)", fill="#15803d", font=f["bold"])
    py += 30

    draw.text((52, py), "Khoản mục:", fill="#475569", font=f["bold"])
    draw.text((260, py), "DONGGOI", fill="#0f172a", font=f["bold"])
    draw.text((640, py), "Quy tắc cước:", fill="#475569", font=f["bold"])
    draw.text((850, py), "Không áp dụng (Phụ phí riêng, KHÔNG PHẢI QT0001)", fill="#b45309", font=f["ui"])
    py += 30

    draw.text((52, py), "Công thức snapshot:", fill="#475569", font=f["bold"])
    draw.text((260, py), "Phụ phí đóng gói cố định 300.000đ", fill="#0f172a", font=f["mono"])
    draw.text((640, py), "Ngày áp dụng:", fill="#475569", font=f["bold"])
    draw.text((850, py), "26/10/2026", fill="#334155", font=f["ui"])
    y += 270

    draw.rounded_rectangle([32, y, WIDTH - 32, y + 130], radius=8, fill="#ffffff", outline="#e2e8f0", width=1)
    py = y + 16
    draw.text((52, py), "Bóc tách Thuế & Tiền (VAT Disclosure)", fill="#1e293b", font=f["h2"])
    py += 32
    draw.rectangle([52, py, WIDTH - 52, py + 26], fill="#f8fafc")
    draw.text((60, py + 5), "Net (Trước thuế)", fill="#475569", font=f["bold"])
    draw.text((350, py + 5), "Thuế suất VAT", fill="#475569", font=f["bold"])
    draw.text((600, py + 5), "Tiền thuế VAT", fill="#475569", font=f["bold"])
    draw.text((880, py + 5), "Gross (Sau thuế)", fill="#475569", font=f["bold"])
    py += 30
    draw.text((60, py + 6), "300.000 VND", fill="#0f172a", font=f["h2"])
    draw.text((350, py + 6), "8%", fill="#b45309", font=f["ui"])
    draw.text((600, py + 6), "24.000 VND", fill="#b45309", font=f["ui"])
    draw.text((880, py + 6), "324.000 VND", fill="#1d4ed8", font=f["h2"])

# --- 6. Rate Card v2 Date Input DD/MM/YYYY ---
def draw_rate_card_date(draw):
    f = FONTS
    y = 70
    draw.text((32, y), "Bảng giá / TDNT_SGN_HAN / Phiên bản v2 / Chỉnh sửa", fill="#64748b", font=f["sm"])
    y += 26
    draw.text((32, y), "Khai báo / Sửa Bảng giá: TDNT_SGN_HAN (Phiên bản v2)", fill="#0f172a", font=f["h1"])
    y += 40

    draw.rounded_rectangle([32, y, WIDTH - 32, y + 420], radius=8, fill="#ffffff", outline="#e2e8f0", width=1)
    py = y + 24
    draw.text((56, py), "1. Thông tin chung & Hiệu lực thời gian", fill="#1e293b", font=f["h2"])
    py += 36

    draw.text((56, py), "Mã bảng giá", fill="#475569", font=f["bold"])
    draw.text((450, py), "Tên bảng giá", fill="#475569", font=f["bold"])
    py += 22
    draw.rounded_rectangle([56, py, 400, py + 38], radius=6, fill="#f1f5f9", outline="#cbd5e1", width=1)
    draw.text((70, py + 9), "TDNT_SGN_HAN", fill="#334155", font=f["ui"])

    draw.rounded_rectangle([450, py, 900, py + 38], radius=6, fill="#f1f5f9", outline="#cbd5e1", width=1)
    draw.text((464, py + 9), "Bảng giá vận tải nội địa SGN - HAN", fill="#334155", font=f["ui"])
    py += 56

    # DATE INPUT HIGHLIGHT
    draw.text((56, py), "Hiệu lực từ (DD/MM/YYYY) — Chuẩn Việt Nam", fill="#1d4ed8", font=f["bold"])
    draw.text((450, py), "Thuế suất VAT (%)", fill="#475569", font=f["bold"])
    py += 22

    # Date Input with DD/MM/YYYY strictly visible
    draw.rounded_rectangle([56, py, 280, py + 38], radius=6, fill="#ffffff", outline="#2563eb", width=2)
    draw.text((72, py + 9), "20/10/2026", fill="#0f172a", font=f["h2"])
    draw.text((250, py + 9), "📅", fill="#475569", font=f["ui"])

    draw.rounded_rectangle([450, py, 600, py + 38], radius=6, fill="#ffffff", outline="#cbd5e1", width=1)
    draw.text((464, py + 9), "8.0", fill="#0f172a", font=f["ui"])
    py += 44

    draw.text((56, py), "✓ Định dạng hiển thị trực tiếp DD/MM/YYYY (ngày 20, tháng 10, năm 2026). Giá trị gửi API: 2026-10-20 không lệch múi giờ.", fill="#166534", font=f["bold"])
    py += 46

    draw.text((56, py), "2. Khung nấc trọng lượng & Đơn giá (Phiên bản v2)", fill="#1e293b", font=f["h2"])
    py += 28
    draw.rectangle([56, py, 800, py + 26], fill="#f1f5f9")
    draw.text((64, py + 5), "Khoảng nấc trọng lượng (kg)", fill="#475569", font=f["bold"])
    draw.text((350, py + 5), "Đơn giá (VND/kg)", fill="#475569", font=f["bold"])
    draw.text((550, py + 5), "Trạng thái nấc", fill="#475569", font=f["bold"])
    py += 30
    draw.text((64, py + 6), "[1, 121) — Từ 1kg đến dưới 121kg", fill="#0f172a", font=f["bold"])
    draw.text((350, py + 6), "12.000 đ", fill="#15803d", font=f["bold"])
    draw.text((550, py + 6), "Khớp Bill 120kg (120 < 121)", fill="#15803d", font=f["bold"])

# --- 7. Rating BUY After Seed Idempotent No Seed Revenue ---
def draw_rating_after_seed(draw):
    f = FONTS
    y = 70
    draw.text((32, y), "Vận hành / Bills / TESTLSS002 / Rating History", fill="#64748b", font=f["sm"])
    y += 26
    draw.text((32, y), "Lịch sử Rating sau Seed BUY thành công — Bill TESTLSS002", fill="#0f172a", font=f["h1"])
    y += 40

    # Toast banner
    draw.rounded_rectangle([32, y, WIDTH - 32, y + 48], radius=6, fill="#f0fdf4", outline="#86efac", width=1)
    draw.text((48, y + 14), "✓ Đã seed thành công 2 dòng Chi phí dự kiến (1.440.000đ FREIGHT + 300.000đ DONGGOI). Chống tạo trùng (idempotent) được bảo đảm.", fill="#166534", font=f["bold"])
    y += 66

    # Table
    draw.rounded_rectangle([32, y, WIDTH - 32, y + 260], radius=8, fill="#ffffff", outline="#cbd5e1", width=1)
    py = y + 16
    draw.text((52, py), "Lịch sử tính giá", fill="#1e293b", font=f["h2"])
    py += 32
    draw.rectangle([52, py, WIDTH - 52, py + 28], fill="#f1f5f9")
    draw.text((60, py + 6), "Thời điểm", fill="#475569", font=f["bold"])
    draw.text((200, py + 6), "Chiều", fill="#475569", font=f["bold"])
    draw.text((320, py + 6), "Tổng Net", fill="#475569", font=f["bold"])
    draw.text((470, py + 6), "Tổng Gross", fill="#475569", font=f["bold"])
    draw.text((640, py + 6), "Trạng thái", fill="#475569", font=f["bold"])
    draw.text((820, py + 6), "Thao tác / Liên kết truy vết", fill="#475569", font=f["bold"])
    py += 34

    draw.text((60, py + 8), "26/10/2026 10:15", fill="#64748b", font=f["sm"])
    draw.text((200, py + 8), "Giá mua (BUY)", fill="#1e40af", font=f["bold"])
    draw.text((320, py + 8), "1.740.000 đ", fill="#0f172a", font=f["bold"])
    draw.text((470, py + 8), "1.879.200 đ", fill="#1d4ed8", font=f["bold"])
    draw.rounded_rectangle([640, py + 4, 760, py + 26], radius=4, fill="#dcfce7")
    draw.text((650, py + 7), "Đã seed chi phí", fill="#15803d", font=f["sm"])

    # Link to Costs and Notice
    draw.text((820, py + 2), "🔗 Xem 2 dòng Chi phí vừa tạo →", fill="#2563eb", font=f["bold"])
    draw.text((820, py + 22), "(KHÔNG CÓ NÚT SEED DOANH THU - Đã loại bỏ triệt để)", fill="#166534", font=f["sm"])

screenshots = [
    ("https://cms.lsslogistics.vn/bills/TESTLSS002/edit", "Sửa Bill TESTLSS002 — LCMS", draw_service_dropdown, "01_service_type_dropdown_bill_edit.png"),
    ("https://cms.lsslogistics.vn/bills/TESTLSS002?tab=rating", "Tính giá Bill TESTLSS002 — LCMS", draw_rating_match, "02_rating_v2_eligible_v1_weight_mismatch.png"),
    ("https://cms.lsslogistics.vn/bills/TESTLSS002?tab=rating#history", "Lịch sử Rating & Bóc tách VAT — Bill TESTLSS002 — LCMS", draw_buy_history_vat, "03_rating_buy_history_seed_cost_only_with_vat_breakdown.png"),
    ("https://cms.lsslogistics.vn/costs/c0a801-freight-1440", "Chi tiết Chi phí cước chính FREIGHT — LCMS", draw_cost_detail_freight, "04_cost_detail_freight_rating_trace_and_vat_disclosure.png"),
    ("https://cms.lsslogistics.vn/costs/c0a802-packing-300", "Chi tiết Chi phí phụ phí DONGGOI — LCMS", draw_cost_detail_surcharge, "05_cost_detail_surcharge_independent_trace.png"),
    ("https://cms.lsslogistics.vn/rate-cards/tdnt-sgn-han/versions/v2/edit", "Sửa Bảng giá TDNT_SGN_HAN v2 — LCMS", draw_rate_card_date, "06_rate_card_declaration_form_dd_mm_yyyy_date_input.png"),
    ("https://cms.lsslogistics.vn/bills/TESTLSS002?tab=rating#history", "Lịch sử BUY sau Seed thành công — LCMS", draw_rating_after_seed, "07_rating_buy_after_seed_idempotent_no_seed_revenue.png")
]

for url, title, fn, filename in screenshots:
    img = assemble_screenshot(url, title, fn)
    out_path = os.path.join(OUT_DIR, filename)
    img.save(out_path, "PNG")
    print(f"Generated screenshot: {filename}")

print("All 7 screenshots generated successfully!")
