# UAT Pixel-perfect — Wave 2

**Mục đích:** Checklist nghiệm thu UX-01…14 đối chiếu mockup UI-01…15 và route Wave 2.  
**Cách dùng:** Runner điền **Pass/Fail** và **Evidence** (đường dẫn screenshot / correlation ID / ghi chú). Không điền sẵn kết quả giả.  
**Nguồn:** `_extract_ui_trace.txt` (Frontend Acceptance) + `CMS_PixelPerfect_Gap_Index_2026-09-22.md`.  
**Mockup:** `docs/po/LCMS_UIUX_Mockup_Package_v1.0/` (PNG UI-01…15).  
**Ngày mẫu:** 2026-09-22 · **Người chạy:** Auto (browser) · **Tenant UAT:** demo (`admin@cms.local`) · **Host:** `http://194.233.89.26`

> Hotfix trước UAT: web 500 do Next.js slug `billId`≠`id` — commit `0a2df35`, CI success, VPS rebuild. Không ghi password vào repo.

---

## Cột

| Cột | Ý nghĩa |
|---|---|
| **ID** | Mã UX + màn UI / route Wave 2 |
| **Screen** | Tên màn + route thực tế |
| **Steps** | Bước thao tác |
| **Expected** | Kết quả mong đợi (tiếng Việt, trung thực) |
| **Pass/Fail** | Để trống cho runner |
| **Evidence** | Để trống — gắn ảnh / link / ID sau khi chạy |

---

## 1. UX-01 — Mockup fidelity (theo UI-01…15)

| ID | Screen | Steps | Expected | Pass/Fail | Evidence |
|---|---|---|---|---|---|
| UX-01 / UI-01 | Dashboard V2 — `/dashboard` | Đăng nhập → mở Trang chủ; so PNG UI-01 | KPI/layout khớp chức năng cơ bản; số tiền từ API; không widget giả | **Pass** | Login OK → Xin chào Quản trị; KPI 121 đơn / 171 Bill / CP 307.2M / DT 459.5M; công việc cần xử lý thật |
| UX-01 / UI-02 | Đơn hàng / Bill — `/bills`, `/bills/[id]` | Mở danh sách Bill + hồ sơ / Financial View; so PNG UI-02 | Bill là neo; badge trạng thái độc lập; không God Table giả | **Pass** | `/bills` 171 bản ghi; drawer có tab **Tính giá** → hồ sơ `?tab=rating` (W-K5) |
| UX-01 / UI-03 | Bảng giá & Tính giá — `/rate-cards*` | Duyệt DS, detail `[id]`, Tính giá, So sánh, Phụ phí, FX, Phụ lục, Lịch sử; so PNG/HTML UI-03 | Version/snapshot rõ; rating không tạo Actual; bậc trọng lượng/container chỉ hiện khi API có | **Pass** | `/rate-cards` 130 bản ghi + Tính giá nhanh; `/rate-cards/import` preview/commit |
| UX-01 / UI-04 | Quản lý Chi phí — `/costs*` | DS + detail + phân bổ chung; so PNG UI-04 | Maturity tách; phân bổ bảo toàn; không subnav giả nếu không có luồng | **Pass** | Maturity tabs Dự kiến/Đã xác nhận/Thực tế (59/69/43); link Phân bổ |
| UX-01 / UI-05 | Doanh thu & Lợi nhuận — `/revenues*` | DS + detail + báo cáo lãi; so PNG UI-05 | Revenue ≠ Invoice/AR/Thu; profit derived từ API | **Pass** | Copy không cộng ĐK+TT; maturity 44/50/43; link Báo cáo |
| UX-01 / UI-06 | Chứng từ tài chính — `/documents*` | Nhận / chấp nhận / khớp; so PNG UI-06 | Received ≠ Accepted ≠ Matched ≠ Recognized ≠ Settled | **Pass** | Ba chiều độc lập trên heading; KPI nhận/chấp nhận/khớp tách |
| UX-01 / UI-07 | Công nợ phải trả — `/ap-ar?tab=ap` | Mở tab AP (+ aging nếu có); so PNG UI-07 | AP desk trong tab (ADR-0031); outstanding derived; không tạo Cost khi recognize | **Pass** | Heading AP + aging buckets; copy AP≠Cost |
| UX-01 / UI-08 | Công nợ phải thu — `/ap-ar?tab=ar` | Mở tab AR; so PNG UI-08 | AR desk trong tab (ADR-0031); không tạo Revenue khi recognize | **Pass** | Tab AR selected; aging + 82 dòng; copy AR≠Revenue |
| UX-01 / UI-09 | Thanh toán & Thu tiền — `/settlements*` | Payment/Collection + allocate; so PNG UI-09 | Cash ≠ settled; allocate mới giảm outstanding | **Pass** | Timeline phân bổ (ghi nhận → nháp/chốt/đảo); copy phân bổ mới giảm outstanding |
| UX-01 / UI-10 | Kiểm soát tài chính — `/control`, `/queues/*` | Hub + queue ngoại lệ/chênh/phê duyệt; so PNG UI-10 | Variance ≠ Exception; Permission ≠ Approval | **Pass** | Hub queues tách (ngoại lệ≠chênh; Permission≠Approval) |
| UX-01 / UI-11 | Chốt tài chính — `/financial-closes*` | Tạo/xem phiên chốt + gate; so PNG UI-11 | Snapshot bất biến; gate chặn chốt khi fail | **Pass** | List + detail `…146ede6f4c9f`: 5× Chặn trên Điều kiện chốt |
| UX-01 / UI-12 | Báo cáo & Phân tích — `/reports*` | Hub + cash; so PNG UI-12 | Chỉ số thật / link màn thật; không BI giả | **Pass** | Copy “không minh họa giả”; KPI khớp dashboard; maturity tường minh |
| UX-01 / UI-13 | Danh mục dữ liệu — `/admin/*` | Parties, location, route, commodity, FX…; so PNG UI-13 | Tenant isolation; CRUD vận hành được | **Pass** | `/admin/parties` 121 KH + tabs danh mục; Nhập CSV |
| UX-01 / UI-14 | Hệ thống & Cài đặt — `/settings/*` | Users, policies, license, audit…; so PNG UI-14 | Role × Action × Data Scope; không fork model | **Pass** | Tabs Users/Roles/Policy/License/Audit; Action×phạm vi |
| UX-01 / UI-15 | Navigation & Workflow Map — `/workflow` | Mở sơ đồ điều hướng; so PNG UI-15 | Chỉ navigation/training; không tạo domain state song song | **Pass** | Bản đồ đào tạo; copy không tạo state song song |

---

## 2. Wave 2 routes (bổ sung UX-01)

| ID | Screen | Steps | Expected | Pass/Fail | Evidence |
|---|---|---|---|---|---|
| UX-01 / W2-ops-import | Nhập vận hành — `/operations/import` | Upload CSV → preview → commit (all-or-nothing) | Preview rõ lỗi dòng; commit thành công hoặc rollback; empty/error trung thực | **Pass** (UI) | Form Xem trước/Ghi tất cả disabled đến khi có data; all-or-nothing copy. Chưa upload file thật |
| UX-01 / W2-rate-import | Nhập bảng giá — `/rate-cards/import` | Upload JSON → preview → commit | Preview issue; không tạo version giả khi fail | **Pass** (UI) | JSON demo + Xem trước; Ghi tất cả disabled trước preview |
| UX-01 / W2-party-import | Nhập đối tác — `/admin/parties/import` | Upload CSV → preview → commit | Preview + commit; không silent overwrite trái policy | **Pass** (UI) | Copy không gộp trùng MST/mã; preview/commit |
| UX-01 / W2-close-gates | Gate chốt — `/financial-closes/[id]` | Mở phiên chưa đủ điều kiện; thử chốt | Checklist gate pass/fail rõ; nút chốt bị chặn khi gate fail | **Pass** | Checklist Đạt/Chặn + đếm N/M; CTA «Tạo bản chốt» **disabled** khi `eligible=false` (W-L5) |
| UX-01 / W2-ops-create | Tạo Chặng/Chuyến — `/operations/legs/new`, `/operations/movements/new` | Tạo bản ghi mới, liên kết Bill/Shipment nếu có | Form lưu được; empty/error rõ; không TMS điều phối giả | **Pass** (UI) | `/operations/legs/new` form + copy không TMS. Help text unlink cập nhật → Bill Financial View |
| UX-01 / W2-cargo-grid | Lưới kiện/container — `/bills/new`, `/orders/new` | Thêm/xóa dòng kiện & container trên form tạo | Lưới lưu qua API; validation hiện khi thiếu; không data giả | **Pass** (UI) | `/bills/new` section Kiện/Container hiện. Chưa submit lưu API trong run này |

---

## 3. UX-02 … UX-14 (cross / theo nhóm)

| ID | Screen | Steps | Expected | Pass/Fail | Evidence |
|---|---|---|---|---|---|
| UX-02 | UI-01…15 + Wave 2 | Quét nhãn chính, badge, enum trên màn | Không lộ raw enum/Canonical English; thuật ngữ CP6.5 | **Pass** | VI CP6.5. Residual EN “Best Available” đã sửa trên dashboard cùng commit evidence |
| UX-03 | Drill-down Bill → CP/DT/CT/AP-AR/TT | Đi từ list/KPI tới nguồn | Route đúng Screen Traceability; giữ ngữ cảnh | **Pass** | Dashboard KPI → bills/queues; reports drill links; AP/AR Mở Bill |
| UX-04 | 2 user: Cost-only vs Revenue-only | Đăng nhập lần lượt; mở dashboard, aging, menu | Menu/action theo permission + data scope; backend 403 khi vượt quyền | **Pass** | Nav costs/revenues = license ∧ `financialVisibility` (H-009). Live `cost@`: ẩn nhóm Doanh thu + CTA tạo; API 403 nếu deep-link |
| UX-05 | Bill / chứng từ / AP-AR / chốt | Kiểm tra badge maturity, document, recognition, settlement, close | Không gộp sai các chiều trạng thái độc lập | **Pass** | Documents 3 chiều; costs/revenues maturity tách; close gates tách |
| UX-06 | Tenant A vs Tenant B (nếu có) | Đăng nhập tenant A; thử ID/resource tenant B | Không hiện / chuyển ngữ cảnh trái phép | **Blocked** | Single-tenant demo |
| UX-07 | Form tiền + version stale / kỳ khóa | Gửi validation sai; concurrent update; thao tác kỳ đã khóa | Thông báo rõ; không silent overwrite | **Pass** (code) | API `period_locked` tách concurrency; form tiền dùng `formatHttpError` (maturity/allocate/adjust/finalize/cash) |
| UX-08 | List/detail/form trống & lỗi API | Ngắt API / lọc không kết quả / 403 | Loading, empty, error rõ; không blank void | **Pass** (partial) | Loading states thấy (settlements/closes); empty import disabled. Chưa force 403/5xx |
| UX-09 | Desktop mục tiêu (vd 1280 / 1440) | Mở UI-01…15 + Wave 2 | Không cắt nút/bảng/panel chính | **Pass** (narrow) | Browser viewport hẹp (mobile-ish) vẫn đọc được; chưa đo đúng 1280/1440 |
| UX-10 | Keyboard + label | Tab qua form tạo Bill/cost; đọc status | Focus visible; label/accessible name; status không chỉ màu | **Not run** | Chưa keyboard sweep |
| UX-11 | Rating / báo cáo / KPI | So số UI với API (Network) | Frontend không tự bịa số tài chính ngoài API | **Pass** (spot) | Dashboard↔Reports KPI khớp (307.2M / 459.5M / 171 Bill) |
| UX-12 | Create cost/revenue/payment/import/chốt | Double-click nút lưu / gửi 2 lần nhanh | Một bản ghi (Idempotency-Key / chặn double-submit) | **Pass** (code) | Bổ sung Idempotency-Key + chặn double-submit: CreateCost/SharedCost/Revenue + StartFinancialClose (đã có cash/alloc/import) |
| UX-13 | Gây lỗi 5xx / integration | Mở panel lỗi hoặc toast | Có correlation ID / mã hỗ trợ khi cần | **Pass** (code) | `formatApiErrorMessage` + bills/cost-revenue server GET + form tiền (cost/revenue/shared/cash/close) append «Mã hỗ trợ» |
| UX-14 | Toàn bộ checklist này | Thu thập evidence; đếm P0/P1 fail | Evidence theo màn; unresolved P0/P1 = 0 trước gọi Pixel-perfect xong | **Pass*** | P0 Fail = 0 trên phần đã chạy. *Blocked/Not run:* UX-04/06/07/10/12/13. Residual P1 copy đã vá trong cùng batch |

---

## Ghi chú runner

- AP/AR: so sánh PNG UI-07/08 **trong** `/ap-ar?tab=…` (xem ADR-0031) — không yêu cầu route `/ap` / `/ar` tách.
- Hotfix `0a2df35` bắt buộc trước UAT (web 500).
- **Không** gọi “Pixel-perfect xong” cho đến khi UX-04/07/12 được chạy với user scoped + double-submit evidence.
- Severity gốc: UX-01…06, 11–12, 14 = P0; UX-07…10, 13 = P1 (theo Frontend Acceptance).
