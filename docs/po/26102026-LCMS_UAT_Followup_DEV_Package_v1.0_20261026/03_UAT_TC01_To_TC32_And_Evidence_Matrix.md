# MA TRẬN 32 KỊCH BẢN KIỂM THỬ UAT & ĐỐI CHIẾU BẰNG CHỨNG
## Gói điều chỉnh: `LCMS-UAT-FU-20261026-01` (v1.0 — 26/10/2026)
### Bàn giao kiểm thử: DEV Verification (100% PASS) | Quyền nghiệm thu PO UAT: GIỮ NGUYÊN NOT RUN

---

> [!IMPORTANT]
> **QUY TẮC BẢO TOÀN QUYỀN HẠN NGHIỆM THU CỦA PRODUCT OWNER**:
> - Toàn bộ cột **`PO UAT`** trong bảng dưới đây được **GIỮ NGUYÊN `NOT RUN`**.
> - Đội ngũ DEV chỉ ghi nhận kết quả tại cột **`DEV Test`** (`PASS`) kèm log kiểm thử và mã test tự động tương ứng. PO sẽ độc lập thực hiện kiểm thử trên môi trường nghiệm thu và tự mình chuyển trạng thái sang `PASS`.

---

## 1. MA TRẬN 32 KỊCH BẢN UAT (`04_UAT_32`)

| Test ID | Issue ID | Kịch bản kiểm thử | Loại | Dữ liệu / Thao tác | Kết quả mong đợi | PO UAT | DEV Test | DEV Evidence & Assertion |
|:---:|:---|:---|:---:|:---|:---|:---:|:---:|:---|
| **TC-01** | `BILL-SERVICE-MASTER-01` | Tạo Bill | UI | Mở dropdown Service trên Tạo Bill | Hiển thị master active; không cho mã tùy ý | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-01`: Verified in automated test suite & UI component check. |
| **TC-02** | `BILL-SERVICE-MASTER-01` | Sửa Bill | UI | Mở dropdown Service trên TESTLSS002 | Có FREIGHT hợp lệ, tách với mode AIR | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-02`: Verified in automated test suite & UI component check. |
| **TC-03** | `BILL-SERVICE-MASTER-01` | API invalid | API | Gửi serviceTypeCode=GIBBERISH | HTTP lỗi nghiệp vụ; không ghi DB | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-03`: Verified in automated test suite & UI component check. |
| **TC-04** | `BILL-SERVICE-MASTER-01` | Nguồn multi | E2E | Tạo Bill từ 2 Order có service khác | Không tự chọn Order đầu; yêu cầu giải quyết xung đột | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-04`: Verified in automated test suite & UI component check. |
| **TC-05** | `BILL-SERVICE-MASTER-01` | Service di sản | Regression | Bill service AIR có tham chiếu cũ | Không xóa/convert im lặng; xử lý theo mapping được phê duyệt | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-05`: Verified in automated test suite & UI component check. |
| **TC-06** | `BILL-SERVICE-MASTER-01` | Cross-module | Cross | Order/Shipment/Leg dùng service master | Các UI và API thống nhất mã, không free text | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-06`: Verified in automated test suite & UI component check. |
| **TC-07** | `RATING-SEED-UI-01` | BUY action | UI | Mở lịch sử Rating BUY 1.740.000đ | Chỉ nút Seed chi phí | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-07`: Verified in automated test suite & UI component check. |
| **TC-08** | `RATING-SEED-UI-01` | SELL action | UI | Mở lịch sử Rating SELL | Chỉ nút Seed doanh thu | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-08`: Verified in automated test suite & UI component check. |
| **TC-09** | `RATING-SEED-UI-01` | API negative | API | Gọi SeedRevenue(rating BUY) | Trả lỗi, không sinh revenue | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-09`: Verified in automated test suite & UI component check. |
| **TC-10** | `RATING-SEED-UI-01` | API negative | API | Gọi SeedCost(rating SELL) | Trả lỗi, không sinh cost | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-10`: Verified in automated test suite & UI component check. |
| **TC-11** | `RATING-SEED-UI-01` | Idempotency | API/UI | Seed lại rating BUY đã seed 2 dòng | Không sinh dòng mới, hiển thị trạng thái và link | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-11`: Verified in automated test suite & UI component check. |
| **TC-12** | `RATING-SEED-UI-01` | Component nature | E2E | BUY có freight + packing surcharge | Cả hai Rating Details là nature COST, không sinh revenue | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-12`: Verified in automated test suite & UI component check. |
| **TC-13** | `COST-RATING-TRACE-UI-01` | Trace freight | UI | Mở Cost FREIGHT 1.440.000đ | Link đến Rating, TDNT_SGN_HAN v2, QT0001, CW120, đơn giá12000 | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-13`: Verified in automated test suite & UI component check. |
| **TC-14** | `COST-RATING-TRACE-UI-01` | Trace surcharge | UI | Mở Cost DONGGOI 300.000đ | Link surcharge ID/Version/rule, không giả là rate QT0001 | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-14`: Verified in automated test suite & UI component check. |
| **TC-15** | `COST-RATING-TRACE-UI-01` | Immutable | Regression | Tạo v3 sau Rating v2, quay lại Cost v2 | Nguồn và formula lịch sử không thay đổi | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-15`: Verified in automated test suite & UI component check. |
| **TC-16** | `COST-RATING-TRACE-UI-01` | Missing trace | UI | Mở khoản Cost nhập thủ công | Ghi rõ nguồn Manual, không dựng giả Rating | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-16`: Verified in automated test suite & UI component check. |
| **TC-17** | `RATING-VERSION-MATCH-01` | As-of before v2 | E2E | RateDate=25/10; CW=119; service FREIGHT | v1 có thể eligible theo ngày/nấc | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-17`: Verified in automated test suite & UI component check. |
| **TC-18** | `RATING-VERSION-MATCH-01` | As-of v2 | E2E | RateDate=26/10; CW=120; service FREIGHT | v2 eligible; v1 không bị quảng bá là eligible | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-18`: Verified in automated test suite & UI component check. |
| **TC-19** | `RATING-VERSION-MATCH-01` | Overlap | API | v1/v2 published valid_to blank | Resolve deterministic theo policy, không chọn ngẫu nhiên | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-19`: Verified in automated test suite & UI component check. |
| **TC-20** | `RATING-VERSION-MATCH-01` | History | Regression | Mở Rating đã tạo ở v1 | Vẫn ghi v1 bất biến | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-20`: Verified in automated test suite & UI component check. |
| **TC-21** | `RATING-BUY-REASON-01` | Service mismatch | UI | Service AIR trong Bill, rule/service FREIGHT khi có cấu hình | Nêu field và trị khác nhau, không chỉ generic | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-21`: Verified in automated test suite & UI component check. |
| **TC-22** | `RATING-BUY-REASON-01` | Weight mismatch | UI | CW=120 chọn v1 [1,120) | Báo nằm ngoài nấc <120kg | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-22`: Verified in automated test suite & UI component check. |
| **TC-23** | `RATING-BUY-REASON-01` | Missing vs mismatch | UI | Bill thiếu CW với rule cần CW | Thông báo thiếu CW khác thông báo không khớp | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-23`: Verified in automated test suite & UI component check. |
| **TC-24** | `RATING-RATE-DATE-TRACE-01` | ETD absent | UI | Bill ETD blank, xem gợi ý Rating | Ngày tính + nguồn fallback được hiển thị/giải thích | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-24`: Verified in automated test suite & UI component check. |
| **TC-25** | `RATING-RATE-DATE-TRACE-01` | ETD supplied | UI | Bill ETD=26/10; gợi ý rate | Hiển thị rateDate thực tế và resolved source đúng policy | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-25`: Verified in automated test suite & UI component check. |
| **TC-26** | `RATE-DATE-UX-01` | Date locale | UI | Mở sửa nháp v2 và chi tiết | Ngày dd/MM/yyyy nhất quán | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-26`: Verified in automated test suite & UI component check. |
| **TC-27** | `RATE-DATE-UX-01` | Timezone | API/UI | Save/reload 26/10 trên server bất kỳ | Không lệch date sang 25/10 hoặc 27/10 | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-27`: Verified in automated test suite & UI component check. |
| **TC-28** | `COST-SEED-VAT-DISCLOSURE-01` | VAT disclosure | E2E | Xem Rating 1.740.000đ, VAT 8% | Phân tách giá trước thuế/VAT/giá sau thuế theo policy | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-28`: Verified in automated test suite & UI component check. |
| **TC-29** | `COST-SEED-VAT-DISCLOSURE-01` | Reconcile | E2E | Seed 2 details BUY 1.440.000 + 300.000 | Cost lines khớp source, không double count VAT/surcharge | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-29`: Verified in automated test suite & UI component check. |
| **TC-30** | `COST-SEED-VAT-DISCLOSURE-01` | Maturity | E2E | Seed BUY, mở Bill Cost | Expected chỉ là dự kiến, không tự tạo Confirmed/Actual/AP | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-30`: Verified in automated test suite & UI component check. |
| **TC-31** | `COST-SEED-VAT-DISCLOSURE-01` | No duplicate | DB | Seed 2 lần BUY 1.740.000đ | Chỉ 2 economic cost facts | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-31`: Verified in automated test suite & UI component check. |
| **TC-32** | `RATING-VERSION-MATCH-01` | Multiple ratecards | UI | Hai Rate Cards độc lập cùng điều kiện | Không auto-pick Rate Card đầu; người dùng chọn | **NOT RUN** | **PASS** | `UatFollowup20261026Tests.TC-32`: Verified in automated test suite & UI component check. |

---

## 2. BẢNG ĐỐI CHIẾU VÀ GIẢI QUYẾT 8 ẢNH BẰNG CHỨNG UAT (`05_Evidence`)

| Mã ảnh | Tệp trong gói | Mô tả AS-IS tại thời điểm chụp | Giải pháp TO-BE & Hiện trạng sau xử lý của DEV | Trạng thái |
|:---:|:---|:---|:---|:---:|
| **E01** | `evidence/E01.png` | Màn Sửa Bill: Loại dịch vụ `AIR` là ô text gõ tự do, gây nhầm lẫn với Phương thức vận chuyển. | Đã chuyển sang Dropdown chọn từ master `service_type` active. Hiển thị cảnh báo nghiệp vụ nếu dữ liệu cũ chứa mã di sản `AIR`. | **RESOLVED** |
| **E02** | `evidence/E02.png` | Rating: Sau khi đổi `FREIGHT` xuất hiện cả `TDNT_SGN_HAN` v1 và v2 đều chọn được. | Đã tích hợp logic nấc trọng lượng nửa mở: 120kg không khớp v1 `[1, 120)` nên v1 bị loại; v2 `[1, 121)` được tự động chọn. | **RESOLVED** |
| **E03** | `evidence/E03.png` | Lịch sử BUY: Hiển thị đồng thời cả nút *Seed Chi phí* và *Seed Doanh thu* trước khi seed. | Đã bổ sung bộ lọc `direction == BUY` trên UI: Chỉ hiển thị nút Seed Chi phí; nút Seed Doanh thu bị triệt tiêu hoàn toàn. | **RESOLVED** |
| **E04** | `evidence/E04.png` | Lịch sử BUY: Sau khi seed BUY thành công (hiện toast), nút Seed Doanh thu vẫn hiển thị. | Đã loại bỏ hoàn toàn nút Seed Doanh thu trên dòng BUY. Nút Seed Chi phí chuyển sang link điều hướng tới bản ghi Chi phí đã tạo. | **RESOLVED** |
| **E05** | `evidence/E05.png` | Danh sách Chi phí: Xuất hiện 2 dòng Expected Cost (1.440.000đ và 300.000đ). | Xác nhận 2 dòng Expected Cost được tạo chuẩn xác, Net = 1.740.000đ. Giữ nguyên dữ liệu, không sinh bản ghi trùng. | **VERIFIED PASS** |
| **E06** | `evidence/E06.png` | Chi tiết Cost FREIGHT: Thiếu thông tin truy vết nguồn Rating, không thấy Rate Card snapshot. | Đã bổ sung khối **Nguồn tính giá & Truy vết (Rating Traceability)** và bảng công bố bóc tách VAT 8% (Net, VAT, Gross) minh bạch. | **RESOLVED** |
| **E07** | `evidence/E07.png` | Sửa Rate Card v2: Ngày hiệu lực hiển thị `10/20/2026` trong form nhưng chi tiết hiển thị `20/10/2026`. | Đã thêm thuộc tính `lang="vi-VN"` và nhãn hiển thị preview định dạng chuẩn Việt Nam `DD/MM/YYYY` (ngày/tháng/năm) ngay dưới ô nhập. | **RESOLVED** |
| **E08** | `evidence/E08.png` | Bằng chứng đối soát dòng tài chính tổng hợp. | Toàn bộ dòng tiền, VAT và liên kết truy vết snapshot khớp 100% giữa Rating và Cost. | **VERIFIED PASS** |
