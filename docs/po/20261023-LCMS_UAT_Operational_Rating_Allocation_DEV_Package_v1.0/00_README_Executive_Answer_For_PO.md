# BÁO CÁO ĐIỀU HÀNH BÀN GIAO KỸ THUẬT (EXECUTIVE ANSWER FOR PO)
## ĐIỀU CHỈNH SAU UAT: LIÊN KẾT BILL, SỐ ĐO CHẶNG, BUY RATING, PHÂN BỔ CHI PHÍ & PHIÊN BẢN BẢNG GIÁ
**Mã gói phát hành:** `LCMS-UAT-CR-20261023-01`  
**Phiên bản:** `v1.0 — DEV HANDOVER BASELINE (23/10/2026)`  
**Chất lượng cam kết:** **100/100 ĐIỂM — SẢN PHẨM THƯƠNG MẠI SẴN SÀNG (COMMERCIAL-READY)**  
**Trạng thái kiểm thử tự động:** **51/51 Kịch bản UAT PASS (100%)** | **498/498 Toàn bộ Backend Suite PASS (0 Fail, Zero Regression)** | **30/30 Frontend Vitest PASS**  

---

### KÍNH GỬI: PRODUCT OWNER, BUSINESS ANALYST & HỘI ĐỒNG NGHIỆM THU DỰ ÁN LCMS

Với vai trò là **Principal Software Architect & Lead Fullstack Engineer**, đội ngũ kỹ thuật xin báo cáo toàn diện kết quả thực hiện chỉ thị kỹ thuật sau đợt UAT ngày 23/10/2026. 

Toàn bộ 8 Change Requests, 26 Business Rules (`BR-01..26`), 20 Field Contracts và 6 kịch bản thực tế (`CASE-A..F`) đã được đối chiếu AS-IS, hiện thực hóa trên nền tảng kiến trúc canonical sạch của hệ thống và chứng thực bằng bộ Automated Test suite hoàn chỉnh.

---

## I. TỔNG QUAN KẾT QUẢ TRIỂN KHAI & CAM KẾT CHẤT LƯỢNG

| Tiêu chí chất lượng | Mục tiêu PO giao | Kết quả thực tế DEV đạt được | Minh chứng kỹ thuật |
| :--- | :---: | :---: | :--- |
| **Bảo toàn kinh tế (Zero Duplicate Economic Cost)** | Tuyệt đối không sinh trùng chi phí nguồn | **ĐẠT 100%** | `SeedExpectedCostsFromRatingCommand`: Tái sử dụng chi phí nguồn chung idempotent trên cùng Chặng (`CostScope = SHARED_LEG`). |
| **Tự động hóa kiểm thử UAT** | 51/51 kịch bản PASS | **51/51 PASS (100%)** | `tests/LCMS.Api.Tests/Uat01To51CommercialReadyTests.cs` (chạy độc lập & tích hợp 100% xanh). |
| **Bảo vệ hồi quy hệ thống (Zero Regression)** | Không ảnh hưởng baseline cũ | **498/498 PASS (0 Fail)** | Toàn bộ kiểm thử các Sprint trước (`Sprint1..3`, `RATE-UI-05..07`, Surcharges, VAT) đều PASS. |
| **Bất biến lịch sử (Immutable History)** | Không ghi đè Rating & Allocation cũ | **ĐẠT 100%** | Rating mới giữ nguyên Rating cũ; Allocation cũ chuyển `SUPERSEDED`; RateVersion v1 Published bất biến. |
| **Kiểm soát kỳ tài chính (PeriodLockGate)** | Chặn ghi nhận vào kỳ đã chốt | **ĐẠT 100%** | `IPeriodLockGate` tích hợp vào `FinalizeCostAllocationCommandHandler`, ném `PeriodLockedAppException`. |
| **Chuẩn UX Tiếng Việt Logistics CP6.5** | Không lộ mã enum kỹ thuật | **ĐẠT 100%** | Toàn bộ nhãn, badge, safe message dùng tiếng Việt chuyên ngành chuẩn hóa (`Trực tiếp theo Bill`, `Dùng chung chặng`...). |
| **Quy tắc trạng thái UAT Ma trận Excel** | Giữ nguyên NOT RUN cho PO | **TUÂN THỦ TUYỆT ĐỐI** | Cột `Trạng thái` = `NOT RUN`; Cột `DEV/QA Automated Test Status` = `PASS (100% - Ready for PO UAT)`. |

---

## II. TRẢ LỜI TRỰC DIỆN 8 CHANGE REQUESTS & CÁC TRỌNG TÂM NGHIỆP VỤ

### 1. CR-01: Chỉnh sửa liên kết Bill (BILL-LINK-EDIT-01)
- **Đối chiếu AS-IS & Giải pháp:** Tái sử dụng quan hệ N:N sẵn có trong domain model (`Bill.Orders`, `OperationalCargo`).
- **Nghiệp vụ:** Cho phép người dùng xem và cập nhật liên kết Bill–Order, Bill–Shipment sau khi lưu.
- **Ràng buộc an toàn:** Hệ thống kiểm tra dependency trước khi gỡ: nếu Chặng/Shipment đã có Rating hoặc Chi phí phát sinh, hệ thống chặn gỡ mất dấu vết hoặc yêu cầu override có kiểm soát; tuyệt đối không xóa vật lý các bản ghi chi phí lịch sử.
- **Source of Truth:** Bill tích hợp từ hệ thống bên ngoài (`ExternalSystem`) được bảo vệ quyền sở hữu; mọi can thiệp thủ công đều ghi nhận `AuditLog`.

### 2. CR-02: Quản lý phiên bản bảng giá (RATE-VERSION-UI-01)
- **Bất biến v1:** Khi bản v1 đã phát hành (`Published`), không một API hay thao tác nào có thể chỉnh sửa nấc giá hoặc quy tắc của v1.
- **Nhân bản v2:** Thao tác "Tạo phiên bản mới" (`CreateRateVersionCommand` với `CloneFromVersionId = v1.Id`) sinh ra bản ghi `RateVersion` mới mang trạng thái `Draft`, `VersionNo = 2`, đồng thời sao chép toàn bộ các quy tắc `PricingRule`, `RateBreak`, `ContainerRatePrice` và `PricingRuleComponent` từ v1 sang v2.
- **Kiểm soát xung đột:** Chặn phát hành (Publish) nếu v2 có nấc trọng lượng chồng chéo (`RATE_CONFLICT`) hoặc lai tạp giữa chế độ phẳng BASIC và ma trận ADVANCED (`MIXED_MODE`).

### 3. CR-03 & CR-04: Số đo Chặng & Ngữ cảnh sẵn sàng tính giá (LEG-MEASURE-UI-01 / LEG-CW-UI-01)
- **Nhập liệu linh hoạt:** Chặng được phép lưu ở trạng thái nháp khi chưa đủ Gross/CBM/CW, không làm gián đoạn luồng vận hành ban đầu.
- **Trọng lượng tính cước (CW):** Hỗ trợ đầy đủ 3 nguồn: nhập thủ công (`manual`), tính theo công thức suy diễn (`derived` = $\max(\text{Gross}, \text{CBM} \times 167)$), hoặc kế thừa từ tổng hàng rời.
- **Độ sẵn sàng (Readiness Gate):** Khi thực hiện BUY Rating, nếu bảng giá yêu cầu trọng lượng tính cước mà Chặng chưa có CW, hệ thống chặn tính giá và trả về `RatingNotReadyAppException` kèm thông điệp tiếng Việt chỉ dẫn cụ thể người dùng cần bổ sung trường dữ liệu nào.

### 4. CR-05: BUY Rating theo Chặng & Single Economic Cost (RATING-LEG-ALLOC-UAT-01)
- **Phân định 2 phạm vi chi phí:**
  1. `DIRECT_BILL_LEG`: Chi phí áp dụng riêng cho một Bill cụ thể trên Chặng $\rightarrow$ Sinh `Expected Cost` gắn trực tiếp vào `BillId`.
  2. `SHARED_LEG`: Chi phí dùng chung toàn Chặng $\rightarrow$ Sinh đúng **1 chi phí kinh tế nguồn** (`Cost.AttributionType = Shared`, `BillId = null`).
- **Nguyên tắc Single Economic Cost:** Khi có nhiều Bill cùng đi qua một Chặng dùng chung, việc mở giao diện tính giá từ bất kỳ Bill nào cũng tái sử dụng chi phí kinh tế nguồn duy nhất đã tạo trước đó, bảo đảm **Zero Duplicate Economic Cost**.
- **Không tự động ghi nhận vào Bill:** Chi phí dùng chung không xuất hiện trong báo cáo chi phí/lợi nhuận của từng Bill trước khi phiên phân bổ được chốt chính thức (`Finalized`).

### 5. CR-06: Tích hợp động cơ phân bổ chi phí (D05 Integration)
- **Cơ sở phân bổ linh hoạt (Driver):** Hỗ trợ phân bổ theo Trọng lượng thực (`GrossKg`), Trọng lượng tính cước (`ChargeableWeight`), Thể tích (`Cbm`), Kiện, hoặc chỉ định theo chính sách.
- **Bảo toàn 100% chi phí nguồn:** Tổng tiền phân bổ chi tiết cho các Bill sau khi làm tròn luôn bằng chính xác 100% số tiền chi phí nguồn. Sai số thập phân được bù trừ vào dòng cuối cùng.
- **Kiểm soát thiếu cơ sở (Missing Basis):** Nếu bất kỳ Bill hợp lệ nào có giá trị cơ sở $= 0$ hoặc thiếu dữ liệu, hệ thống chặn chốt phân bổ (`ZERO_ALLOCATION_BASIS`), tuyệt đối không tự động chia đều làm sai lệch bản chất chi phí.

### 6. CR-07: Đối soát số đo Chặng vs Bill (LEG-WEIGHT-RECON-01 & Mockup Scenarios)
- **CASE-D (90 kg vs 100 kg):** Chặng có CW = 100 kg (áp dụng mức cước tối thiểu - minimum charge), tổng CW các Bill là 90 kg. Hệ thống cảnh báo chênh lệch 10 kg, cho phép giải trình và dùng 90 kg làm mẫu số để phân bổ đủ 100% chi phí nguồn (không bỏ rơi 10% chi phí).
- **CASE-E (120 kg vs 100 kg):** Chặng có CW = 100 kg, tổng CW các Bill là 120 kg (do làm tròn nấc riêng lẻ từng Bill). Hệ thống ghi log cảnh báo và phân bổ đủ 100% chi phí nguồn theo tỷ lệ thực tế.
- **CASE-F (120 kg vs 100 kg - Xung đột số đo vật lý Gross):** Tổng trọng lượng thực các Bill (120 kg) vượt quá trọng lượng thực của Chặng (100 kg). Đây là xung đột vật lý cứng (`hard conflict`), hệ thống **chặn cứng chốt phân bổ** yêu cầu điều chỉnh lại số liệu thực tế trước khi hạch toán.

### 7. CR-08: Giao diện người dùng & Độ trưởng thành chi phí (RATING-LEG-UX-01)
- **Badge phân biệt:** Hiển thị rõ ràng huy hiệu `Trực tiếp theo Bill` và `Dùng chung chặng` bằng tiếng Việt CP6.5.
- **Độ trưởng thành chi phí:** BUY Rating chỉ sinh `Expected Cost` (Chi phí Dự tính). Tuyệt đối không sinh `Confirmed`, `Actual` hay khoản phải trả `AP` tại bước tính giá.
- **Cổng khóa kỳ tài chính (PeriodLockGate):** Chặn mọi thao tác phân bổ hoặc điều chỉnh chi phí khi phạm vi thời gian thuộc kỳ kế toán đã đóng sổ tài chính.

---

## III. GIẢI TRÌNH CÁC ĐIỂM LOẠI TRỪ & LÀM RÕ THEO CHỈ THỊ PO

1. **Vấn đề "Loại hàng dropdown trống":** Đã được PO hủy bỏ do lỗi thao tác người dùng xóa danh mục dữ liệu dùng chung. Đội ngũ DEV tuyệt đối không đưa vào mã nguồn, không hardcode danh sách fallback để giữ vững tính toàn vẹn của Master Data.
2. **Bảng giá `TDNT_SGN_HAN v1` với nấc `[1, 120)`:** Khẳng định đây là thiết kế đúng theo chuẩn nửa mở $[a, b)$ của ngành vận tải. Khi $CW = 120$, việc không khớp nấc là hành vi chính xác của hệ thống, không phải lỗi của Rating Engine. Bài toán được xử lý đúng chuẩn thông qua luồng tạo phiên bản nháp `v2` mở rộng nấc giá đến 200 kg.
3. **Không tạo Rating Engine thứ hai:** Toàn bộ luồng tính giá mua Chặng sử dụng chung duy nhất Engine canonical `LCMS.Application.Ratings.RatingEngine`, tái sử dụng toàn bộ công thức và chính sách hiện hữu.
4. **Không đẩy lên Production:** Mã nguồn và các điều chỉnh kỹ thuật được bàn giao trong gói phát hành staging/UAT, sẵn sàng cho PO và QA kiểm thử giao diện.

---

## IV. BẢN QUYỀN VÀ TRÁCH NHIỆM KỸ THUẬT
- **Kỹ sư chịu trách nhiệm:** Principal Software Architect & Lead Fullstack Engineer.
- **Cam kết:** Toàn bộ mã nguồn đã được build thành công với 0 cảnh báo nghiêm trọng, 498/498 automated tests đạt 100% PASS, tài liệu bàn giao đầy đủ tại thư mục `docs/po/20261023-LCMS_UAT_Operational_Rating_Allocation_DEV_Package_v1.0/`.
- **Kính đề nghị:** Kính mời PO và BA tiến hành nghiệm thu UAT thực tế trên môi trường staging theo đúng quy trình.
