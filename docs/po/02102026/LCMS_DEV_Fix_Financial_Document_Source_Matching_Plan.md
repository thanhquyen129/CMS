# LCMS — Kế Hoạch Triển Khai: Financial Document Source & Matching (v1.0)

> **Tài liệu tham chiếu gốc từ PO:**  
> - `LCMS_DEV_Fix_Financial_Document_Source_Matching_v1.0.docx`  
> - `LCMS_DEV_Fix_Financial_Document_Source_Matching_Matrix_v1.0.xlsx`  
> **Ngày lập:** 03/10/2026  
> **Trạng thái:** PO CONFIRMED / ARCHITECTURE AGREED  
> **Mục đích:** Tài liệu kỹ thuật chi tiết dùng để theo dõi tiến độ, đồng bộ nghiệp vụ và nhập vào Notion quản lý dự án.

---

## 1. Bối cảnh & Mục tiêu nghiệp vụ

### 1.1 Vấn đề hiện tại
* Màn hình **"Nhận chứng từ tài chính"** hiện tại cho phép chọn Bill/đối tác nhưng trường **Tổng tiền** (`totalAmount`) vẫn là một ô nhập độc lập.
* Người dùng phải nhập lặp lại số tiền đã tồn tại ở Cost/Revenue, tăng nguy cơ nhập sai, vi phạm nguyên tắc **Single Economic Fact**, làm lu mờ vai trò của cơ chế Matching.
* **Không được sửa đơn giản bằng cách tự động lấy toàn bộ số tiền của Bill vào chứng từ**, vì một chứng từ có thể bao phủ một phần dòng (partial), nhiều dòng, trải dài nhiều Bill; đồng thời chứng từ bên ngoài có thể có số tiền thực tế khác với Cost/Revenue trong hệ thống để phục vụ đối soát chênh lệch.

### 1.2 Mục tiêu nghiệp vụ
1. **Phân biệt rõ Economic Layer với Document Layer:** Cost/Revenue là tầng kinh tế cốt lõi (Economic Fact); Invoice/Debit Note/Credit Note là tầng chứng từ (Document Layer). Tạo/nhận chứng từ **không được sinh thêm Cost/Revenue**.
2. **Bill/Shipment là phạm vi lọc/tham chiếu:** Cost/Revenue lines mới là đối tượng trực tiếp để liên kết và đối soát (match).
3. **Hai chế độ bắt buộc:**
   * **Mode A (EXTERNAL_RECEIVED — Nhận chứng từ bên ngoài):** Nhập Tổng tiền theo hóa đơn thực tế của đối tác, đối soát với các dòng Cost/Revenue, ghi nhận phần khớp, phần chưa khớp (`Unmatched`) và chênh lệch (`Variance`). Không tự ý ghi đè Cost/Revenue.
   * **Mode B (LCMS_GENERATED — Tạo chứng từ từ dữ liệu LCMS):** Tổng tiền được tự động tính (derive) từ các dòng Cost/Revenue được chọn trong hệ thống. Ô Tổng tiền mặc định là **Readonly**.
4. **Bảo toàn kiến trúc Reporting Currency & FX (ADR-0040):** Mọi chứng từ và liên kết khớp phải giữ nguyên tệ gốc, tỷ giá snapshot và số tiền theo đồng tiền báo cáo (`BaseAmount`). Không được ép tỷ giá bằng 0 hoặc 1.

---

## 2. Bảng so sánh Hiện trạng (As-Is) vs Yêu cầu mới (To-Be)

| Hạng mục | Hiện trạng hệ thống (As-Is) | Yêu cầu mới từ PO (To-Be) | Trạng thái Gap & Xử lý |
| :--- | :--- | :--- | :--- |
| **Chế độ chứng từ (Modes)** | Chỉ có 1 form nhận chứng từ chung, không phân biệt nguồn gốc. | 2 chế độ rõ ràng: `EXTERNAL_RECEIVED` (Mode A) và `LCMS_GENERATED` (Mode B). | **Cần thêm field `Mode`** trên DB, DTO và UI switch. |
| **Nguồn số tiền Tổng** | Người dùng gõ tay ô `totalAmount` tự do, tách rời Cost/Revenue. | • **Mode B:** Tự động tính từ các dòng được chọn (Readonly).<br>• **Mode A:** Nhập theo chứng từ gốc, tính Variance so với các dòng khớp. | **Cần cơ chế tự động tính tổng** và khóa input ở Mode B. |
| **Chọn dòng tài chính (Source Lines)** | Chỉ có dropdown chọn Bill/Đối tác, không hiển thị dòng chi phí/doanh thu nào. | Bảng **"Khoản tài chính liên quan"** liệt kê các dòng Cost/Revenue có thể gắn, kèm số dư còn lại (`Remaining Amount`). | **Cần xây dựng mới UI Table** và API query `eligible-source-lines`. |
| **Lọc theo Chiều (AP/AR)** | Có dropdown chọn chiều, nhưng chỉ để validate vai trò đối tác. | Lọc chặt chẽ: `AP-side` chỉ hiển thị Cost của Vendor; `AR-side` chỉ hiển thị Revenue của Customer. | **Cần bổ sung logic filter** theo chiều và đối tác. |
| **Gán một phần & Kiểm soát Over-match** | Chưa có query theo dõi số dư còn lại trên Cost/Revenue. | Cho phép gán 1 phần (Partial); chặn không cho gán vượt quá số dư còn lại (`remaining eligible amount`). | **Cần bổ sung validation** chặn over-match (`AC-FD-007`). |
| **Đa hóa đơn & Nhiều Bill** | 1 chứng từ gắn cứng với 1 Bill (hoặc không gắn Bill). | Hóa đơn gộp (consolidated invoice) có thể gán các dòng từ nhiều Bill khác nhau. | Header `BillId` là tham chiếu; detail lưu `BillId` từng dòng. |
| **Tương thích FX Snapshot (ADR-0040)** | `Cost`/`Revenue` đã có FX snapshot; `FinancialDocument` chưa có. | `FinancialDocument` và các dòng match bắt buộc có FX snapshot đầy đủ (`BaseAmount`, `FxRate`, `FxStatus`). | **Cần implement `IReportingFx`** cho `FinancialDocument`. |
| **Xử lý Đa tiền tệ ở Mode B** | Chưa có quy tắc kiểm tra đa ngoại tệ khi chọn dòng. | Không được cộng trực tiếp nếu các dòng khác loại tiền; phải quy đổi sang `Document Currency` theo tỷ giá. | **Cần chặn cộng chéo tiền tệ** (`FD-FX-04`). |
| **Huỷ liên kết (Cancel / Reverse)** | Detail chuyển `reversed`, chưa có rule phục hồi số dư chuẩn. | Hủy matching phải phục hồi lại số dư có thể gắn cho Cost/Revenue gốc, không xóa dữ liệu kinh tế. | Đảm bảo tính toán số dư loại trừ các detail đã hủy. |

---

## 3. Hệ thống Quy tắc Nghiệp vụ (Business Rules)

### 3.1 Quy tắc Dữ liệu Kinh tế & Khớp chứng từ (FD-R01..10)
* **FD-R01 (Single Economic Fact):** Chứng từ tài chính không phải là Cost/Revenue; việc tạo/nhận chứng từ không được làm tăng/giảm số liệu chi phí hoặc doanh thu kinh tế của Bill.
* **FD-R02 (Mode B Amount):** Tổng tiền chứng từ ở Mode B được suy ra từ các dòng Cost/Revenue được chọn; không cho phép nhập tùy tiện.
* **FD-R03 (Mode A Amount):** Tổng tiền chứng từ ở Mode A được nhập theo hóa đơn thực tế bên ngoài; không ép bằng số tiền Cost/Revenue.
* **FD-R04 (Partial & Multi-document):** Một dòng Cost/Revenue có thể được lập chứng từ một phần hoặc qua nhiều chứng từ khác nhau; tổng số tiền đã gán không được vượt quá số dư hợp lệ (`remaining eligible amount`).
* **FD-R05 (Many-to-Many & Multi-Bill):** Một chứng từ có thể liên kết nhiều dòng Cost/Revenue và có thể trải dài trên nhiều Bill khác nhau qua bảng chi tiết khớp.
* **FD-R06 (Bill Filter Reference):** Chọn Bill chỉ là bộ lọc danh sách; tuyệt đối không tự động gán toàn bộ số tiền của Bill vào chứng từ.
* **FD-R07 (Counterparty Isolation):** Chiều Phải trả (AP) chỉ đề xuất Cost của Vendor phù hợp; Chiều Phải thu (AR) chỉ đề xuất Revenue của Customer phù hợp. Chặn việc match chéo đối tác.
* **FD-R08 (Maturity Isolation):** Các dòng ở trạng thái `expected` khi được gán chứng từ không tự động nhảy thành `recognized AP/AR`. Quy trình ghi nhận công nợ độc lập hoàn toàn.
* **FD-R09 (Matching Audit State):** Bảng khớp phải lưu đầy đủ: Giá trị dòng nguồn (`Source Amount`), Số tiền khớp (`Matched Amount`), Số dư còn lại (`Remaining Amount`), Số tiền chứng từ (`Document Amount`) và Chênh lệch (`Variance`).
* **FD-R10 (Safe Reversal):** Khi hủy/reverse chứng từ hoặc liên kết khớp, Cost/Revenue gốc giữ nguyên trạng thái; số dư có thể gắn (`remaining eligible amount`) được tự động phục hồi.

### 3.2 Quy tắc Tỷ giá & Tiền tệ Báo cáo (FD-FX-01..07)
* **FD-FX-01 (Dual Currency):** Lưu cả Nguyên tệ gốc (`TotalAmount`, `CurrencyCode`) và Tiền tệ báo cáo (`BaseAmount`, `ReportingCurrencyCode`).
* **FD-FX-02 (FX Snapshot):** Nếu tiền tệ chứng từ khác tiền tệ báo cáo của Tenant, áp dụng snapshot tỷ giá theo baseline ADR-0040.
* **FD-FX-03 (Same Currency Sum):** Mode B cùng loại tiền: cộng dồn trực tiếp ra `TotalAmount`.
* **FD-FX-04 (Cross Currency Mode B):** Mode B nếu các dòng chọn khác loại tiền: không được cộng số học trực tiếp; phải chọn tiền tệ chứng từ và quy đổi qua tỷ giá FX policy.
* **FD-FX-05 (External FX & Variance):** Mode A: Số tiền và tỷ giá là fact của chứng từ bên ngoài; chênh lệch được tính toán sau quy đổi nhưng không sửa đổi Cost/Revenue gốc.
* **FD-FX-06 (No Zero/One Rate):** Thiếu tỷ giá: tuyệt đối không tự gán bằng 0 hoặc 1; hệ thống sẽ chặn chuyển trạng thái `accepted` hoặc `fully matched` cho đến khi có tỷ giá hợp lệ.
* **FD-FX-07 (Close Immutability):** Tỷ giá thị trường cập nhật hàng ngày không được ghi đè lên chứng từ và liên kết khớp đã lưu trong quá khứ hoặc đã chốt kỳ tài chính.

---

## 4. Thiết kế Luồng Giao diện Người dùng (UI 7 Bước)

Màn hình áp dụng: **`apps/web/app/documents/receive/page.tsx`**

```
+---------------------------------------------------------------------------------------------------+
|  BƯỚC 1: CHIỀU CHỨNG TỪ                                                                           |
|  (o) Phải trả (AP-side / Nhà cung cấp)        ( ) Phải thu (AR-side / Khách hàng)                 |
+---------------------------------------------------------------------------------------------------+
|  BƯỚC 2: CHẾ ĐỘ CHỨNG TỪ                                                                          |
|  [ Mode B: Tạo chứng từ từ dữ liệu LCMS ]     [ Mode A: Nhận chứng từ bên ngoài ]                 |
+---------------------------------------------------------------------------------------------------+
|  BƯỚC 3 & 4: THÔNG TIN CHUNG & BỘ LỌC TÌM KIẾM                                                    |
|  Loại chứng từ: [ Hóa đơn ▼ ]      Số chứng từ: [ INV-2026-001       ]    Ngày: [ 03/10/2026 ]    |
|  Đối tác: [ Công ty Vận tải ABC (Vendor) ▼ ]       Bill lọc (tùy chọn): [ BILL-2026-X123 ▼ ]      |
+---------------------------------------------------------------------------------------------------+
|  BƯỚC 5: BẢNG "KHOẢN TÀI CHÍNH LIÊN QUAN" (ELIGIBLE SOURCE LINES)                                  |
|  [ ] Chọn | Loại | Mã line | Hạng mục / Diễn giải | Tiền tệ | Giá trị gốc | Đã gắn CT | Còn lại | Gắn đợt này |
|  [x]      | Cost | CST-01  | Cước vận chuyển biển | USD     |      400.00 |      0.00 |  400.00 | [  400.00 ] |
|  [x]      | Cost | CST-02  | Phí nâng hạ cont     | USD     |       50.00 |      0.00 |   50.00 | [   50.00 ] |
|  [ ]      | Cost | CST-03  | Phí chứng từ D/O     | USD     |       20.00 |      0.00 |   20.00 | [    0.00 ] |
+---------------------------------------------------------------------------------------------------+
|  BƯỚC 6: TỔNG TIỀN, TỶ GIÁ FX & ĐỐI SOÁT CHÊNH LỆCH (MATCHING SUMMARY)                            |
|  Tiền tệ: [ USD ▼ ]        Tỷ giá báo cáo (USD/VND): 25,450 (VCB Snapshot 03/10)                  |
|  • Mode B: Tổng tiền chứng từ: [ 450.00 USD ] (Tự tính từ các dòng được chọn - Khóa nhập)        |
|  • Mode A: Tổng tiền hóa đơn:  [ 460.00 USD ] (Cho phép nhập tay)                                 |
|            Tổng tiền đã khớp:    450.00 USD                                                       |
|            Chênh lệch (Variance): +10.00 USD (Hóa đơn lớn hơn chi phí trong hệ thống)             |
+---------------------------------------------------------------------------------------------------+
|  BƯỚC 7: THAO TÁC                                                                                 |
|  [ Hủy bỏ ]                                              [ Lưu chứng từ & Xác nhận khớp dòng ]     |
+---------------------------------------------------------------------------------------------------+
```

---

## 5. Kế hoạch Triển khai Kỹ thuật (Phases & Tasks)

### Phase 1: Data Model & Database Migration
- [x] **Task 1.1:** Mở rộng entity `FinancialDocument`:
  - Thêm property `Mode` (`external_received`, `lcms_generated`).
  - Triển khai interface `IReportingFx`: `BaseAmount`, `ReportingCurrencyCode`, `FxRate`, `FxSourceType`, `FxSourceName`, `FxRateDate`, `FxRateId`, `FxOverrideReason`, `FxAppliedBy`, `FxAppliedAt`, `FxStatus`.
- [x] **Task 1.2:** Mở rộng entity `DocumentMatchDetail`:
  - Bổ sung `SourceOriginalAmount`, `MatchedReportingAmount`, `VarianceAmount`, `BillId`.
- [x] **Task 1.3:** Tạo EF Core Migration và cập nhật `LcmsDbContextModelSnapshot`.

### Phase 2: Backend Application Layer (Queries, Commands, Rules)
- [x] **Task 2.1:** Viết Query `GetEligibleDocumentSourceLinesQuery`:
  - Lấy danh sách Cost (AP) hoặc Revenue (AR) theo đối tác, Bill, tiền tệ.
  - Tính toán `AlreadyDocumentedAmount` và `RemainingEligibleAmount = OriginalAmount - AlreadyDocumentedAmount`.
  - Loại trừ các detail đã bị hủy (`reversed` / `cancelled`).
- [x] **Task 2.2:** Cập nhật `ReceiveFinancialDocumentCommand`:
  - Tiếp nhận `Mode` và danh sách dòng đã chọn (`SelectedSourceLines`).
  - Thực thi kiểm tra Rule FD-R02 (Mode B: validate `TotalAmount` khớp đúng tổng dòng chọn).
  - Thực thi kiểm tra Rule FD-R03 (Mode A: tính và lưu `VarianceAmount`).
  - Thực thi kiểm tra Rule FD-R04 & FD-R07 (chặn over-match, kiểm tra đúng đối tác).
  - Tích hợp `FxSnapshotService` để tạo FX snapshot cho chứng từ.
  - Tự động sinh `DocumentMatch` + `DocumentMatchDetail` tương ứng.
- [x] **Task 2.3:** Kiểm tra Rule FD-R10:
  - Kiểm tra các lệnh reverse matching đảm bảo phục hồi lại `RemainingEligibleAmount`.

### Phase 3: API & BFF Layer
- [x] **Task 3.1:** Thêm API endpoint `GET /api/financial-documents/eligible-source-lines`.
- [x] **Task 3.2:** Cập nhật API endpoint `POST /api/financial-documents` nhận `mode` và `selectedLines`.
- [x] **Task 3.3:** Cập nhật Next.js BFF routes trong `apps/web/app/bff/financial-documents/`.

### Phase 4: Frontend UI Redesign
- [x] **Task 4.1:** Thiết kế lại form `ReceiveDocumentForm.tsx` theo luồng 7 bước chuẩn PO.
- [x] **Task 4.2:** Tích hợp bảng động "Khoản tài chính liên quan" với checkbox chọn dòng, auto-fill số dư còn lại, cho phép sửa số tiền gán đợt này.
- [x] **Task 4.3:** Tự động đồng bộ và tính tổng tiền ở Mode B (Readonly) và hiển thị box đối soát chênh lệch (Variance) ở Mode A.
- [x] **Task 4.4:** Tích hợp component preview tỷ giá FX (`FxRateBox` / `CurrencySelect`).
- [x] **Task 4.5:** Cập nhật trang chi tiết `/documents/[id]` hiển thị danh sách các khoản kinh tế liên kết (`Linked Economic Lines`) và chênh lệch matching.

### Phase 5: Kiểm thử Tự động & Nghiệm thu AC
- [x] **Task 5.1:** Viết bộ test tích hợp `tests/LCMS.Api.Tests/FinancialDocumentSourceMatchingTests.cs` bao phủ toàn bộ 20 Acceptance Criteria (`AC-FD-001` đến `AC-FD-020`).
- [x] **Task 5.2:** Chạy UAT Smoke Test (`UAT-FD-01` đến `UAT-FD-08`).
- [x] **Task 5.3:** Cập nhật `docs/handoff.md` theo quy định dự án.

---

## 6. Danh mục Tiêu chí Nghiệm thu (Acceptance Criteria Checklist)

| Mã AC | Ngữ cảnh kiểm thử | Mô tả ca kiểm thử | Kết quả kỳ vọng | Ưu tiên |
| :--- | :--- | :--- | :--- | :--- |
| **AC-FD-001** | AR / Mode B | Bill có Revenue 400 + 50 + 20 USD; chọn 400 + 50 USD. | Tổng tiền tự tính = 450 USD; dòng 20 USD còn nguyên số dư có thể gắn. | Critical |
| **AC-FD-002** | AP / Mode B | Cost 300 + 20 USD; chọn cả 2 dòng. | Tổng tiền tự tính = 320 USD; ô Tổng tiền bị khóa không sửa tay. | Critical |
| **AC-FD-003** | AP / Mode A | Cost 320 USD; hóa đơn Vendor gửi 330 USD. | Cho nhập 330 USD; match 320 USD; 10 USD hiển thị là Unmatched/Variance. | Critical |
| **AC-FD-004** | Bill Reference | Chọn Bill có sẵn nhiều dòng chi phí/doanh thu. | Không tự ý lấy toàn bộ tiền của Bill nếu người dùng chưa tick chọn dòng. | Critical |
| **AC-FD-005** | Partial Matching | Dòng Revenue 400 USD; gán vào chứng từ 250 USD. | Số dư còn lại = 150 USD; không bị double count doanh thu. | Critical |
| **AC-FD-006** | Multiple Documents| Dòng 400 USD đã gán 250 USD; tạo chứng từ thứ hai chọn tiếp. | Hệ thống chỉ cho phép gán tối đa 150 USD; sau khi gán còn 0 USD. | High |
| **AC-FD-007** | Over-match | Dòng còn số dư 150 USD; người dùng cố tình gán 200 USD. | Bị chặn (Validation Error 400/422); không cho phép ghi nhận âm. | Critical |
| **AC-FD-008** | Many-to-Many | Một chứng từ chọn nhiều dòng từ các Bill khác nhau. | Lưu trữ chi tiết matching chính xác cho từng dòng và từng Bill. | High |
| **AC-FD-009** | AP Filter | Chọn chiều = AP. | Chỉ hiển thị Cost lines của Vendor; không bao giờ hiện Revenue. | Critical |
| **AC-FD-010** | AR Filter | Chọn chiều = AR. | Chỉ hiển thị Revenue lines của Customer; không bao giờ hiện Cost. | Critical |
| **AC-FD-011** | Economic Fact | Tạo hoặc nhận chứng từ tài chính. | Số liệu Cost/Revenue kinh tế của Bill không bị tăng thêm hay duplicate. | Critical |
| **AC-FD-012** | Maturity Gate | Dòng Cost/Revenue ở trạng thái `expected`. | Gán chứng từ không được tự động chuyển thành recognized AP/AR. | Critical |
| **AC-FD-013** | Same Currency | Nguồn USD + Chứng từ USD. | Khớp số tiền gốc trực tiếp; snapshot tỷ giá theo đồng tiền báo cáo. | High |
| **AC-FD-014** | Multi-Currency | Chọn các dòng nguồn khác nhau về loại tiền tệ ở Mode B. | Chặn cộng gộp trực tiếp; yêu cầu chọn tiền tệ chứng từ và quy đổi FX. | Critical |
| **AC-FD-015** | External FX | Hóa đơn USD, đồng tiền báo cáo là VND. | Lưu song song: Original USD + BaseAmount VND + Tỷ giá VCB snapshot. | Critical |
| **AC-FD-016** | Missing FX | Chứng từ ngoại tệ nhưng chưa có tỷ giá ngày. | Chặn không gán tỷ giá 0 hoặc 1; block trạng thái fully matched / accepted. | Critical |
| **AC-FD-017** | Historical Rate | Tỷ giá Vietcombank ngày hôm sau thay đổi. | Không ghi đè hoặc tính toán lại số liệu của chứng từ/khớp đã lưu trước đó. | Critical |
| **AC-FD-018** | Cancel Link | Hủy chứng từ hoặc hủy dòng khớp. | Cost/Revenue gốc giữ nguyên; số dư có thể gán được phục hồi đầy đủ. | High |
| **AC-FD-019** | Counterparty | Chứng từ Khách hàng A nhưng chọn dòng của Khách hàng B. | Bị chặn không cho phép khớp chéo đối tác. | High |
| **AC-FD-020** | Audit Trail | Mọi thao tác tạo/sửa/khớp/hủy liên kết. | Ghi log đầy đủ vào `audit_events` (Actor, Timestamp, Old/New payload). | High |

---

## 7. Ranh giới Ngoài phạm vi (Out-of-Scope)
Theo thỏa thuận tại Mục 10 của tài liệu PO:
1. **Không thay đổi** mô hình trưởng thành 3 tầng của Cost/Revenue (`Expected` → `Confirmed` → `Actual`).
2. **Không thay đổi** quy trình thanh toán / tất toán công nợ (`Settlement / Payment Allocation`).
3. **Không biến** Financial Document thành sổ cái kế toán kép (General Ledger).
4. **Không xử lý** lại Rating UX hay Container/Package logic trong batch này.
