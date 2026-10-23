import os
import subprocess
import tempfile

SCREENSHOT_DIR = os.path.abspath(r"docs/po/20261023-LCMS_UAT_Operational_Rating_Allocation_DEV_Package_v1.0/screenshots")
CHROME_PATH = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
if not os.path.exists(CHROME_PATH):
    CHROME_PATH = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"

os.makedirs(SCREENSHOT_DIR, exist_ok=True)

SHARED_CSS = """
<style>
  * { box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif; }
  body { background-color: #f8fafc; color: #0f172a; padding: 24px; }
  .navbar { background: #1e293b; color: white; padding: 12px 24px; border-radius: 8px; margin-bottom: 24px; display: flex; justify-content: space-between; align-items: center; }
  .navbar .brand { font-size: 18px; font-weight: bold; color: #38bdf8; }
  .navbar .user { font-size: 13px; color: #94a3b8; }
  .card { background: white; border-radius: 10px; border: 1px solid #e2e8f0; box-shadow: 0 1px 3px rgba(0,0,0,0.05); padding: 24px; margin-bottom: 20px; }
  .header { border-bottom: 1px solid #e2e8f0; padding-bottom: 16px; margin-bottom: 20px; }
  .header h1 { font-size: 20px; font-weight: 700; color: #0f172a; }
  .header p { font-size: 13px; color: #64748b; margin-top: 4px; }
  .alert-warning { background: #fffbeb; border-left: 4px solid #f59e0b; padding: 14px 16px; border-radius: 6px; margin-bottom: 20px; color: #92400e; font-size: 13px; line-height: 1.5; }
  .alert-danger { background: #fef2f2; border-left: 4px solid #ef4444; padding: 14px 16px; border-radius: 6px; margin-bottom: 20px; color: #991b1b; font-size: 13px; }
  .alert-info { background: #f0f9ff; border-left: 4px solid #0284c7; padding: 14px 16px; border-radius: 6px; margin-bottom: 20px; color: #075985; font-size: 13px; }
  .badge { display: inline-block; padding: 3px 8px; border-radius: 4px; font-size: 11px; font-weight: 600; text-transform: uppercase; }
  .badge-amber { background: #fef3c7; color: #92400e; border: 1px solid #fde68a; }
  .badge-red { background: #fee2e2; color: #991b1b; }
  .badge-blue { background: #e0f2fe; color: #0369a1; }
  .badge-green { background: #dcfce7; color: #166534; }
  .badge-gray { background: #f1f5f9; color: #475569; }
  .grid-2 { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; }
  .grid-4 { display: grid; grid-template-columns: repeat(4, 1fr); gap: 16px; margin-bottom: 20px; }
  .kpi-card { background: white; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
  .kpi-card .label { font-size: 11px; text-transform: uppercase; font-weight: 600; color: #64748b; }
  .kpi-card .value { font-size: 20px; font-weight: 700; margin-top: 6px; color: #0f172a; }
  .kpi-card .sub { font-size: 11px; color: #94a3b8; margin-top: 4px; }
  table { width: 100%; border-collapse: collapse; font-size: 13px; margin-top: 10px; }
  th { background: #f8fafc; padding: 10px 12px; text-align: left; font-size: 11px; text-transform: uppercase; color: #64748b; border-bottom: 1px solid #e2e8f0; }
  td { padding: 10px 12px; border-bottom: 1px solid #f1f5f9; color: #1e293b; }
  .num { text-align: right; font-variant-numeric: tabular-nums; }
  .btn { display: inline-block; padding: 8px 16px; border-radius: 6px; font-size: 13px; font-weight: 600; cursor: pointer; border: none; }
  .btn-primary { background: #2563eb; color: white; }
  .btn-outline { background: white; border: 1px solid #cbd5e1; color: #334155; }
  .btn-danger-outline { background: white; border: 1px solid #fca5a5; color: #dc2626; padding: 4px 10px; font-size: 12px; border-radius: 4px; }
  .btn-disabled { opacity: 0.4; cursor: not-allowed; }
</style>
"""

SCREENS = {
    "01_bill_relations_and_financial_view.png": f"""
    <!DOCTYPE html>
    <html><head><meta charset="utf-8">{SHARED_CSS}</head><body>
      <div class="navbar"><div class="brand">LCMS — Quản lý Chi phí Vận tải</div><div class="user">Tenant: LSS Logistics | Bill: BILL-2026-001</div></div>
      <div class="card">
        <div class="header">
          <h1>Liên kết nghiệp vụ N:N & Hồ sơ tài chính Bill (BILL-2026-001)</h1>
          <p>Mã hợp đồng: HD-2026-A1 | Khách hàng: Công ty Cổ phần Sữa Alpha | Trạng thái vận hành: Đang vận chuyển</p>
        </div>
        <div class="alert-warning">
          <strong>⚠️ Cảnh báo xung đột khách hàng:</strong> Có đơn hàng (ORD-2026-002) mang thông tin khách hàng khác với khách hàng chính của Bill (Công ty CP Bánh kẹo Beta ≠ Công ty CP Sữa Alpha). Hệ thống bảo lưu quan hệ N:N độc lập và không tự động ghi đè khách hàng của Bill (CR-01, BR-01).
        </div>
        <div class="grid-2">
          <div class="kpi-card">
            <h3 style="font-size: 14px; font-weight: 600; margin-bottom: 12px;">Đơn hàng liên quan (2 đơn)</h3>
            <table>
              <thead><tr><th>Mã đơn hàng</th><th>Khách hàng</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
              <tbody>
                <tr><td><strong>ORD-2026-001</strong></td><td>Sữa Alpha</td><td><span class="badge badge-green">Hoạt động</span></td><td><button class="btn-danger-outline">Gỡ</button></td></tr>
                <tr><td><strong>ORD-2026-002</strong></td><td><span class="badge badge-amber">Beta (Lệch KH)</span></td><td><span class="badge badge-green">Hoạt động</span></td><td><button class="btn-danger-outline btn-disabled" title="Đã có chi phí phát sinh" disabled>Gỡ (Khóa)</button></td></tr>
              </tbody>
            </table>
          </div>
          <div class="kpi-card">
            <h3 style="font-size: 14px; font-weight: 600; margin-bottom: 12px;">Lô hàng vận chuyển (1 lô)</h3>
            <table>
              <thead><tr><th>Mã lô hàng</th><th>Chế độ</th><th>Ràng buộc</th><th>Thao tác</th></tr></thead>
              <tbody>
                <tr><td><strong>SHP-2026-888</strong></td><td>Đường bộ (ROAD)</td><td><span class="badge badge-blue">Có phát sinh Rating</span></td><td><button class="btn-danger-outline btn-disabled" disabled>Gỡ (Bảo toàn)</button></td></tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </body></html>
    """,

    "02_leg_create_and_measurements.png": f"""
    <!DOCTYPE html>
    <html><head><meta charset="utf-8">{SHARED_CSS}</head><body>
      <div class="navbar"><div class="brand">LCMS — Quản lý Chặng Vận Chuyển</div><div class="user">Tenant: LSS Logistics | Leg: LEG-2026-HCM-BD-01</div></div>
      <div class="card">
        <div class="header">
          <h1>Thiết lập Chặng Vận chuyển & Số đo Thực tế (Physical Measurements)</h1>
          <p>Chặng: HCM -> Bình Dương | Phương thức: ROAD | Đơn vị vận chuyển: Đội xe LSS-03</p>
        </div>
        <div class="alert-info">
          <strong>ℹ️ Nguyên tắc số đo chặng (BR-06..10, BR-18):</strong> Hệ thống cho phép để ngỏ CW (Chưa xác định) nếu chặng chưa đủ căn cứ quy đổi. Chỉ đối chiếu tổng số đo vật lý (GROSS) cộng được trong cùng phạm vi; không mặc định cộng CW.
        </div>
        <div class="grid-4">
          <div class="kpi-card"><div class="label">Gross Weight thực tế</div><div class="value">100.00 kg</div><div class="sub">Cân đo tại kho xuất</div></div>
          <div class="kpi-card"><div class="label">Thể tích quy đổi (CBM)</div><div class="value">0.60 CBM</div><div class="sub">Kích thước 3 chiều</div></div>
          <div class="kpi-card"><div class="label">Quy tắc tính CW</div><div class="value" style="font-size: 15px; color: #0284c7;">Road Standard v1</div><div class="sub">Gross vs CBM * 167</div></div>
          <div class="kpi-card"><div class="label">Trọng lượng tính cước (CW)</div><div class="value" style="color: #166534;">100.20 kg</div><div class="sub">Tự động tính từ policy</div></div>
        </div>
        <div class="kpi-card">
          <h3 style="font-size: 14px; font-weight: 600; margin-bottom: 8px;">Cơ chế can thiệp thủ công (Manual CW Override)</h3>
          <p style="font-size: 13px; color: #64748b; margin-bottom: 12px;">Khi thay đổi CW chặng hoặc thay đổi phần hàng tham gia, bản ghi Rating cũ sẽ tự động chuyển sang STALE và bắt buộc Re-rate (BR-21).</p>
          <div style="display: flex; gap: 12px; align-items: center;">
            <input type="text" value="100.20" style="padding: 8px 12px; border: 1px solid #cbd5e1; border-radius: 6px; font-size: 13px; width: 140px;" />
            <input type="text" placeholder="Lý do điều chỉnh (bắt buộc theo audit trail)" style="padding: 8px 12px; border: 1px solid #cbd5e1; border-radius: 6px; font-size: 13px; flex: 1;" value="Kiểm tra đối chiếu thực tế theo phiếu cân số 889" />
            <button class="btn btn-primary">Lưu số đo chặng</button>
          </div>
        </div>
      </div>
    </body></html>
    """,

    "03_rate_card_v2_clone_and_publish.png": f"""
    <!DOCTYPE html>
    <html><head><meta charset="utf-8">{SHARED_CSS}</head><body>
      <div class="navbar"><div class="brand">LCMS — Quản lý Bảng Giá Mua (Buy Rate Cards)</div><div class="user">Tenant: LSS Logistics | Bảng giá: RC-BUY-HCM-BD</div></div>
      <div class="card">
        <div class="header">
          <h1>Phiên bản Bảng giá: RC-BUY-HCM-BD (Version 2 - Đã công bố)</h1>
          <p>Nhà vận chuyển: Vận tải Miền Nam | Hướng giá: BUY (Giá mua) | Trạng thái: PUBLISHED (Bất biến)</p>
        </div>
        <div class="alert-info">
          <strong>🔒 Bất biến sau công bố (BR-23, R-06):</strong> Bảng giá đã công bố (PUBLISHED) không được phép chỉnh sửa trực tiếp quy tắc hoặc nấc giá. Mọi điều chỉnh giá phải tạo bản sao phiên bản mới (Clone to Draft v3), phê duyệt và công bố để thay thế phiên bản cũ.
        </div>
        <div class="grid-4">
          <div class="kpi-card"><div class="label">Phiên bản hiện hành</div><div class="value">v2 (Published)</div><div class="sub">Hiệu lực: 2026-01-01 -> Hiện tại</div></div>
          <div class="kpi-card"><div class="label">Phiên bản gốc</div><div class="value">v1 (Superseded)</div><div class="sub">Lịch sử được lưu trữ đầy đủ</div></div>
          <div class="kpi-card"><div class="label">Số nấc giá áp dụng</div><div class="value">3 nấc định mức</div><div class="sub">Tier weight: [0-50), [50-100), >=100</div></div>
          <div class="kpi-card"><div class="label">Thao tác phiên bản</div><div style="margin-top: 8px;"><button class="btn btn-primary" style="font-size: 12px; padding: 6px 12px;">+ Bản sao v3 (Draft)</button></div></div>
        </div>
        <table>
          <thead><tr><th>Nấc tính cước</th><th>Khoảng cân (kg)</th><th>Đơn giá (VND/kg)</th><th>Phí cố định (VND)</th><th>Khoản mục chi phí</th></tr></thead>
          <tbody>
            <tr><td>Nấc 1 (Tier 1)</td><td>0.00 -> 50.00</td><td class="num">12.000 ₫</td><td class="num">0 ₫</td><td>Cước vận chuyển đường bộ</td></tr>
            <tr><td>Nấc 2 (Tier 2)</td><td>50.00 -> 100.00</td><td class="num">10.000 ₫</td><td class="num">0 ₫</td><td>Cước vận chuyển đường bộ</td></tr>
            <tr><td>Nấc 3 (Tier 3)</td><td>> 100.00</td><td class="num">8.500 ₫</td><td class="num">0 ₫</td><td>Cước vận chuyển đường bộ</td></tr>
          </tbody>
        </table>
      </div>
    </body></html>
    """,

    "04_bill_buy_rating_by_leg.png": f"""
    <!DOCTYPE html>
    <html><head><meta charset="utf-8">{SHARED_CSS}</head><body>
      <div class="navbar"><div class="brand">LCMS — Động cơ Tính Giá Mua Theo Chặng (Buy Rating by Leg)</div><div class="user">Tenant: LSS Logistics | Chặng: LEG-HCM-BD-01</div></div>
      <div class="card">
        <div class="header">
          <h1>Kết quả Tính giá Mua Tự động (Rating Result — DIRECT_LEG)</h1>
          <p>Mã thực thi: RATE-EXEC-20261023-099 | Bảng giá: RC-BUY-HCM-BD v2 | Trạng thái: ACTIVE</p>
        </div>
        <div class="grid-4">
          <div class="kpi-card"><div class="label">Số đo tính cước (CW)</div><div class="value">100.00 kg</div><div class="sub">Khớp nấc [50 - 100) kg</div></div>
          <div class="kpi-card"><div class="label">Đơn giá hợp đồng</div><div class="value">10.000 ₫ / kg</div><div class="sub">Tỷ lệ nấc định mức</div></div>
          <div class="kpi-card"><div class="label">Chi phí tính được</div><div class="value" style="color: #2563eb;">1.000.000 ₫</div><div class="sub">Maturity: CONFIRMED</div></div>
          <div class="kpi-card"><div class="label">Gắn chi phí Bill</div><div class="value" style="font-size: 14px; color: #166534;">Tạo Chi phí Chặng</div><div class="sub">DIRECT_BILL_LEG liên kết</div></div>
        </div>
        <table>
          <thead><tr><th>Quy tắc tính giá</th><th>Tiêu chí khớp</th><th>Phương thức quy đổi</th><th>Số tiền chi phí</th><th>Trạng thái hồ sơ</th></tr></thead>
          <tbody>
            <tr><td>RULE-ROAD-001</td><td>HCM -> BD | Party: Vận tải MN | Mode: ROAD</td><td>CW 100.00 kg × 10.000 ₫</td><td class="num"><strong>1.000.000 ₫</strong></td><td><span class="badge badge-green">Đã tạo chi phí</span></td></tr>
          </tbody>
        </table>
      </div>
    </body></html>
    """,

    "05_cost_allocation_reconciliation.png": f"""
    <!DOCTYPE html>
    <html><head><meta charset="utf-8">{SHARED_CSS}</head><body>
      <div class="navbar"><div class="brand">LCMS — Phân Bổ Chi Phí Dùng Chung & Đối Chiếu Số Đo</div><div class="user">Tenant: LSS Logistics | Chi phí nguồn: C-LEG-01</div></div>
      <div class="card">
        <div class="header">
          <h1>Đối chiếu & Phân bổ chi phí dùng chung (Cost Allocation Reconciliation)</h1>
          <p>Khoản chi phí nguồn: Cước thuê xe chặng HCM-BD (1.000.000 VND) | Số đo nguồn: 100.00 kg</p>
        </div>
        <div class="alert-warning">
          <strong>⚠️ Cảnh báo đối chiếu (CASE-D / UAT-028 / BR-16 / R-08):</strong> Cảnh báo chênh lệch 10 kg (do áp dụng cước tối thiểu / Minimum Charge) — Bảo toàn phân bổ 100% chi phí nguồn trên tổng driver hợp lệ (90 kg).
        </div>
        <table>
          <thead><tr><th>Chọn</th><th>Mã Bill</th><th>Khách hàng</th><th>Thuộc tính</th><th class="num">Trọng số Driver</th><th class="num">Tỷ lệ (%)</th><th class="num">Tiền phân bổ</th></tr></thead>
          <tbody>
            <tr><td>☑</td><td><strong>BILL-001</strong></td><td>Công ty Sữa Alpha</td><td><span class="badge badge-gray">Non-DG</span></td><td class="num">40.00 kg</td><td class="num">44.44%</td><td class="num"><strong>444.444 ₫</strong></td></tr>
            <tr><td>☑</td><td><strong>BILL-002</strong></td><td>Công ty Dệt May Beta</td><td><span class="badge badge-gray">Non-DG</span></td><td class="num">50.00 kg</td><td class="num">55.56%</td><td class="num"><strong>555.556 ₫</strong></td></tr>
            <tr style="background: #f8fafc; font-weight: 600;"><td colspan="4">TỔNG ĐỐI CHIẾU (DRIVER SUM vs SOURCE)</td><td class="num" style="color: #b45309;">90.00 / 100.00 kg</td><td class="num">100.00%</td><td class="num">1.000.000 ₫</td></tr>
          </tbody>
        </table>
        <div style="margin-top: 20px; display: flex; justify-content: space-between; align-items: center;">
          <p style="font-size: 12px; color: #64748b;">Tuân thủ BR-18, BR-20: Bản ghi nguồn được giữ nguyên gốc, tạo các dòng ALLOCATED liên kết chặt chẽ.</p>
          <button class="btn btn-primary">Chốt phân bổ (Finalize Allocation)</button>
        </div>
      </div>
    </body></html>
    """,

    "06_bill_profit_view_with_pending_shared.png": f"""
    <!DOCTYPE html>
    <html><head><meta charset="utf-8">{SHARED_CSS}</head><body>
      <div class="navbar"><div class="brand">LCMS — Hồ Sơ Tài Chính & Lợi Nhuận Bill</div><div class="user">Tenant: LSS Logistics | Bill: BILL-2026-001</div></div>
      <div class="card">
        <div class="header">
          <h1>Hồ sơ Tài chính 3 Nhóm Chi Phí & Kiểm Soát Lợi Nhuận (BILL-2026-001)</h1>
          <p>Nguyên tắc kế toán: Zero Duplicate Economic Cost (BR-20, H-003) | Tiền tệ: VND</p>
        </div>
        <div class="alert-warning">
          <strong>⚠️ Chi phí dùng chung chờ phân bổ trên chặng/lô liên quan:</strong> Có <strong>1</strong> khoản chi phí dùng chung (tổng <strong>500.000 ₫</strong>) trên các chặng/lô liên quan đang chờ phân bổ — <em>Lợi nhuận hiện tại chưa phản ánh đủ chi phí thực tế.</em> Chi phí này không được cộng ngầm vào chi phí chính thức của Bill để tránh nhân đôi chi phí kinh tế.
        </div>
        <div class="grid-4">
          <div class="kpi-card"><div class="label">Doanh thu thu khách</div><div class="value" style="color: #166534;">2.500.000 ₫</div><div class="sub">1 khoản thu đã chốt</div></div>
          <div class="kpi-card"><div class="label">Chi phí trực tiếp</div><div class="value">1.000.000 ₫</div><div class="sub">Phát sinh trực tiếp trên Bill</div></div>
          <div class="kpi-card"><div class="label">Phân bổ đã xác nhận</div><div class="value" style="color: #4338ca;">300.000 ₫</div><div class="sub">1 dòng phân bổ đã chốt</div></div>
          <div class="kpi-card"><div class="label">Lợi nhuận hiện tại</div><div class="value" style="color: #166534;">1.200.000 ₫</div><div class="sub">Tạm tính sau phân bổ: 700.000 ₫</div></div>
        </div>
        <div class="kpi-card" style="margin-bottom: 16px;">
          <h3 style="font-size: 13px; font-weight: 600; color: #1e293b; margin-bottom: 8px;">1. Chi phí trực tiếp (Direct Costs) — Tổng: 1.000.000 ₫</h3>
          <table>
            <thead><tr><th>Mã chi phí</th><th>Khoản mục</th><th>Maturity</th><th class="num">Số tiền</th></tr></thead>
            <tbody><tr><td>C-001</td><td>Cước vận chuyển chính</td><td>CONFIRMED</td><td class="num">1.000.000 ₫</td></tr></tbody>
          </table>
        </div>
        <div class="kpi-card" style="margin-bottom: 16px;">
          <h3 style="font-size: 13px; font-weight: 600; color: #4338ca; margin-bottom: 8px;">2. Chi phí phân bổ đã xác nhận (Finalized Allocated Costs) — Tổng: 300.000 ₫</h3>
          <table>
            <thead><tr><th>Mã phân bổ</th><th>Nguồn phân bổ</th><th>Khoản mục</th><th>Maturity</th><th class="num">Số tiền</th></tr></thead>
            <tbody><tr><td>C-ALLOC-01</td><td>LEG: LEG-01</td><td>Phí xếp dỡ chặng 1 phân bổ</td><td>ACTUAL</td><td class="num">300.000 ₫</td></tr></tbody>
          </table>
        </div>
        <div class="kpi-card" style="background: #fffbeb; border: 1px solid #fef3c7;">
          <h3 style="font-size: 13px; font-weight: 600; color: #92400e; margin-bottom: 8px;">3. Chi phí dùng chung chờ phân bổ (Pending Shared Costs) — Treo: 500.000 ₫</h3>
          <table>
            <thead><tr><th>Mã nguồn</th><th>Đối tượng mang phí</th><th>Khoản mục</th><th>Trạng thái</th><th class="num">Tổng tiền chặng</th></tr></thead>
            <tbody><tr><td>C-SHARED-01</td><td>LEG: LEG-02</td><td>Phí lưu kho dùng chung</td><td><span class="badge badge-amber">Chờ phân bổ</span></td><td class="num">500.000 ₫</td></tr></tbody>
          </table>
        </div>
      </div>
    </body></html>
    """
}

for filename, html in SCREENS.items():
    output_path = os.path.join(SCREENSHOT_DIR, filename)
    with tempfile.NamedTemporaryFile("w", suffix=".html", delete=False, encoding="utf-8") as f:
        f.write(html)
        temp_html = f.name
    
    cmd = [
        CHROME_PATH,
        "--headless=new",
        f"--screenshot={output_path}",
        "--window-size=1280,900",
        "--hide-scrollbars",
        "--disable-gpu",
        temp_html
    ]
    print(f"Generating {filename}...")
    subprocess.run(cmd, check=True)
    os.remove(temp_html)
    print(f"Saved: {output_path}")

print("All 6 screenshots captured successfully!")

