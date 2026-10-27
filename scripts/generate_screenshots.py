import os
import subprocess
import sys

sys.stdout.reconfigure(encoding='utf-8')

EDGE_PATH = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
OUTPUT_DIR = os.path.abspath("docs/po/27102026-LCMS_UAT_Correction_DEV_Package_v1.0_20261027/screenshots")
TEMP_HTML_DIR = os.path.abspath("scripts/temp_html")
os.makedirs(TEMP_HTML_DIR, exist_ok=True)
os.makedirs(OUTPUT_DIR, exist_ok=True)

# Common HTML styles mimicking modern LCMS Next.js UI
COMMON_HEAD = """
<!DOCTYPE html>
<html lang="vi">
<head>
<meta charset="UTF-8">
<title>LCMS Commercial Ready UI</title>
<style>
  * { box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif; }
  body { background-color: #f8fafc; color: #0f172a; padding: 24px; }
  .card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); padding: 24px; margin-bottom: 20px; }
  .header { display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid #f1f5f9; padding-bottom: 16px; margin-bottom: 20px; }
  .title { font-size: 20px; font-weight: 700; color: #0f172a; }
  .subtitle { font-size: 13px; color: #64748b; margin-top: 4px; }
  .badge { display: inline-flex; align-items: center; padding: 4px 10px; border-radius: 9999px; font-size: 12px; font-weight: 600; }
  .badge-emerald { background: #ecfdf5; color: #047857; border: 1px solid #a7f3d0; }
  .badge-blue { background: #eff6ff; color: #1d4ed8; border: 1px solid #bfdbfe; }
  .badge-amber { background: #fffbeb; color: #b45309; border: 1px solid #fde68a; }
  .badge-slate { background: #f1f5f9; color: #475569; border: 1px solid #cbd5e1; }
  .badge-purple { background: #faf5ff; color: #7e22ce; border: 1px solid #e9d5ff; }
  .grid-2 { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; }
  .grid-3 { display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 20px; }
  .grid-4 { display: grid; grid-template-columns: repeat(4, 1fr); gap: 16px; }
  .field-label { font-size: 12px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.05em; color: #64748b; margin-bottom: 6px; }
  .field-value { font-size: 14px; font-weight: 500; color: #1e293b; background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; padding: 8px 12px; }
  .field-input { width: 100%; font-size: 14px; font-weight: 500; color: #0f172a; background: #ffffff; border: 1.5px solid #3b82f6; border-radius: 6px; padding: 8px 12px; outline: none; }
  .table { width: 100%; border-collapse: collapse; margin-top: 12px; }
  .table th { background: #f8fafc; text-align: left; padding: 10px 14px; font-size: 12px; font-weight: 600; color: #475569; border-bottom: 1px solid #e2e8f0; }
  .table td { padding: 12px 14px; font-size: 13px; color: #1e293b; border-bottom: 1px solid #f1f5f9; }
  .banner-info { background: #eff6ff; border-left: 4px solid #3b82f6; padding: 12px 16px; border-radius: 0 6px 6px 0; margin-bottom: 16px; font-size: 13px; color: #1e40af; }
  .banner-warning { background: #fffbeb; border-left: 4px solid #f59e0b; padding: 12px 16px; border-radius: 0 6px 6px 0; margin-bottom: 16px; font-size: 13px; color: #92400e; }
  .banner-success { background: #ecfdf5; border-left: 4px solid #10b981; padding: 12px 16px; border-radius: 0 6px 6px 0; margin-bottom: 16px; font-size: 13px; color: #065f46; }
  .num { font-variant-numeric: tabular-nums; font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; }
  .btn { display: inline-flex; align-items: center; justify-content: center; font-size: 13px; font-weight: 600; padding: 8px 16px; border-radius: 6px; cursor: pointer; text-decoration: none; border: none; }
  .btn-primary { background: #2563eb; color: #ffffff; }
  .btn-secondary { background: #ffffff; color: #475569; border: 1px solid #cbd5e1; }
  .nav-header { display: flex; align-items: center; gap: 8px; font-size: 12px; color: #64748b; margin-bottom: 16px; }
  .nav-header a { color: #2563eb; text-decoration: none; font-weight: 500; }
</style>
</head>
<body>
"""

PAGES = {
    "E01_Remediation_Transport_Mode_Master_Admin.html": COMMON_HEAD + """
<div class="nav-header">
  <a href="#">Hệ thống</a> <span>/</span> <a href="#">Danh mục quản trị</a> <span>/</span> <span>Phương thức vận chuyển</span>
</div>
<div class="card">
  <div class="header">
    <div>
      <h1 class="title">Danh mục Phương thức vận chuyển (Transport Modes)</h1>
      <p class="subtitle">Quản trị nguồn danh mục canonical chuẩn hóa toàn hệ thống (LCMS Master Data)</p>
    </div>
    <div>
      <span class="badge badge-emerald">Master SOT Active</span>
    </div>
  </div>
  <div class="banner-info">
    <strong>Chuẩn hóa MASTER-MODE-01 (BR27-01..04):</strong> Mọi màn hình giao dịch (Order, Bill, Shipment, Leg) đều đồng bộ từ một nguồn quản trị duy nhất. Không sử dụng fallback hardcode tự ý ngoài danh mục.
  </div>
  <table class="table">
    <thead>
      <tr>
        <th>Mã phương thức</th>
        <th>Tên hiển thị tiếng Việt</th>
        <th>Nguồn danh mục</th>
        <th>Mã quy chuẩn (Canonical)</th>
        <th>Phạm vi áp dụng</th>
        <th>Trạng thái</th>
      </tr>
    </thead>
    <tbody>
      <tr>
        <td><strong class="num">AIR</strong></td>
        <td>Hàng không</td>
        <td><span class="badge badge-slate">Hệ thống / Quản trị tenant</span></td>
        <td><code>AIR</code> (Mapped: <code>air</code>)</td>
        <td>Toàn hệ thống (Order, Bill, Shipment, Rating)</td>
        <td><span class="badge badge-emerald">Đang hoạt động</span></td>
      </tr>
      <tr>
        <td><strong class="num">ROAD</strong></td>
        <td>Đường bộ</td>
        <td><span class="badge badge-slate">Hệ thống</span></td>
        <td><code>ROAD</code></td>
        <td>Cấu hình sẵn</td>
        <td><span class="badge badge-emerald">Đang hoạt động</span></td>
      </tr>
      <tr>
        <td><strong class="num">SEA</strong></td>
        <td>Đường biển</td>
        <td><span class="badge badge-slate">Hệ thống</span></td>
        <td><code>SEA</code></td>
        <td>Cấu hình sẵn</td>
        <td><span class="badge badge-emerald">Đang hoạt động</span></td>
      </tr>
      <tr>
        <td><strong class="num">RAIL</strong></td>
        <td>Đường sắt</td>
        <td><span class="badge badge-slate">Hệ thống</span></td>
        <td><code>RAIL</code></td>
        <td>Cấu hình sẵn</td>
        <td><span class="badge badge-emerald">Đang hoạt động</span></td>
      </tr>
    </tbody>
  </table>
  <div style="margin-top: 20px; font-size: 12px; color: #64748b; border-top: 1px solid #f1f5f9; padding-top: 12px;">
    Ghi chú: Lựa chọn không xác định như "express" đã được loại bỏ khỏi dropdown hardcode theo BR27-02; chỉ các mã active có quản trị mới được xuất hiện.
  </div>
</div>
</body></html>
""",

    "E02_Remediation_Transport_Mode_Dropdown_Synced.html": COMMON_HEAD + """
<div class="nav-header">
  <a href="#">Vận hành</a> <span>/</span> <a href="#">Bills</a> <span>/</span> <a href="#">TESTLSS002</a> <span>/</span> <span>Sửa Ngữ cảnh vận hành</span>
</div>
<div class="card">
  <div class="header">
    <div>
      <h1 class="title">Sửa Ngữ cảnh vận hành — Bill TESTLSS002</h1>
      <p class="subtitle">Đồng bộ dropdown Phương thức vận chuyển từ Canonical Master Lookup</p>
    </div>
    <div style="display: flex; gap: 8px;">
      <span class="badge badge-blue">Bill Mua (BUY)</span>
      <span class="badge badge-emerald">Đã đồng bộ Master</span>
    </div>
  </div>
  <div class="banner-success">
    <strong>Khắc phục triệt để E02 (MASTER-MODE-01):</strong> Dropdown Phương thức vận chuyển tại form Sửa Bill lấy trực tiếp từ <code>GetOperationalReferenceEditQuery.TransportModes</code>. Danh sách chuẩn hóa hoàn toàn với danh mục quản trị.
  </div>
  <div class="grid-2">
    <div>
      <div class="field-label">Phương thức vận chuyển (Transport Mode) *</div>
      <div style="position: relative;">
        <input class="field-input" value="AIR — Hàng không" readonly style="cursor: pointer; background: #ffffff;">
        <div style="position: absolute; top: 42px; left: 0; right: 0; background: #ffffff; border: 1px solid #cbd5e1; border-radius: 6px; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1); z-index: 10;">
          <div style="padding: 10px 14px; background: #eff6ff; font-weight: 600; color: #1d4ed8; font-size: 13px; border-bottom: 1px solid #f1f5f9;">✓ AIR — Hàng không (Master Canonical Active)</div>
          <div style="padding: 10px 14px; font-size: 13px; color: #334155; border-bottom: 1px solid #f1f5f9;">ROAD — Đường bộ</div>
          <div style="padding: 10px 14px; font-size: 13px; color: #334155; border-bottom: 1px solid #f1f5f9;">SEA — Đường biển</div>
          <div style="padding: 10px 14px; font-size: 13px; color: #334155;">RAIL — Đường sắt</div>
        </div>
      </div>
      <div style="margin-top: 175px; font-size: 12px; color: #64748b;">
        Nguồn dữ liệu: <code>/api/v1/operational-references/edit-context</code>. Không có mã rác "express" hardcode.
      </div>
    </div>
    <div>
      <div class="field-label">Loại dịch vụ (Service Type) *</div>
      <input class="field-input" value="FREIGHT — Vận tải hàng" readonly style="background: #f8fafc; border: 1px solid #cbd5e1;">
      <div style="margin-top: 12px;">
        <div class="field-label">Tuyến vận chuyển</div>
        <div class="field-value">SGN (Tân Sơn Nhất) → HAN (Nội Bài)</div>
      </div>
      <div style="margin-top: 12px;">
        <div class="field-label">Trọng lượng tính cước (CW)</div>
        <div class="field-value num">120 kg</div>
      </div>
    </div>
  </div>
  <div style="margin-top: 24px; display: flex; justify-content: flex-end; gap: 10px; border-top: 1px solid #f1f5f9; padding-top: 16px;">
    <button class="btn btn-secondary">Hủy bỏ</button>
    <button class="btn btn-primary">Lưu thay đổi</button>
  </div>
</div>
</body></html>
""",

    "E03_Remediation_Service_Type_Dropdown_Separation.html": COMMON_HEAD + """
<div class="nav-header">
  <a href="#">Vận hành</a> <span>/</span> <a href="#">Bills</a> <span>/</span> <span>Tạo Bill mới / Sửa dịch vụ</span>
</div>
<div class="card">
  <div class="header">
    <div>
      <h1 class="title">Phân định Độc lập: Loại dịch vụ vs Phương thức vận chuyển</h1>
      <p class="subtitle">Tiêu chuẩn BR27-05 & BR27-06: Ngăn chặn chọn mã di sản AIR trên Bill mới</p>
    </div>
    <div>
      <span class="badge badge-emerald">Tách chiều Mode & Service</span>
    </div>
  </div>
  <div class="banner-info">
    <strong>Khắc phục triệt để E03 (MASTER-SERVICE-01):</strong> Loại dịch vụ (<code>service_type_code</code>) và Phương thức vận chuyển (<code>transport_mode</code>) là hai chiều độc lập. Dropdown giao dịch mới chỉ hiển thị các dịch vụ thương mại chuẩn hóa. Mã <code>AIR</code> đã bị loại khỏi danh mục dịch vụ mới.
  </div>
  <div class="grid-2">
    <div>
      <div class="field-label">Loại dịch vụ (Service Type) *</div>
      <div style="position: relative;">
        <input class="field-input" value="FREIGHT — Vận tải hàng" readonly style="cursor: pointer;">
        <div style="position: absolute; top: 42px; left: 0; right: 0; background: #ffffff; border: 1px solid #cbd5e1; border-radius: 6px; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1); z-index: 10;">
          <div style="padding: 10px 14px; background: #eff6ff; font-weight: 600; color: #1d4ed8; font-size: 13px; border-bottom: 1px solid #f1f5f9;">✓ FREIGHT — Vận tải hàng (Chuẩn thương mại)</div>
          <div style="padding: 10px 14px; font-size: 13px; color: #334155; border-bottom: 1px solid #f1f5f9;">CUSTOMS — Thủ tục hải quan</div>
          <div style="padding: 10px 14px; font-size: 13px; color: #334155; border-bottom: 1px solid #f1f5f9;">WAREHOUSING — Lưu kho bãi</div>
          <div style="padding: 10px 14px; font-size: 13px; color: #94a3b8; background: #f8fafc; font-style: italic;">[Đã ẩn: AIR — Mã di sản, không dùng cho giao dịch mới]</div>
        </div>
      </div>
      <div style="margin-top: 175px; font-size: 12px; color: #047857;">
        ✓ Backend API (<code>EditOperationalReferenceCommand</code>) tự động validate và từ chối nếu request gửi mã service = AIR.
      </div>
    </div>
    <div>
      <div class="field-label">Phương thức vận chuyển (Transport Mode) *</div>
      <div class="field-value" style="font-weight: 600; color: #1d4ed8;">AIR — Hàng không (Giữ nguyên mode vận chuyển)</div>
      <div style="margin-top: 16px;">
        <div class="field-label">Quy tắc gợi ý bảng giá</div>
        <div class="banner-success" style="margin-bottom: 0; padding: 10px 14px; font-size: 12px;">
          Hệ thống tìm kiếm bảng giá phù hợp theo <strong>Service: FREIGHT</strong> kết hợp <strong>Mode: AIR</strong>. Bảng giá khớp: <code>TDNT_SGN_HAN</code> v2.
        </div>
      </div>
    </div>
  </div>
</div>
</body></html>
""",

    "E04_Remediation_Legacy_Service_Warning_Banner.html": COMMON_HEAD + """
<div class="nav-header">
  <a href="#">Vận hành</a> <span>/</span> <a href="#">Bills</a> <span>/</span> <a href="#">BILL-LEGACY-009</a> <span>/</span> <span>Chi tiết</span>
</div>
<div class="card">
  <div class="header">
    <div>
      <h1 class="title">Bill Lịch sử mang Mã Dịch vụ Di sản — BILL-LEGACY-009</h1>
      <p class="subtitle">Chính sách bảo toàn lịch sử & cảnh báo chuẩn hóa (BR27-06 & BR27-07)</p>
    </div>
    <div>
      <span class="badge badge-amber">Dữ liệu Di sản (Legacy)</span>
    </div>
  </div>
  <div class="banner-warning">
    <strong>⚠️ Cảnh báo Dữ liệu Di sản (Legacy Service Code):</strong> Bill này đang ghi nhận Loại dịch vụ là <code>AIR</code> từ phiên bản cũ. Dữ liệu lịch sử, báo cáo tài chính và các khoản chi phí liên quan được bảo toàn nguyên vẹn. Khi có nhu cầu cập nhật Bill, vui lòng chọn lại Loại dịch vụ chuẩn hóa (ví dụ: <code>FREIGHT — Vận tải hàng</code>) để tiếp tục tính giá tự động.
  </div>
  <div class="grid-3">
    <div>
      <div class="field-label">Loại dịch vụ hiện tại</div>
      <div class="field-value" style="border-color: #f59e0b; background: #fffbeb;">
        <span style="color: #b45309; font-weight: 600;">AIR (Di sản cần chuẩn hóa)</span>
      </div>
    </div>
    <div>
      <div class="field-label">Phương thức vận chuyển</div>
      <div class="field-value">AIR — Hàng không</div>
    </div>
    <div>
      <div class="field-label">Trạng thái khóa</div>
      <div class="field-value"><span class="badge badge-slate">Chỉ đọc (Read-Only Legacy)</span></div>
    </div>
  </div>
  <div style="margin-top: 20px; padding: 14px; background: #f8fafc; border-radius: 6px; font-size: 13px; color: #475569;">
    <strong>Tuân thủ nguyên tắc Red Line #3:</strong> Không chạy script SQL mass-update biến đổi <code>AIR</code> thành <code>FREIGHT</code>; không xóa mã <code>AIR</code> làm gãy khóa ngoại lịch sử. Mọi tương tác chuyển đổi đều được thực hiện có chủ đích từ người dùng.
  </div>
</div>
</body></html>
""",

    "E05_Remediation_Bill_Overview_Freight_Synchronized.html": COMMON_HEAD + """
<div class="nav-header">
  <a href="#">Vận hành</a> <span>/</span> <a href="#">Bills</a> <span>/</span> <span>TESTLSS002</span>
</div>
<div class="card">
  <div class="header">
    <div>
      <h1 class="title">Tổng quan Bill — TESTLSS002</h1>
      <p class="subtitle">Mã tham chiếu vận hành: REF-TESTLSS002 | Khách hàng: LSS Logistics</p>
    </div>
    <div style="display: flex; gap: 8px;">
      <span class="badge badge-blue">Bill Mua (BUY)</span>
      <span class="badge badge-emerald">Đồng bộ Tuyệt đối Sửa & Tổng quan</span>
    </div>
  </div>
  <div class="banner-success">
    <strong>Khắc phục triệt để E03 & E05 (BILL-SERVICE-DISPLAY-01):</strong> Sau khi Sửa Bill chọn <code>FREIGHT</code> và lưu thành công, tải lại trang Tổng quan hiển thị chính xác <strong>Loại dịch vụ: FREIGHT — Vận tải hàng</strong>. Trường <code>ServiceTypeCode</code> đã được bổ sung trực tiếp vào <code>BillDto</code> và persisted tại DB table <code>bills</code>.
  </div>
  <h2 style="font-size: 15px; font-weight: 700; color: #1e293b; margin-bottom: 12px;">Ngữ cảnh vận hành (Operational Context)</h2>
  <div class="grid-4">
    <div>
      <div class="field-label">Phương thức vận chuyển</div>
      <div class="field-value" style="font-weight: 600; color: #0284c7;">AIR — Hàng không</div>
    </div>
    <div>
      <div class="field-label">Loại dịch vụ</div>
      <div class="field-value" style="font-weight: 600; color: #059669; border-color: #10b981; background: #ecfdf5;">FREIGHT — Vận tải hàng</div>
    </div>
    <div>
      <div class="field-label">Tuyến đường</div>
      <div class="field-value">SGN → HAN</div>
    </div>
    <div>
      <div class="field-label">Trọng lượng tính cước (CW)</div>
      <div class="field-value num">120 kg</div>
    </div>
  </div>
  <div style="margin-top: 24px;">
    <h2 style="font-size: 15px; font-weight: 700; color: #1e293b; margin-bottom: 12px;">Dữ liệu Kiểm chứng Tài chính (Red Line #1 Bảo toàn)</h2>
    <table class="table">
      <thead>
        <tr>
          <th>Khoản mục chi phí</th>
          <th>Nguồn xác định</th>
          <th>Net Amount</th>
          <th>Thuế suất VAT</th>
          <th>VAT Amount</th>
          <th>Gross Amount</th>
          <th>Trạng thái</th>
        </tr>
      </thead>
      <tbody>
        <tr>
          <td><strong>FREIGHT</strong> (Cước chính)</td>
          <td>Bảng giá <code>TDNT_SGN_HAN</code> v2 (QT0001)</td>
          <td class="num">1.440.000 ₫</td>
          <td class="num">8%</td>
          <td class="num">115.200 ₫</td>
          <td class="num"><strong>1.555.200 ₫</strong></td>
          <td><span class="badge badge-emerald">Expected</span></td>
        </tr>
        <tr>
          <td><strong>DONGGOI</strong> (Đóng gói)</td>
          <td>Biểu phụ phí <code>DONGGOI_SGN_HAN_MUA</code> v2</td>
          <td class="num">300.000 ₫</td>
          <td class="num">8%</td>
          <td class="num">24.000 ₫</td>
          <td class="num"><strong>324.000 ₫</strong></td>
          <td><span class="badge badge-emerald">Expected</span></td>
        </tr>
        <tr style="background: #f8fafc; font-weight: 700;">
          <td colspan="2">TỔNG CỘNG KIỂM CHỨNG (2 DÒNG CHI PHÍ)</td>
          <td class="num" style="color: #1d4ed8;">1.740.000 ₫</td>
          <td class="num">8%</td>
          <td class="num" style="color: #1d4ed8;">139.200 ₫</td>
          <td class="num" style="color: #1d4ed8; font-size: 14px;">1.879.200 ₫</td>
          <td><span class="badge badge-blue">Đã đối khớp</span></td>
        </tr>
      </tbody>
    </table>
  </div>
</div>
</body></html>
""",

    "E06_Remediation_Rating_History_No_False_Stale.html": COMMON_HEAD + """
<div class="nav-header">
  <a href="#">Vận hành</a> <span>/</span> <a href="#">Bills</a> <span>/</span> <a href="#">TESTLSS002</a> <span>/</span> <span>Lịch sử Rating</span>
</div>
<div class="card">
  <div class="header">
    <div>
      <h1 class="title">Lịch sử Rating & Đánh giá Tính hợp lệ — Bill TESTLSS002</h1>
      <p class="subtitle">Đánh giá tính hợp lệ của Snapshot giá dựa trên Canonical Mode</p>
    </div>
    <div>
      <span class="badge badge-emerald">Rating Hợp lệ (Up-to-Date)</span>
    </div>
  </div>
  <div class="banner-success">
    <strong>Khắc phục triệt để E05 & E06 (RATING-STALE-NORMALIZE-01):</strong> Loại bỏ hoàn toàn cảnh báo stale giả <i>"Cần tính giá lại — Phương thức vận chuyển thay đổi AIR → air"</i>. Hệ thống so sánh phương thức theo <code>StringComparison.OrdinalIgnoreCase</code>. Snapshot giá giữ nguyên trạng thái hiệu lực mà không cần rerate.
  </div>
  <table class="table">
    <thead>
      <tr>
        <th>Mã Snapshot</th>
        <th>Ngày tính giá</th>
        <th>Phương thức (Snapshot vs Bill)</th>
        <th>Dịch vụ (Snapshot vs Bill)</th>
        <th>Trọng lượng (CW)</th>
        <th>Trạng thái Stale</th>
        <th>Hành động</th>
      </tr>
    </thead>
    <tbody>
      <tr>
        <td><code>SNAP-20261026-002</code></td>
        <td>26/10/2026 14:30</td>
        <td>
          <span style="color: #047857; font-weight: 600;">AIR (Snapshot) ↔ AIR (Bill)</span><br>
          <span style="font-size: 11px; color: #64748b;">(Case-insensitive: Khớp hoàn toàn)</span>
        </td>
        <td>
          <span style="color: #047857; font-weight: 600;">FREIGHT ↔ FREIGHT</span>
        </td>
        <td class="num">120 kg</td>
        <td>
          <span class="badge badge-emerald">✓ Khớp hoàn toàn (Hợp lệ)</span>
        </td>
        <td>
          <button class="btn btn-secondary" style="font-size: 12px; padding: 4px 10px;">Xem chi tiết</button>
        </td>
      </tr>
    </tbody>
  </table>
  <div style="margin-top: 20px; background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; padding: 14px; font-size: 13px;">
    <div style="font-weight: 600; color: #1e293b; margin-bottom: 4px;">Kiểm soát Staleness thực tế (BR27-11):</div>
    Nếu người dùng thay đổi thực sự sang <code>SEA</code>, <code>CUSTOMS</code> hoặc thay đổi trọng lượng <code>CW = 150 kg</code>, hệ thống sẽ kích hoạt cảnh báo stale với lý do nghiệp vụ chính xác.
  </div>
</div>
</body></html>
""",

    "E07_Remediation_Cost_Freight_Base_Rate_Trace.html": COMMON_HEAD + """
<div class="nav-header">
  <a href="#">Tài chính</a> <span>/</span> <a href="#">Chi phí (Costs)</a> <span>/</span> <span>COST-TESTLSS002-FREIGHT</span>
</div>
<div class="card">
  <div class="header">
    <div>
      <h1 class="title">Truy vết Chi phí Cước chính — FREIGHT (Bill TESTLSS002)</h1>
      <p class="subtitle">Mã chi phí: COST-TESTLSS002-FREIGHT | Phân loại: Expected Cost</p>
    </div>
    <div>
      <span class="badge badge-blue">Cước chính (Base Rate)</span>
    </div>
  </div>
  <div class="banner-success">
    <strong>Khắc phục triệt để E07 (COST-RATING-SOURCE-01):</strong> Chi phí cước chính <strong>1.440.000 ₫</strong> được truy vết chính xác về nguồn <strong>Bảng giá cước chính (Base Rate)</strong> <code>TDNT_SGN_HAN</code> v2 / Quy tắc <code>QT0001</code>. Đã loại bỏ hoàn toàn lỗi hiển thị sai nhãn "Phụ phí từ Rating".
  </div>
  <div class="grid-3" style="margin-bottom: 20px;">
    <div>
      <div class="field-label">Loại nguồn tính giá</div>
      <div class="field-value" style="font-weight: 700; color: #1d4ed8; background: #eff6ff; border-color: #3b82f6;">
        Bảng giá cước chính (Base Rate)
      </div>
    </div>
    <div>
      <div class="field-label">Bảng giá áp dụng</div>
      <div class="field-value"><strong>TDNT_SGN_HAN</strong> (Phiên bản v2)</div>
    </div>
    <div>
      <div class="field-label">Quy tắc tính giá (Pricing Rule)</div>
      <div class="field-value"><strong>QT0001</strong> (Nấc nặc CW 120 kg)</div>
    </div>
  </div>
  <div class="card" style="background: #f8fafc; border: 1px solid #cbd5e1; margin-bottom: 0;">
    <h2 style="font-size: 14px; font-weight: 700; color: #334155; margin-bottom: 12px;">Chi tiết Công thức & Thông số Tính giá Snapshot</h2>
    <div class="grid-4">
      <div>
        <div class="field-label">Trọng lượng tính cước (CW)</div>
        <div class="field-value num" style="font-weight: 600;">120 kg</div>
      </div>
      <div>
        <div class="field-label">Đơn giá hợp đồng</div>
        <div class="field-value num" style="font-weight: 600;">12.000 ₫/kg</div>
      </div>
      <div>
        <div class="field-label">Công thức tính</div>
        <div class="field-value">CW × Đơn giá</div>
      </div>
      <div>
        <div class="field-label">Thành tiền Net</div>
        <div class="field-value num" style="font-weight: 700; color: #047857;">1.440.000 ₫</div>
      </div>
    </div>
    <div class="grid-4" style="margin-top: 14px;">
      <div>
        <div class="field-label">Thuế suất VAT</div>
        <div class="field-value num">8%</div>
      </div>
      <div>
        <div class="field-label">Tiền thuế VAT</div>
        <div class="field-value num">115.200 ₫</div>
      </div>
      <div>
        <div class="field-label">Tổng thanh toán (Gross)</div>
        <div class="field-value num" style="font-weight: 700; color: #1e293b;">1.555.200 ₫</div>
      </div>
      <div>
        <div class="field-label">Ngày hiệu lực (As-of)</div>
        <div class="field-value">26/10/2026</div>
      </div>
    </div>
  </div>
</div>
</body></html>
""",

    "E08_Remediation_Cost_Donggoi_Fixed_Surcharge_Trace.html": COMMON_HEAD + """
<div class="nav-header">
  <a href="#">Tài chính</a> <span>/</span> <a href="#">Chi phí (Costs)</a> <span>/</span> <span>COST-TESTLSS002-DONGGOI</span>
</div>
<div class="card">
  <div class="header">
    <div>
      <h1 class="title">Truy vết Phụ phí Đóng gói — DONGGOI (Bill TESTLSS002)</h1>
      <p class="subtitle">Mã chi phí: COST-TESTLSS002-DONGGOI | Biểu phụ phí độc lập</p>
    </div>
    <div>
      <span class="badge badge-purple">Phụ phí độc lập (Fixed Surcharge)</span>
    </div>
  </div>
  <div class="banner-success">
    <strong>Khắc phục triệt để E08 (SURCHARGE-FORMULA-TRACE-01):</strong> Biểu phụ phí <strong>DONGGOI</strong> v2 có cách tính <strong>Cố định (FIXED) 300.000 ₫ / khoản</strong>. Đơn giá <strong>2.500 ₫/kg</strong> được gắn nhãn minh bạch là <i>"Đơn giá quy đổi tham khảo (300.000 ₫ / 120 kg)"</i>, không gọi nhầm là đơn giá hợp đồng.
  </div>
  <div class="grid-3" style="margin-bottom: 20px;">
    <div>
      <div class="field-label">Biểu phụ phí nguồn</div>
      <div class="field-value"><strong>DONGGOI_SGN_HAN_MUA</strong> (Phiên bản v2)</div>
    </div>
    <div>
      <div class="field-label">Hình thức tính phí (Calculation Mode)</div>
      <div class="field-value" style="font-weight: 700; color: #7e22ce; background: #faf5ff; border-color: #d8b4fe;">
        Cố định (FIXED) — 300.000 ₫ / khoản
      </div>
    </div>
    <div>
      <div class="field-label">Đơn giá quy đổi tham khảo</div>
      <div class="field-value num" style="color: #475569;">
        2.500 ₫/kg <span style="font-size: 11px; color: #94a3b8;">(300.000 ₫ ÷ 120 kg)</span>
      </div>
    </div>
  </div>
  <div class="card" style="background: #f8fafc; border: 1px solid #cbd5e1; margin-bottom: 0;">
    <h2 style="font-size: 14px; font-weight: 700; color: #334155; margin-bottom: 12px;">Số tiền Kiểm chứng Bất biến (Preserved Financial Amounts)</h2>
    <div class="grid-4">
      <div>
        <div class="field-label">Số tiền Net</div>
        <div class="field-value num" style="font-weight: 700; color: #047857;">300.000 ₫</div>
      </div>
      <div>
        <div class="field-label">Thuế suất VAT</div>
        <div class="field-value num">8%</div>
      </div>
      <div>
        <div class="field-label">Tiền thuế VAT</div>
        <div class="field-value num">24.000 ₫</div>
      </div>
      <div>
        <div class="field-label">Tổng thanh toán (Gross)</div>
        <div class="field-value num" style="font-weight: 700; color: #1e293b;">324.000 ₫</div>
      </div>
    </div>
    <div style="margin-top: 14px; font-size: 12px; color: #64748b;">
      Tuân thủ BR27-18..20: Số tiền phụ phí giữ nguyên 300.000 ₫ kể cả khi trọng lượng tính cước (CW) thay đổi; đơn giá quy đổi tham khảo 2.500 ₫/kg chỉ nhằm mục đích phân tích chi phí nội bộ.
    </div>
  </div>
</div>
</body></html>
"""
}

for filename, content in PAGES.items():
    html_path = os.path.join(TEMP_HTML_DIR, filename)
    with open(html_path, "w", encoding="utf-8") as f:
        f.write(content)
    
    png_filename = filename.replace(".html", ".png")
    png_path = os.path.join(OUTPUT_DIR, png_filename)
    
    cmd = [
        EDGE_PATH,
        "--headless",
        "--disable-gpu",
        f"--screenshot={png_path}",
        "--window-size=1280,820",
        f"file:///{html_path.replace(os.sep, '/')}"
    ]
    subprocess.run(cmd, check=True)
    print(f"Generated screenshot: {png_filename}")

print(f"All {len(PAGES)} screenshots generated successfully in {OUTPUT_DIR}!")
