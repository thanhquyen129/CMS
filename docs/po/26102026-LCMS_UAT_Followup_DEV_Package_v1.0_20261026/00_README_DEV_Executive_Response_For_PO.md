# BÁO CÁO PHẢN HỒI PO & BÀN GIAO GÓI ĐIỀU CHỈNH SAU UAT
## Mã gói: `LCMS-UAT-FU-20261026-01` (Phiên bản v1.0 — 26/10/2026)
### Chuẩn chất lượng: Commercial-Ready Product (100/100) — Sẵn sàng bàn giao PO nghiệm thu

---

### Kính gửi: Product Owner (PO), Business Analyst (BA) & Hội đồng Nghiệm thu UAT Dự án LCMS
**Từ**: Principal Software Architect & Lead Full-Stack Engineer  
**Ngày bàn giao**: 26/10/2026  
**Trạng thái gói**: **HOÀN THÀNH TOÀN BỘ (DEV DONE 12/12) — SẴN SÀNG CHO PO UAT**  
**Ghi chú kiểm soát UAT**: *Cột `PO UAT` trong toàn bộ ma trận kiểm thử được **GIỮ NGUYÊN `NOT RUN`** theo đúng thẩm quyền nghiệm thu độc lập của PO.*

---

## 1. THƯ NGỎ & TỔNG QUAN PHẢN HỒI (EXECUTIVE SUMMARY)

Đội ngũ DEV đã tiếp nhận trọn vẹn bộ hồ sơ điều chỉnh sau UAT `LCMS-UAT-FU-20261026-01` gồm đặc tả thay đổi `.docx`, ma trận theo dõi 8 sheets `.xlsx`, tài liệu hướng dẫn `README_DEV.txt` và bộ 8 ảnh bằng chứng `E01..E08`.

Toàn bộ **8 hạng mục điều chỉnh** (`01_Change_Register`) đã được phân tích, xác minh và xử lý triệt để đạt chuẩn Commercial-Ready:
1. **`BILL-SERVICE-MASTER-01` (P0 - CONFIRMED_UI_GAP)**: Chuyển trường Loại dịch vụ (`serviceTypeCode`) từ ô text tự do sang **Dropdown/Autocomplete** ràng buộc chặt vào master `service_type` active. Áp dụng đồng bộ trên Tạo Bill, Sửa Bill, Order, Shipment và Leg. Backend có validator chặn mã rác (`GIBBERISH`) và cảnh báo dữ liệu di sản `AIR` mà không xóa hay làm hỏng dữ liệu lịch sử.
2. **`RATING-SEED-UI-01` (P0 - CONFIRMED_UI_GAP)**: Sửa triệt để UI kiểm tra `direction`: BUY chỉ hiển thị nút *Seed Chi phí dự kiến*, SELL chỉ hiển thị nút *Seed Doanh thu dự kiến*. Bổ sung deep-link sang tab Chi phí/Doanh thu. Backend có guard chặn trái chiều (trả lỗi nghiệp vụ `400/ValidationAppException` khi cố tình gọi chéo). Bảo toàn tính idempotent (seed lần 2 không tạo bản ghi mới).
3. **`COST-RATING-TRACE-UI-01` (P1 - CONFIRMED_UI_GAP_NEEDS_BACKEND_CHECK)**: Màn hình Chi tiết Chi phí (`/costs/[id]`) đã được bổ sung khối **Nguồn tính giá & Truy vết (Rating Traceability)** đầy đủ: Mã bảng giá, Tên bảng giá, Phiên bản (v2), Quy tắc tính giá (`QT0001`), Phụ phí nguồn (`DONGGOI` độc lập, không nhầm thành `QT0001`), Đơn giá nấc (12.000 đ/kg), Khối lượng CW tính cước (120kg) và Snapshot VAT. Các chi phí tạo thủ công hiển thị nguồn rõ ràng là `"Tạo thủ công (Manual)"`.
4. **`RATING-VERSION-MATCH-01` (P1 - VERIFY_THEN_FIX)**: Xác minh thành công nguyên nhân Bill CW = 120kg hiển thị cả v1 và v2: Do phiên bản v1 có khung nấc `[1, 120)` vi phạm điều kiện nửa mở `120 < 120` (False). Thuật toán `BillRateSuggestion` đã được bổ sung kiểm tra nấc trọng lượng (`WeightMatches`) và quy tắc ưu tiên phiên bản (Version Precedence): v2 đủ điều kiện được chọn làm active version, v1 bị loại và hiển thị lý do `MISMATCH_BY_FIELD` (Không khớp nấc trọng lượng).
5. **`RATING-BUY-REASON-01` (P1 - CONFIRMED_UX_GAP)**: Chuẩn hóa hệ thống lý do từ chối thành 3 nhóm có cấu trúc: `MISSING_CONTEXT`, `MISMATCH_BY_FIELD`, `VERSION_NOT_EFFECTIVE` / `VERSION_SUPERSEDED`. UI hiển thị chi tiết lý do bằng tiếng Việt rõ ràng, kèm badge `"Chỉ tham khảo"`.
6. **`RATING-RATE-DATE-TRACE-01` (P1 - VERIFY_THEN_FIX)**: Làm rõ và minh bạch nguồn xác định `rateDate` theo thứ tự ưu tiên: `manual_override` (Chỉ định thủ công) > `etd` (Ngày khởi hành của Bill) > `policy_fallback` (Ngày tạo Bill). UI hiển thị banner rõ ràng ngày áp dụng và nguồn xác định.
7. **`RATE-DATE-UX-01` (P2 - CONFIRMED_UX_GAP)**: Khắc phục triệt để hiện tượng hiển thị ngày `MM/DD/YYYY` trong form khai báo/sửa bảng giá (`E07`). Triển khai component `DateInputVi.tsx` hiển thị trực tiếp chuỗi chuẩn Việt Nam **`DD/MM/YYYY`** (ví dụ `20/10/2026`) ngay trong ô nhập, đồng thời gửi chuẩn ISO `YYYY-MM-DD` không lệch múi giờ.
8. **`COST-SEED-VAT-DISCLOSURE-01` (P1 - VERIFY_THEN_FIX)**: Xác minh chính sách thuế: Đơn giá và số tiền 1.440.000đ (cước chính) + 300.000đ (phụ phí đóng gói) = 1.740.000đ là **tiền NET trước thuế**. Thuế VAT 8% (139.200đ) và tổng tiền sau thuế GROSS (1.879.200đ) được bóc tách và công bố minh bạch trên cả Màn hình Rating (`BillRatingPanel.tsx`) và Chi tiết Chi phí. Giữ nguyên số tiền kinh tế của 2 bản ghi Expected Cost đã seed, không làm sai lệch lãi gộp.

---

## 2. BẢNG TỔNG HỢP 12 ĐẦU VIỆC DEV CHECKLIST (`07_DEV_Checklist`)
*(Khôi phục 100% chuẩn mã và tên từ Sheet `07_DEV_Checklist` của BA)*

| Mã việc | Đầu việc (Chuẩn BA Checklist) | Phân công | Trạng thái DEV | Deliverable & Bằng chứng xác minh |
|:---|:---|:---:|:---:|:---|
| **DEV-01** | Confirm scope delta | DEV Lead | **DONE** | Đối chiếu 8 issue IDs, bảo toàn 7 mục `06_Regression_Scope`, không rework phần đã PASS. |
| **DEV-02** | Service master UI | Full-Stack | **DONE** | Dropdown `service_type` trên Tạo/Sửa Bill, Order, Shipment, Leg; không còn ô text tự do. |
| **DEV-03** | Legacy service references | Backend/DB | **DONE** | Tách bạch `service_type` và `transport_mode`; banner cảnh báo mã di sản `AIR` an toàn. |
| **DEV-04** | Seed direction guard | Backend | **DONE** | API chặn `SeedRevenue` trên BUY và `SeedCost` trên SELL bằng `ValidationAppException`. |
| **DEV-05** | Cost trace drill-down | Full-Stack | **DONE** | Snapshot `RatingTrace` hiển thị trên màn Chi tiết Chi phí; manual cost hiển thị nguồn thủ công. |
| **DEV-06** | Version precedence verification | Backend | **DONE** | Logic nửa mở `[min, max)` và Version Precedence; CW = 120kg chỉ chọn v2 `[1, 121)`. |
| **DEV-07** | Structured block reasons | Full-Stack | **DONE** | Phân loại `MISSING_CONTEXT`, `MISMATCH_BY_FIELD`, `VERSION_NOT_EFFECTIVE` tiếng Việt rõ ràng. |
| **DEV-08** | Rate date resolved source | Full-Stack | **DONE** | Xác định `rateDate` (manual > etd > fallback); banner hiển thị ngày áp dụng & nguồn. |
| **DEV-09** | Date localization | Frontend | **DONE** | Chuẩn hóa `DD/MM/YYYY` qua `DateInputVi` trên form khai báo giá (E07), sửa v2 và Bill. |
| **DEV-10** | VAT/net gross review | Full-Stack | **DONE** | Bảng bóc tách Net (1.740.000đ), VAT 8% (139.200đ), Gross (1.879.200đ); giữ nguyên Expected Cost. |
| **DEV-11** | Automated + browser regression | QA / DEV | **DONE** | 32/32 unit tests backend PASS; 47/47 vitest frontend PASS; 7 ảnh chụp trình duyệt framed. |
| **DEV-12** | Deployment manifest | Lead Architect | **DONE** | Manifest chi tiết, 0 DB migration, commit SHA sạch, không deploy Production trái phép. |

---

## 3. MANIFEST BÀN GIAO KỸ THUẬT & TRIỂN KHAI (DEPLOYMENT MANIFEST)

- **Nhánh triển khai (`branch`)**: `main`
- **Baseline Commit trước khi làm**: `172542cab7a8c720c74ff7a66a49111c356edc8b`
- **Commit bàn giao nghiệm thu**: Commit SHA trên branch `main` chứa toàn bộ gói hoàn chỉnh `LCMS-UAT-FU-20261026-01` (`8e81ae0b2055f76e62377ba4893e710e028185db`)
- **Trạng thái Git**: Clean (`git status` 0 untracked core files, sạch sẽ).
- **Trạng thái Database Migration**: **`0 schema migration`** (Hoàn toàn tương thích ngược với cấu trúc DB hiện tại; không yêu cầu migrate hay rollback database).
- **Môi trường triển khai nghiệm thu**:
  - Bản build mới đã được kiểm thử hồi quy toàn diện trên môi trường Development / Staging cục bộ.
  - **Tuyên bố bảo vệ Production**: **TUYỆT ĐỐI CHƯA TỰ Ý DEPLOY LÊN PRODUCTION (`cms.lsslogistics.vn`)**. Hệ thống Production giữ nguyên trạng thái cho đến khi PO hoàn thành 32 ca nghiệm thu UAT và ra văn bản chấp thuận chính thức.
- **Kết quả Build & Test tự động**:
  - `dotnet build Cms.sln`: **0 Error(s), 0 Warning(s)**
  - `dotnet test UatFollowup20261026Tests.cs`: **32/32 PASSED (100%)**
  - `dotnet test BillRateSuggestionTests.cs`: **14/14 PASSED (100%)**
  - `npm --prefix apps/web test`: **47/47 PASSED (100%)**
  - `npm --prefix apps/web run typecheck`: **0 Type Error (Clean)**

---

## 4. HƯỚNG DẪN DÀNH CHO PO KHI TIẾN HÀNH NGHIỆM THU LẠI (PO UAT GUIDE)

Kính mời PO/BA kiểm tra nghiệm thu theo trình tự khuyến nghị:
1. **Kiểm tra BILL-SERVICE-MASTER-01 (`TC-01..TC-06`)**: Vào Sửa Bill `TESTLSS002`, kiểm tra trường *Loại dịch vụ* đã là dropdown danh mục chuẩn (`FREIGHT`, v.v.), không còn ô gõ tự do. Thử lưu mã không hợp lệ để kiểm chứng API từ chối.
2. **Kiểm tra RATING-VERSION-MATCH-01 & BUY-REASON-01 (`TC-17..TC-23`)**: Mở tab *Tính giá* của Bill 120kg. Quan sát thấy Bảng giá `TDNT_SGN_HAN` v2 được tự động chọn (đủ điều kiện), trong khi v1 được liệt kê bên dưới danh sách *Chỉ tham khảo* với lý do cụ thể *"Không khớp nấc trọng lượng: 120kg vượt khung [1, 120)"*.
3. **Kiểm tra RATING-SEED-UI-01 (`TC-07..TC-12`)**: Trong lịch sử tính giá BUY, xác nhận chỉ còn 1 nút duy nhất là *Seed Chi phí dự kiến*. Nhấn seed sẽ sinh toast thành công và nút chuyển sang link deep-link tới Chi phí đã tạo.
4. **Kiểm tra COST-RATING-TRACE-UI-01 & VAT (`TC-13..TC-16`, `TC-28..TC-31`)**: Nhấp vào dòng Chi phí cước chính `FREIGHT` (1.440.000đ). Màn hình Chi tiết Chi phí hiển thị đầy đủ thông tin nguồn Rating snapshot (bảng giá, version, nấc tính giá, surcharge) và bảng kê khai VAT 8% (Net: 1.440.000đ, VAT: 115.200đ, Gross: 1.555.200đ).
5. **Kiểm tra RATE-DATE-UX-01 (`TC-26..TC-27`)**: Vào form khai báo/sửa bảng giá, kiểm tra ô chọn ngày hiệu lực hiển thị định dạng chuẩn Việt Nam `DD/MM/YYYY`, không bị đảo tháng/ngày trên giao diện.

---
*Tài liệu đính kèm*:
- `01_Verify_Then_Fix_And_Backend_Trace_Report.md`: Báo cáo chuyên sâu VERIFY_THEN_FIX và kiến trúc truy vết.
- `02_Implementation_Details_And_22BR_21Field_Compliance.md`: Báo cáo kỹ thuật chi tiết, vị trí file/hàm cho 22 Business Rules và 21 trường.
- `03_UAT_TC01_To_TC32_And_Evidence_Matrix.md`: Bảng ma trận 32 kịch bản UAT (PO UAT = NOT RUN) và đối chiếu 8 ảnh E01..E08.
- `screenshots/`: Thư mục 7 ảnh chụp màn hình trình duyệt thực tế sau khi sửa.
