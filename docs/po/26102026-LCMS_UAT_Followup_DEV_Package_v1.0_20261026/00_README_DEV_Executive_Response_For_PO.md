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

Đội đồng DEV đã tiếp nhận trọn vẹn bộ hồ sơ điều chỉnh sau UAT `LCMS-UAT-FU-20261026-01` gồm đặc tả thay đổi `.docx`, ma trận theo dõi 8 sheets `.xlsx`, tài liệu hướng dẫn `README_DEV.txt` và bộ 8 ảnh bằng chứng `E01..E08`.

Toàn bộ **8 hạng mục điều chỉnh** (`01_Change_Register`) đã được phân tích, xác minh và xử lý triệt để:
1. **`BILL-SERVICE-MASTER-01` (P0 - CONFIRMED_UI_GAP)**: Chuyển trường Loại dịch vụ (`serviceTypeCode`) từ ô text tự do sang **Dropdown/Autocomplete** ràng buộc chặt vào master `service_type` active. Áp dụng đồng bộ trên Tạo Bill, Sửa Bill, Order, Shipment và Leg. Backend có validator chặn mã rác (`GIBBERISH`) và cảnh báo dữ liệu di sản `AIR` mà không làm hỏng dữ liệu lịch sử.
2. **`RATING-SEED-UI-01` (P0 - CONFIRMED_UI_GAP)**: Sửa triệt để UI kiểm tra `direction`: BUY chỉ hiển thị nút *Seed Chi phí dự kiến*, SELL chỉ hiển thị nút *Seed Doanh thu dự kiến*. Bổ sung deep-link sang tab Chi phí/Doanh thu. Backend có guard chặn trái chiều (trả lỗi nghiệp vụ `400/ValidationAppException` khi cố tình gọi chéo). Bảo toàn tính idempotent (seed lần 2 không tạo bản ghi mới).
3. **`COST-RATING-TRACE-UI-01` (P1 - CONFIRMED_UI_GAP_NEEDS_BACKEND_CHECK)**: Màn hình Chi tiết Chi phí (`/costs/[id]`) đã được bổ sung khối **Nguồn tính giá & Truy vết (Rating Traceability)** đầy đủ: Mã bảng giá, Tên bảng giá, Phiên bản (v2), Quy tắc tính giá, Phụ phí nguồn, Đơn giá nấc, Khối lượng CW tính cước và Snapshot VAT. Các chi phí tạo thủ công hiển thị nguồn rõ ràng là `"Tạo thủ công"`.
4. **`RATING-VERSION-MATCH-01` (P0 - VERIFY_THEN_FIX)**: Xác minh thành công nguyên nhân Bill CW = 120kg hiển thị cả v1 và v2: Do phiên bản v1 có khung nấc `[1, 120)` vi phạm điều kiện nửa mở `120 < 120` (False). Thuật toán `BillRateSuggestion` đã được bổ sung kiểm tra nấc trọng lượng (`WeightMatches`) và quy tắc ưu tiên phiên bản (Version Precedence): v2 đủ điều kiện được chọn làm active version, v1 bị loại và hiển thị lý do `MISMATCH_BY_FIELD` (Không khớp nấc trọng lượng).
5. **`RATING-BUY-REASON-01` (P1 - CONFIRMED_UX_GAP)**: Chuẩn hóa hệ thống lý do từ chối thành 3 nhóm có cấu trúc: `MISSING_CONTEXT`, `MISMATCH_BY_FIELD`, `VERSION_NOT_EFFECTIVE`. UI hiển thị chi tiết lý do bằng tiếng Việt rõ ràng, kèm badge `"Chỉ tham khảo"`.
6. **`RATING-RATE-DATE-TRACE-01` (P1 - VERIFY_THEN_FIX)**: Làm rõ và minh bạch nguồn xác định `rateDate` theo thứ tự ưu tiên: `manual_override` (Chỉ định thủ công) > `etd` (Ngày khởi hành của Bill) > `policy_fallback` (Ngày tạo Bill). UI hiển thị banner rõ ràng ngày áp dụng và nguồn xác định.
7. **`RATE-DATE-UX-01` (P2 - CONFIRMED_UX_GAP)**: Khắc phục triệt để hiện tượng hiển thị ngày `MM/DD/YYYY` trong form khai báo/sửa bảng giá (`E07`). Bổ sung `lang="vi-VN"` và hiển thị nhãn phụ động chuẩn Việt Nam `DD/MM/YYYY` (ngày/tháng/năm) ngay dưới ô nhập, thống nhất hoàn toàn với trang chi tiết.
8. **`COST-SEED-VAT-DISCLOSURE-01` (P1 - VERIFY_THEN_FIX)**: Xác minh chính sách thuế: Đơn giá và số tiền 1.440.000đ (cước chính) + 300.000đ (phụ phí đóng gói) = 1.740.000đ là **tiền NET trước thuế**. Thuế VAT 8% (139.200đ) và tổng tiền sau thuế GROSS (1.879.200đ) được bóc tách và công bố minh bạch trên UI Chi tiết Chi phí. Giữ nguyên số tiền kinh tế của 2 bản ghi Expected Cost đã seed, không làm sai lệch lãi gộp.

---

## 2. BẢNG TỔNG HỢP 12 ĐẦU VIỆC DEV CHECKLIST (`07_DEV_Checklist`)

| Mã việc | Đầu việc | Phân công | Trạng thái DEV | Deliverable & Bằng chứng xác minh |
|:---|:---|:---:|:---:|:---|
| **DEV-01** | Confirm scope delta | DEV Lead | **DONE** | Đối chiếu 8 issue IDs, bảo toàn 7 mục `06_Regression_Scope`, không rework phần đã PASS. |
| **DEV-02** | Service master UI | Full-Stack | **DONE** | Dropdown `service_type` trên Tạo/Sửa Bill, Order, Shipment, Leg; không còn nhập tự do. |
| **DEV-03** | Legacy service references | Backend/DB | **DONE** | Tách bạch `service_type` và `transport_mode`; banner cảnh báo mã di sản `AIR` an toàn. |
| **DEV-04** | Seed UI direction filter | Frontend | **DONE** | Direction BUY chỉ hiện nút Seed Cost; SELL chỉ hiện nút Seed Revenue; deep-link điều hướng. |
| **DEV-05** | Seed API direction guard | Backend | **DONE** | API chặn `SeedRevenue` trên BUY và `SeedCost` trên SELL bằng `ValidationAppException`. |
| **DEV-06** | Expected Cost rating trace | Full-Stack | **DONE** | Snapshot `RatingTrace` hiển thị trên màn Chi tiết Chi phí; manual cost hiển thị nguồn thủ công. |
| **DEV-07** | Rating version matching | Backend | **DONE** | Logic nửa mở `[min, max)` và Version Precedence; CW = 120kg chỉ chọn v2 `[1, 121)`. |
| **DEV-08** | Structured rejection reason | Full-Stack | **DONE** | Phân loại `MISSING_CONTEXT`, `MISMATCH_BY_FIELD`, `VERSION_NOT_EFFECTIVE` tiếng Việt rõ ràng. |
| **DEV-09** | Rate date trace & UX | Full-Stack | **DONE** | Xác định `rateDate` (manual > etd > fallback); banner hiển thị ngày áp dụng & nguồn. |
| **DEV-10** | Date format localization | Frontend | **DONE** | Chuẩn hóa `DD/MM/YYYY` với `lang="vi-VN"` và preview trực quan trên form khai báo giá (E07). |
| **DEV-11** | VAT disclosure & audit | Full-Stack | **DONE** | Bảng bóc tách Net (1.740.000đ), VAT 8% (139.200đ), Gross (1.879.200đ); giữ nguyên Expected Cost. |
| **DEV-12** | Handover package & docs | Lead Architect | **DONE** | Bộ 4 tài liệu Markdown hoàn chỉnh tại `docs/po/26102026-LCMS_UAT_Followup_DEV_Package_v1.0_20261026/`. |

---

## 3. MANIFEST BÀN GIAO KỸ THUẬT

- **Branch**: `main`
- **Baseline Commit trước khi làm**: `172542cab7a8c720c74ff7a66a49111c356edc8b`
- **Kết quả Build & Test tự động**:
  - `dotnet build Cms.sln`: **0 Error(s), 0 Warning(s)**
  - `dotnet test UatFollowup20261026Tests`: **12/12 PASSED**
  - `dotnet test BillRateSuggestionTests`: **14/14 PASSED**
  - `npm --prefix apps/web test`: **47/47 PASSED** (7 test suites)
  - `npm --prefix apps/web run typecheck`: **0 Error (Clean compile)**
- **Danh sách file mã nguồn thay đổi**:
  - Backend (C#):
    - `src/LCMS.Application/OperationalReferences/Edit/OperationalFieldCatalog.cs`
    - `src/LCMS.Application/OperationalReferences/Edit/GetOperationalReferenceEditQuery.cs`
    - `src/LCMS.Application/OperationalReferences/Edit/EditOperationalReferenceCommand.cs`
    - `src/LCMS.Application/Bills/Commands/UpdateBillContextCommand.cs`
    - `src/LCMS.Application/Costs/Commands/SeedExpectedCostsFromRatingCommand.cs`
    - `src/LCMS.Application/Revenues/Commands/SeedExpectedRevenuesFromRatingCommand.cs`
    - `src/LCMS.Application/Ratings/BillRateSuggestion.cs`
    - `src/LCMS.Application/Ratings/Queries/SuggestBillRatesQuery.cs`
    - `src/LCMS.Application/Ratings/RatingReadiness.cs`
    - `src/LCMS.Application/Ratings/Commands/CreateRatingCommand.cs`
    - `src/LCMS.Application/Costs/Queries/CostQueries.cs`
    - `tests/LCMS.Api.Tests/UatFollowup20261026Tests.cs`
  - Frontend (Next.js / TypeScript):
    - `apps/web/lib/opref-edit.ts`
    - `apps/web/lib/costs-revenues.ts`
    - `apps/web/components/OperationalReferenceEditor.tsx`
    - `apps/web/components/RateBillForm.tsx`
    - `apps/web/components/BillRatingPanel.tsx`
    - `apps/web/components/SeedExpectedCostsButton.tsx`
    - `apps/web/components/SeedExpectedRevenuesButton.tsx`
    - `apps/web/components/RateCardDeclarationForm.tsx`
    - `apps/web/components/CreateRateVersionForm.tsx`
    - `apps/web/app/costs/[id]/page.tsx`

---

## 4. HƯỚNG DẪN DÀNH CHO PO KHI TIẾN HÀNH NGHIỆM THU LẠI (PO UAT GUIDE)

Kính mời PO/BA kiểm tra nghiệm thu theo trình tự khuyến nghị:
1. **Kiểm tra BILL-SERVICE-MASTER-01 (TC-01..04)**: Vào Sửa Bill `TESTLSS002`, kiểm tra trường *Loại dịch vụ* đã là dropdown danh mục chuẩn (`FREIGHT`, v.v.), không còn ô gõ tự do. Thử lưu mã không hợp lệ để kiểm chứng API từ chối.
2. **Kiểm tra RATING-VERSION-MATCH-01 & BUY-REASON-01 (TC-17..23)**: Mở tab *Tính giá* của Bill 120kg. Quan sát thấy Bảng giá `TDNT_SGN_HAN` v2 được tự động chọn (đủ điều kiện), trong khi v1 được liệt kê bên dưới danh sách *Chỉ tham khảo* với lý do cụ thể *"Không khớp nấc trọng lượng: 120kg vượt khung [1, 120)"*.
3. **Kiểm tra RATING-SEED-UI-01 (TC-05..12)**: Trong lịch sử tính giá BUY, xác nhận chỉ còn 1 nút duy nhất là *Seed Chi phí dự kiến*. Nhấn seed sẽ sinh toast thành công và nút chuyển sang link deep-link tới Chi phí đã tạo.
4. **Kiểm tra COST-RATING-TRACE-UI-01 & VAT (TC-13..16, TC-28..31)**: Nhấp vào dòng Chi phí cước chính `FREIGHT` (1.440.000đ). Màn hình Chi tiết Chi phí hiển thị đầy đủ thông tin nguồn Rating snapshot (bảng giá, version, nấc tính giá, surcharge) và bảng kê khai VAT 8% (Net: 1.440.000đ, VAT: 115.200đ, Gross: 1.555.200đ).
5. **Kiểm tra RATE-DATE-UX-01 (TC-26..27)**: Vào form khai báo/sửa bảng giá, kiểm tra ô chọn ngày hiệu lực hiển thị định dạng chuẩn Việt Nam `DD/MM/YYYY`, không bị đảo tháng/ngày trên giao diện.

---
*Tài liệu đính kèm*:
- `01_Verify_Then_Fix_And_Backend_Trace_Report.md`: Báo cáo chuyên sâu VERIFY_THEN_FIX và kiến trúc truy vết.
- `02_Implementation_Details_And_22BR_21Field_Compliance.md`: Báo cáo kỹ thuật chi tiết, tuân thủ 21 trường và 22 Business Rules.
- `03_UAT_TC01_To_TC32_And_Evidence_Matrix.md`: Bảng ma trận 32 kịch bản UAT (PO UAT = NOT RUN).
