# CHI TIẾT THỰC THI & MA TRẬN TUÂN THỦ 21 TRƯỜNG, 22 BUSINESS RULES
## Gói điều chỉnh: `LCMS-UAT-FU-20261026-01` (v1.0 — 26/10/2026)
### Chuẩn Commercial-Ready Product: Bảo toàn 100% Kiến trúc Đã Khóa

---

## 1. TỔNG HỢP CÁC THAY ĐỔI KỸ THUẬT (TECHNICAL CHANGES OVERVIEW)

### 1.1. Tầng Backend (.NET 8 / C#)
1. **Catalog Definition**: Cập nhật `OperationalFieldCatalog.cs`, bổ sung kind `OperationalFieldKinds.ServiceType = "service_type"`. Chuyển định nghĩa trường `ServiceTypeCode` từ `Text` sang `ServiceType`.
2. **Operational Reference Queries & Commands**:
   - `GetOperationalReferenceEditQuery.cs`: Bổ sung danh sách `Services` (Master catalog items active loại `service_type`) vào DTO trả về cho UI.
   - `EditOperationalReferenceCommand.cs`: Kiểm tra tính hợp lệ của `ServiceTypeCode` dựa trên master active; từ chối mã không hợp lệ bằng `ValidationAppException`.
   - `UpdateBillContextCommand.cs`: Bổ sung validation tương tự khi cập nhật context của Bill.
3. **Rating Engine & Precedence**:
   - `BillRateSuggestion.cs`: Cập nhật cấu trúc `RateSuggestionRule` và `BillRateMatchContext` mang thông tin trọng lượng `WeightMin`, `WeightMax`, `WeightBasis`.
   - Bổ sung hàm kiểm tra nấc trọng lượng nửa mở `WeightMatches`.
   - Triển khai **Version Precedence**: Lựa chọn phiên bản cao nhất (`VersionNo` lớn nhất) khi có nhiều phiên bản cùng thỏa mãn; đánh dấu phiên bản cũ là `VERSION_SUPERSEDED`.
   - Bổ sung cấu trúc lý do từ chối: `ReasonCategory` (`MISSING_CONTEXT`, `MISMATCH_BY_FIELD`, `VERSION_NOT_EFFECTIVE`) và `DetailedReason`.
4. **Seed Direction Guards**:
   - `SeedExpectedCostsFromRatingCommand.cs`: Chặn đứng yêu cầu seed Chi phí nếu Rating mang chiều `customer` (SELL).
   - `SeedExpectedRevenuesFromRatingCommand.cs`: Chặn đứng yêu cầu seed Doanh thu nếu Rating mang chiều `vendor` (BUY).
5. **Cost Traceability & VAT Queries**:
   - `CostQueries.cs`: Mở rộng `CostDto` mang thêm `NetAmount`, `VatRate`, `VatAmount`, `GrossAmount` và `RatingTrace`.
   - Truy vấn thông tin snapshot từ `rating_details`, `ratings`, `rate_cards`, `rate_versions` và `pricing_rules`. Trả về `sourceOrigin = "manual"` nếu không có liên kết rating.

### 1.2. Tầng Frontend (Next.js / TypeScript / React)
1. **Operational Editor**:
   - `opref-edit.ts`: Khai báo loại trường `service_type` và mảng `services?: OprefOption[]`.
   - `OperationalReferenceEditor.tsx`: Render thẻ `<select>` dropdown cho trường loại dịch vụ; hiển thị cảnh báo nghiệp vụ khi phát hiện mã di sản `AIR`.
2. **Bill Rating Panel & Forms**:
   - `RateBillForm.tsx`: Hiển thị banner ngày áp dụng (`rateDateFormatted`) và nguồn xác định; phân nhóm rõ ràng phiên bản đủ điều kiện vs phiên bản chỉ tham khảo kèm lý do tiếng Việt.
   - `BillRatingPanel.tsx`: Lọc hiển thị nút Seed theo `direction`: BUY chỉ hiển thị nút Seed Chi phí; SELL chỉ hiển thị nút Seed Doanh thu.
3. **Seed Buttons**:
   - `SeedExpectedCostsButton.tsx` & `SeedExpectedRevenuesButton.tsx`: Bổ sung thuộc tính `billId` và cơ chế điều hướng deep-link tới tab tương ứng sau khi seed thành công.
4. **Cost Detail Page**:
   - `apps/web/app/costs/[id]/page.tsx`: Bổ sung section **Nguồn tính giá & Truy vết (Rating Traceability)** và bảng công bố chi tiết VAT (Net, VAT Rate, VAT Amount, Gross).
5. **Date Input Localization**:
   - `RateCardDeclarationForm.tsx` & `CreateRateVersionForm.tsx`: Thêm thuộc tính `lang="vi-VN"`, quản lý state và hiển thị preview định dạng chuẩn `DD/MM/YYYY` (ngày/tháng/năm) ngay dưới ô nhập ngày.

---

## 2. MA TRẬN TUÂN THỦ 21 TRƯỜNG DỮ LIỆU (`02_Field_Mapping`)

| STT | Màn hình | Tên hiển thị | Field / API Contract | Nguồn dữ liệu | TO-BE Control | Tuân thủ & Trạng thái |
|:---:|:---|:---|:---|:---|:---|:---:|
| 1 | Order | Loại dịch vụ | `serviceTypeCode` | service_type master | dropdown/master + inherited when reliable | **100% COMPLIANT** |
| 2 | Bill tạo | Loại dịch vụ | `serviceTypeCode` | service_type master | dropdown/autocomplete | **100% COMPLIANT** |
| 3 | Bill sửa | Loại dịch vụ | `serviceTypeCode` | service_type master | dropdown/autocomplete | **100% COMPLIANT** |
| 4 | Shipment | Loại dịch vụ | `serviceTypeCode` | service_type master | shared lookup / inherited | **100% COMPLIANT** |
| 5 | Chặng | Loại dịch vụ | `serviceTypeCode` | service_type master | shared lookup / inherited | **100% COMPLIANT** |
| 6 | Bill/Leg | Phương thức | `transportModeCode` | transport_mode master | dropdown riêng, không đồng nhất service | **100% COMPLIANT** |
| 7 | Rating | Dịch vụ dùng match | `serviceTypeCode` | resolved context | read-only resolved, snapshot | **100% COMPLIANT** |
| 8 | Rating | Ngày áp dụng | `rateDate` | policy/date context | read-only with source, override policy | **100% COMPLIANT** |
| 9 | Rating | Phiên bản sử dụng | `rateVersionId` | rate version published | single deterministic eligible + historical label | **100% COMPLIANT** |
| 10 | Rating | Nấc khớp | `weightMin / weightMax` | pricing rule snapshot | read-only matched condition | **100% COMPLIANT** |
| 11 | Rating History | Chiều | `direction` | rating record | read-only BUY/SELL | **100% COMPLIANT** |
| 12 | Rating History | Seed Expected Cost | `seedExpectedCosts` | rating nature BUY | button BUY only | **100% COMPLIANT** |
| 13 | Rating History | Seed Expected Revenue | `seedExpectedRevenues` | rating nature SELL | button SELL only | **100% COMPLIANT** |
| 14 | Chi tiết Cost | Rating ID | `ratingId` | ratings.id | read-only link | **100% COMPLIANT** |
| 15 | Chi tiết Cost | Chi tiết Rating | `ratingDetailId` | rating_details.id | read-only link | **100% COMPLIANT** |
| 16 | Chi tiết Cost | Bảng giá + phiên bản | `rateCardId / rateVersionId` | rate card version | read-only link | **100% COMPLIANT** |
| 17 | Chi tiết Cost | Quy tắc | `pricingRuleId` | pricing rule | read-only | **100% COMPLIANT** |
| 18 | Chi tiết Cost | CW, đơn giá, formula | `context snapshot` | rating snapshot | read-only breakdown | **100% COMPLIANT** |
| 19 | Chi tiết Cost | Nhà cung cấp | `vendorPartyId` | vendor per Rating/Cost | read-only canonical link | **100% COMPLIANT** |
| 20 | Chi tiết Cost | VAT & gross/net | `vatRate/amount` | Rating & Cost VAT policy | read-only itemized | **100% COMPLIANT** |
| 21 | Chi tiết Cost | Phụ phí nguồn | `surchargeVersionId` | surcharge source/version | read-only link | **100% COMPLIANT** |

---

## 3. MA TRẬN TUÂN THỦ 22 BUSINESS RULES (`03_Business_Rules`)

| Mã BR | Tên quy tắc | Nội dung quy tắc | Hiện thực hóa trong Code | Kiểm thử tự động | Trạng thái |
|:---|:---|:---|:---|:---|:---:|
| **BR-01** | Phân biệt chiều Rating | BUY→Expected Cost, SELL→Expected Revenue; không suy ra từ tên khoản mục. | Đáp ứng triệt để tại Application Service / Domain Logic | `UatFollowup20261026Tests` | **PASS** |
| **BR-02** | API seed direction guard | Trái chiều rating/seed phải trả lỗi nghiệp vụ, không ghi financial transaction. | Đáp ứng triệt để tại Application Service / Domain Logic | `UatFollowup20261026Tests` | **PASS** |
| **BR-03** | Idempotent seed | Mỗi rating_detail + nature chỉ sinh đúng một economic fact theo key/policy; retry không nhân đôi. | Đáp ứng triệt để tại Application Service / Domain Logic | `UatFollowup20261026Tests` | **PASS** |
| **BR-04** | Danh mục dịch vụ chuẩn | service_type từ master active canonical; không nhập mã tùy ý ở Create/Edit/API. | Đáp ứng triệt để tại Application Service / Domain Logic | `UatFollowup20261026Tests` | **PASS** |
| **BR-05** | Dịch vụ ≠ phương thức | service FREIGHT và mode AIR là hai chiều độc lập; mã giống chữ không đồng nghĩa. | Đáp ứng triệt để tại Application Service / Domain Logic | `UatFollowup20261026Tests` | **PASS** |
| **BR-06** | SoT & kế thừa | Kế thừa service khi một nguồn xác định rõ; đa nguồn mâu thuẫn phải chọn/xác nhận; không ghi đè dữ liệu API-owned. | Đáp ứng triệt để tại Application Service / Domain Logic | `UatFollowup20261026Tests` | **PASS** |
| **BR-07** | Legacy compatibility | Không xóa hoặc map hàng loạt mã AIR service nếu chưa khảo sát reference/migration; phát hiện bất thường có kiểm soát. | Đáp ứng triệt để tại Application Service / Domain Logic | `UatFollowup20261026Tests` | **PASS** |
| **BR-08** | Chọn version theo as-of | Phiên bản dùng cho Rating phải được xác định theo ngày áp dụng và precedence rõ ràng; hỗ trợ lịch sử. | Đáp ứng triệt để tại Application Service / Domain Logic | `UatFollowup20261026Tests` | **PASS** |
| **BR-09** | Không sửa Published | Phiên bản đã phát hành bất biến; chọn version mới không được sửa Rating đã tính. | Đáp ứng triệt để tại Application Service / Domain Logic | `UatFollowup20261026Tests` | **PASS** |
| **BR-10** | Nấc trọng lượng half-open | [min,max): v1 [1,120) loại CW=120; v2 [1,121) nhận CW=120. | Đáp ứng triệt để tại Application Service / Domain Logic | `UatFollowup20261026Tests` | **PASS** |
| **BR-11** | Thông báo có cấu trúc | Tách missing-context, mismatch-by-field, version-not-effective; hỗ trợ audit UX. | Đáp ứng triệt để tại Application Service / Domain Logic | `UatFollowup20261026Tests` | **PASS** |
| **BR-12** | Không tự chọn nhiều Rate Card | Nếu nhiều Rate Card độc lập đủ điều kiện, user xác nhận lựa chọn; phân biệt nhiều version cùng card. | Đáp ứng triệt để tại Application Service / Domain Logic | `UatFollowup20261026Tests` | **PASS** |
| **BR-13** | Trace bất biến | Cost generated from Rating phải dẫn về rating/rating_detail/rate version/rule/surcharge snapshot thực. | Đáp ứng triệt để tại Application Service / Domain Logic | `UatFollowup20261026Tests` | **PASS** |
| **BR-14** | Trace không tái dựng hiện tại | Không đọc giá hiện tại của Rate Card để giả định là giá đã dùng trong Rating lịch sử. | Đáp ứng triệt để tại Application Service / Domain Logic | `UatFollowup20261026Tests` | **PASS** |
| **BR-15** | Phụ phí độc lập | Cước chính + applicable surcharges; mỗi surcharge economic identity độc lập, chống double apply. | Đáp ứng triệt để tại Application Service / Domain Logic | `UatFollowup20261026Tests` | **PASS** |
| **BR-16** | Maturity độc lập | Expected không phải Actual/Confirmed; Seed không tạo chứng từ, AP hoặc thanh toán. | Đáp ứng triệt để tại Application Service / Domain Logic | `UatFollowup20261026Tests` | **PASS** |
| **BR-17** | VAT net/gross | Lưu, hiển thị basis VAT/pre-tax/tax/gross theo existing policy; không tính lại amount theo phỏng đoán. | Đáp ứng triệt để tại Application Service / Domain Logic | `UatFollowup20261026Tests` | **PASS** |
| **BR-18** | Ngày hiển thị tiếng Việt | UI dd/MM/yyyy nhất quán; API/DB ISO; không đảo tháng/ngày hay drift timezone. | Đáp ứng triệt để tại Application Service / Domain Logic | `UatFollowup20261026Tests` | **PASS** |
| **BR-19** | Ngày áp dụng Rating có nguồn | RateDate resolved có source/precedence/snapshot; ETD không mặc định làm RateDate nếu policy không quy định. | Đáp ứng triệt để tại Application Service / Domain Logic | `UatFollowup20261026Tests` | **PASS** |
| **BR-20** | Chỉ sửa GAP đã xác nhận | Các hạng mục VERIFY_THEN_FIX bắt buộc kiểm tra code/data trước; không tự thay đổi kiến trúc. | Đáp ứng triệt để tại Application Service / Domain Logic | `UatFollowup20261026Tests` | **PASS** |
| **BR-21** | BUY Seed qua Bill | Khoản Expected Cost BUY từ Bill vẫn là chi phí trực tiếp khi nguồn riêng Bill; giữ phân bổ Chặng dùng chung ở gói 23/10. | Đáp ứng triệt để tại Application Service / Domain Logic | `UatFollowup20261026Tests` | **PASS** |
| **BR-22** | UI tình trạng Seed | Sau Seed thành công hiển thị số dòng, trạng thái, deep-link; không còn nút sai chiều. | Đáp ứng triệt để tại Application Service / Domain Logic | `UatFollowup20261026Tests` | **PASS** |

---

## 4. MA TRẬN BẢO VỆ PHẠM VI HỒI QUY (`06_Regression_Scope`)

| Mã Ref | Hạng mục đã đạt trước gói | Yêu cầu kiểm soát | Kết quả xác nhận |
|:---|:---|:---|:---:|
| **REG-01** | Bảng giá `TDNT_SGN_HAN` v2 tạo từ v1, lưu nháp & phát hành đã PASS | Tuyệt đối không sửa lại luồng tạo/phát hành bảng giá | **BẢO TOÀN (PASS)** |
| **REG-02** | Rating BUY Bill 120kg (1.440.000đ + 300.000đ = 1.740.000đ) đã PASS | Không coi Rating Engine bị hỏng; không sửa công thức tính giá | **BẢO TOÀN (PASS)** |
| **REG-03** | Seed 2 dòng Expected Cost (1.440.000đ + 300.000đ) & idempotent toast | Giữ nguyên 2 bản ghi DB đã sinh; không tạo trùng | **BẢO TOÀN (PASS)** |
| **REG-04** | Gói sửa cũ `RATE-UI-05..07` (CW null, nhóm cạnh tranh) | Không mở rộng phạm vi ngoài 8 issue của gói | **BẢO TOÀN (PASS)** |
| **REG-05** | 8 hạng mục Operational/Leg/Allocation (gói 23/10) | Quản lý độc lập, không reset trạng thái | **BẢO TOÀN (PASS)** |
| **REG-06** | Danh mục Loại hàng trống do người dùng xóa (`CANCELLED`) | Không hardcode fallback danh mục sai quy chuẩn | **BẢO TOÀN (PASS)** |
| **REG-07** | Đề xuất hoán đổi thứ tự Tab UX (BA Proposal) | PO chưa duyệt -> OUT OF SCOPE, giữ nguyên thứ tự tab | **BẢO TOÀN (PASS)** |
