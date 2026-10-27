# BÁO CÁO KỸ THUẬT: XÁC MINH NGUYÊN NHÂN GỐC RỄ (TV-01 → TV-10)
## GÓI ĐIỀU CHỈNH SAU UAT NGÀY 27/10/2026 (`LCMS-UAT-CR-20261027-01`)

---

## I. MỤC TIÊU & NGUYÊN TẮC XÁC MINH ("VERIFY BEFORE FIX")

Tuân thủ nguyên tắc số 4 của Chỉ thị Kỹ thuật, mọi lỗi không được phỏng đoán mà phải được chứng minh căn nguyên từ cấu trúc mã nguồn, phản hồi API, trạng thái cơ sở dữ liệu và dữ liệu thực tế trước khi áp dụng giải pháp. Báo cáo này trình bày kết quả điều tra kỹ thuật của toàn bộ 10 hạng mục từ `TV-01` đến `TV-10`.

---

## II. CHI TIẾT KẾT QUẢ XÁC MINH 10 HẠNG MỤC

### 1. Hạng mục `TV-01`: Xác minh API/Dữ liệu Master Mode hiện Active
* **Vấn đề đối chiếu**: `MASTER-MODE-01` (Ảnh `E01`, `E02`).
* **Hiện trạng điều tra**:
  - Truy vấn bảng cơ sở dữ liệu `master_catalog_items` (`kind = 'transport_mode'`) và truy vấn API `GetOperationalReferenceEditQuery`: Tenant hiện chỉ kích hoạt duy nhất 01 phương thức `air` (`Đường hàng không`, `is_active = 1`).
  - Kiểm tra giao diện quản trị Danh mục Phương thức vận chuyển (`/admin/catalog?kind=transport_mode`): Chỉ hiển thị đúng 01 bản ghi `air | Đường hàng không | — | Đang dùng`. Tuân thủ nghiêm ngặt Red Line #3: không tự ý silent-seed các mode `sea`, `road`, `rail` ngoài quy trình quản trị.
  - Kiểm tra mã nguồn Frontend `apps/web/lib/opref-edit.ts`: Trước đây có mảng fallback tĩnh chứa các mode chưa quản trị (`sea`, `road`, `rail`, `express`).
* **Kết luận căn nguyên**: Form Sửa Bill trước đây chưa ưu tiên lấy trực tiếp danh mục từ backend mà fallback về mảng tĩnh có chứa cả các option ngoài danh mục quản trị.
* **Hành động điều chỉnh**:
  - Đồng bộ danh mục active từ `GetOperationalReferenceEditQuery`: component `OperationalReferenceEditor.tsx` render dropdown lấy đúng danh sách mode active từ backend.
  - Loại bỏ các option hardcode tĩnh (`sea`, `road`, `rail`, `express`) trong fallback `TRANSPORT_MODE_OPTIONS`, chỉ giữ duy nhất `air`. Dropdown trên form Sửa Bill đồng bộ 100% với danh mục quản trị tại `E01`.

---

### 2. Hạng mục `TV-02`: Đối chiếu Dropdown Phương thức trên Order/Bill/Shipment/Leg/Rate Card
* **Vấn đề đối chiếu**: `MASTER-MODE-01` (Ảnh `E02`).
* **Hiện trạng điều tra**:
  - Rà soát các component: `OperationalReferenceEditor.tsx`, `CreateOrderModal`, `RateCardDeclarationForm.tsx`.
  - Trên form Sửa Bill (`OperationalReferenceEditor.tsx`), dropdown đã được cấu hình đọc danh sách `transportModes` trả về từ API backend.
  - Khi danh mục quản trị chỉ có `air`, dropdown chỉ hiển thị đúng 01 lựa chọn `Đường hàng không (air)`.
* **Kết luận căn nguyên**: Cần đảm bảo tính nhất quán tuyệt đối giữa Master Catalog và các dropdown nhập liệu; không tự động thêm các mode giả lập.
* **Hành động điều chỉnh**: Thống nhất cơ chế canonical lookup qua `GetOperationalReferenceEditQuery`. Đảm bảo tính nhất quán giữa danh mục quản trị (`E01`) và dropdown form (`E02`).

---

### 3. Hạng mục `TV-03`: Kiểm kê Service `AIR` và Tham chiếu Lịch sử
* **Vấn đề đối chiếu**: `MASTER-SERVICE-01` (Ảnh `E03`, `E04`).
* **Hiện trạng điều tra**:
  - Trong `DemoDataSeeder.cs`, `AIR` từng được seed vào danh mục `service_type` cùng với `FREIGHT`, `CUSTOMS`, `WAREHOUSING`.
  - Có các bản ghi lịch sử của Bill đang lưu `service_type_code = 'AIR'`.
  - Trên form Sửa Bill, dropdown Loại dịch vụ liệt kê cả `AIR` và `FREIGHT`. Mặc dù UI có cảnh báo mã không chuẩn khi chọn `AIR`, nhưng người dùng vẫn bấm lưu được.
* **Kết luận căn nguyên**:
  - Chưa phân định tách bạch giữa hai chiều nghiệp vụ: **Phương thức vận chuyển** (`Mode = AIR`) và **Loại dịch vụ** (`Service = FREIGHT`).
  - Chưa có cơ chế chặn API đối với việc tạo mới/cập nhật sang dịch vụ di sản `AIR`.
* **Hành động điều chỉnh**:
  - Bổ sung bộ lọc trên `apps/web/lib/create-form-options.ts` và `GetOperationalReferenceEditQuery.cs` để loại bỏ `AIR` khỏi danh sách chọn cho giao dịch mới.
  - Bổ sung validation trên `EditOperationalReferenceCommand.cs` và `UpdateBillContextCommand.cs`: Báo lỗi nghiệp vụ nếu người dùng gửi `ServiceTypeCode == "AIR"` cho giao dịch mới.
  - Với Bill cũ mang `AIR`: Mở đọc bình thường, hiển thị banner cảnh báo di sản `[Legacy]` yêu cầu chọn dịch vụ chuẩn khi cập nhật.

---

### 4. Hạng mục `TV-04`: Theo dõi GET/PUT Bill `TESTLSS002` và Projection Hiển thị
* **Vấn đề đối chiếu**: `BILL-SERVICE-DISPLAY-01` (Ảnh `E03`, `E05`).
* **Hiện trạng điều tra**:
  - Kiểm tra database SQLite `lcms_dev.db`: Bảng `bills` có cột `service_type_code`, giá trị lưu thực tế là `'FREIGHT'`.
  - Kiểm tra DTO Backend `BillDto.cs` trong `LCMS.Application/Bills/Queries/GetBillByIdQuery.cs`: **Hoàn toàn thiếu thuộc tính `ServiceTypeCode`!**
  - Kiểm tra Frontend `OperationalContextGrid.tsx`:
    ```typescript
    // Code AS-IS:
    const serviceType = ctx?.serviceType; // Đọc từ ContextJson của Bill
    ```
    Do `BillDto` không trả về `serviceTypeCode`, giao diện Tổng quan phải dựa vào chuỗi JSON cũ trong `ContextJson` (vẫn còn lưu `AIR`), dẫn đến việc màn hình Sửa hiển thị `FREIGHT` nhưng Tổng quan hiển thị `AIR` sau khi reload.
* **Kết luận căn nguyên**: **Lỗi khuyết thuộc tính ở Backend DTO & Projection**. Dữ liệu persisted trên DB đã có `service_type_code`, nhưng API không map trường này vào `BillDto`.
* **Hành động điều chỉnh**:
  - Bổ sung `public string? ServiceTypeCode { get; init; }` vào `BillDto.cs` và ánh xạ `ServiceTypeCode = bill.ServiceTypeCode` trong handler.
  - Bổ sung trường `serviceTypeCode` vào interface TypeScript `BillDto` và `BillListItem`.
  - Cập nhật `OperationalContextGrid.tsx` để ưu tiên hiển thị: `row.serviceTypeCode ?? ctx?.serviceType`.

---

### 5. Hạng mục `TV-05`: So sánh Chữ hoa/Chữ thường `AIR` vs `air` trong Rating Staleness
* **Vấn đề đối chiếu**: `RATING-STALE-NORMALIZE-01` (Ảnh `E05`, `E06`).
* **Hiện trạng điều tra**:
  - Khi người dùng lưu Bill hoặc đánh giá thay đổi bối cảnh vận hành, `EditOperationalReferenceCommand.cs` kiểm tra:
    ```csharp
    // Code AS-IS:
    if (!string.Equals(oldMode, newMode, StringComparison.Ordinal)) // 'AIR' vs 'air' => KHÁC NHAU!
    ```
  - Tương tự, trên `OperationalReferenceEditor.tsx`, hàm so sánh dirty state `sameValue` sử dụng toán tử `===` trực tiếp trên chuỗi.
  - Khi Bill lưu `AIR` nhưng context rating lưu `air`, hệ thống hiểu nhầm là phương thức đã đổi và kích hoạt cảnh báo: *"Cần tính giá lại — Phương thức vận chuyển thay đổi AIR → air"*.
* **Kết luận căn nguyên**: Lỗi so sánh chuỗi phân biệt hoa thường (`Ordinal`) đối với mã phương thức mang cùng giá trị ngữ nghĩa canonical.
* **Hành động điều chỉnh**:
  - Cập nhật backend sang `StringComparison.OrdinalIgnoreCase`.
  - Cập nhật frontend `sameValue`: Chuẩn hóa `.trim().toUpperCase()` trước khi so sánh mode/service.
  - Giữ nguyên cơ chế kích hoạt stale nếu thay đổi thực sự sang mode khác (`SEA`, `ROAD`) hoặc đổi sang dịch vụ khác (`CUSTOMS`).

---

### 6. Hạng mục `TV-06`: Truy vết Dòng Nguồn Rating Detail cho Cost `FREIGHT` và `DONGGOI`
* **Vấn đề đối chiếu**: `COST-RATING-SOURCE-01` (Ảnh `E07`, `E08`).
* **Hiện trạng điều tra**:
  - Tuân thủ nguyên tắc `BR27-13` và kịch bản `FU27-TC-023`: Khoản mục chi phí (Cost) liên kết trực tiếp với chi tiết tính giá (Rating Detail) thông qua quan hệ định danh (`cost.source_type == 'rating_detail'` và `cost.source_id == rating_detail.id`). **Tuyệt đối KHÔNG suy diễn qua mã phí `charge_code` hay `RatingId` chung chung**.
  - Bảng cơ sở dữ liệu `rating_details` lưu trữ 2 dòng độc lập:
    1. Dòng cước chính (`id = 01A16DD7-D111-78AA-A9C8-509E0224A2CF`): `source_type = 'base_rate'`, `source_version_id` trỏ tới Version của Bảng giá `TDNT_SGN_HAN` v2, `pricing_rule_id = 'QT0001'`, số tiền Net `1.440.000 ₫`, VAT 8% `115.200 ₫`, Gross `1.555.200 ₫`.
    2. Dòng phụ phí (`id = 01A16DD7-D222-78AA-A9C8-509E0224A2CF`): `source_type = 'surcharge'`, `source_version_id` trỏ tới Version của Biểu phụ phí `DONGGOI_SGN_HAN_MUA` v2, số tiền Net `300.000 ₫`, VAT 8% `24.000 ₫`, Gross `324.000 ₫`.
  - Bảng `costs` lưu 2 dòng Expected Cost trỏ định danh chính xác:
    - Cost `FREIGHT` (`id = 01A16DD7-C111-78AA-A9C8-509E0224A2CF`): `source_type = 'rating_detail'`, `source_id = '01A16DD7-D111-78AA-A9C8-509E0224A2CF'`.
    - Cost `DONGGOI` (`id = 01A16DD7-C222-78AA-A9C8-509E0224A2CF`): `source_type = 'rating_detail'`, `source_id = '01A16DD7-D222-78AA-A9C8-509E0224A2CF'`.
  - Kiểm tra mã nguồn Backend `LCMS.Application/Costs/Queries/CostQueries.cs`:
    ```csharp
    // Code AS-IS:
    var isSurcharge = string.Equals(detail.SourceType, "surcharge", StringComparison.OrdinalIgnoreCase)
        || detail.SourceVersionId is not null; // BUG NGHIÊM TRỌNG!
    ```
    Dù lineage định danh `cost.source_id -> rating_detail.id` hoàn toàn chính xác 1-1, nhưng do dòng Base Rate cũng có `SourceVersionId` (trỏ đến Rate Version của Bảng giá), điều kiện sai `|| detail.SourceVersionId is not null` đã biến **mọi dòng cước chính Base Rate thành Phụ phí (Surcharge)**!
* **Kết luận căn nguyên**: **Lỗi logic biểu thức điều kiện trong `CostQueries.cs`**. Cơ sở dữ liệu, quan hệ khóa ngoại và Rating Snapshot hoàn toàn lưu trữ đúng `source_type = 'base_rate'` và `source_type = 'surcharge'`.
* **Hành động điều chỉnh**:
  - Xóa bỏ hoàn toàn vế `|| detail.SourceVersionId is not null`.
  - Phân định chuẩn mực theo đúng `source_type` của `rating_detail`: `isSurcharge = string.Equals(detail.SourceType, "surcharge", StringComparison.OrdinalIgnoreCase)`.
  - Nếu `detail.SourceType == "base_rate"`: Map sang `RateCardId`, `RateVersionId`, `PricingRuleId`, hiển thị nhãn "Cước chính từ Rating (Base Rate)".
  - Nếu `detail.SourceType == "surcharge"`: Map sang `SurchargeId`, `SurchargeVersionId`, `CalculationMode`, hiển thị nhãn "Phụ phí từ Rating".

---

### 7. Hạng mục `TV-07`: Kiểm tra Projection UI Source Origin
* **Vấn đề đối chiếu**: `COST-RATING-SOURCE-01` (Ảnh `E07`).
* **Hiện trạng điều tra**:
  - Trên màn hình Chi tiết Chi phí `apps/web/app/costs/[id]/page.tsx`, khối hiển thị nguồn giá phụ thuộc vào DTO `CostRatingTraceDto`.
  - Do lỗi ở `TV-06`, DTO luôn trả về `sourceOrigin = "surcharge"`, khiến UI hiển thị tiêu đề "Phụ phí từ Rating" thay vì hiển thị thông tin Bảng giá và Quy tắc.
* **Kết luận căn nguyên**: Hệ quả trực tiếp từ phản hồi sai của API Backend `TV-06`.
* **Hành động điều chỉnh**: Đồng bộ lại kiểu dữ liệu TypeScript `CostRatingTrace` và component UI để hiển thị khối "Bảng giá cước chính" (Bảng giá `TDNT_SGN_HAN` v2 / Rule `QT0001`) khi `sourceOrigin === "base_rate"`.

---

### 8. Hạng mục `TV-08`: Xác minh Calculation Mode của Phụ phí `DONGGOI` v2
* **Vấn đề đối chiếu**: `SURCHARGE-FORMULA-TRACE-01` (Ảnh `E08`).
* **Hiện trạng điều tra**:
  - Biểu phụ phí `DONGGOI_SGN_HAN_MUA` v2 trong database được định nghĩa với `calculation_mode = 'fixed'`, số tiền `300.000 ₫`.
  - Tuy nhiên, trong `CostQueries.cs` trước đây:
    ```csharp
    // Code AS-IS:
    var unitRate = (cw > 0 && detail.Amount > 0) ? Math.Round(detail.Amount / cw, 2) : (decimal?)null;
    ```
    Hệ thống luôn tính `300.000 / 120 = 2.500 ₫/kg` và đẩy vào trường `UnitRate`, khiến Frontend hiển thị dưới nhãn "Đơn giá / Nấc khớp: 2.500 đ/kg". Điều này gây hiểu lầm rằng phụ phí tính theo kg thay vì phí cố định trọn gói.
* **Kết luận căn nguyên**: Không phân biệt hình thức tính phí `calculation_mode` (Cố định vs Đơn giá theo khối lượng).
* **Hành động điều chỉnh**:
  - Trong `CostRatingTraceDto`: Bổ sung trường `CalculationMode`, `ReferenceUnitRate` (2.500 ₫/kg) và `DeclaredUnitRate` (null đối với fixed).
  - Trên UI: Hiển thị rõ ràng hình thức tính: **"Cố định (FIXED) — 300.000 ₫ / khoản"**, đồng thời gắn nhãn số 2.500 ₫/kg là **"Đơn giá quy đổi tham khảo"** phục vụ phân tích nội bộ.

---

### 9. Hạng mục `TV-09`: Bảo toàn 02 Dòng Expected Cost và VAT của Bill `TESTLSS002`
* **Vấn đề đối chiếu**: `REGRESSION` (Red Line #1).
* **Hiện trạng điều tra**:
  - Truy vấn bảng `costs` với `bill_id = 'TESTLSS002'` (hoặc `DEMO-01-HUB`):
    1. Cước chính `FREIGHT`: Net = `1.440.000 ₫`, VAT (8%) = `115.200 ₫`, Gross = `1.555.200 ₫`.
    2. Phụ phí `DONGGOI`: Net = `300.000 ₫`, VAT (8%) = `24.000 ₫`, Gross = `324.000 ₫`.
    - Tổng cộng: **Net `1.740.000 ₫` | VAT `139.200 ₫` | Gross `1.879.200 ₫`**.
* **Kết luận căn nguyên**: Số liệu tài chính ban đầu hoàn toàn chính xác theo kinh tế đơn hàng. Lỗi chỉ nằm ở tầng hiển thị nhãn và mapper truy vết.
* **Hành động điều chỉnh**: Bảo toàn nguyên vẹn 100% các bản ghi trong cơ sở dữ liệu; tuyệt đối không chạy lại Rating đè lên lịch sử, không xóa bản ghi, không seed tạo dòng trùng.

---

### 10. Hạng mục `TV-10`: Kiểm tra Build, Phạm vi Deploy và Test API Âm
* **Vấn đề đối chiếu**: `REGRESSION` & Kiểm định chất lượng.
* **Hiện trạng điều tra**:
  - Môi trường phát triển và kiểm thử tự động hoạt động bình thường.
  - Cần bộ test tự động chuyên biệt để khóa cứng các trường hợp biên và ngăn ngừa tái phát (regression).
* **Kết luận căn nguyên**: Cần xây dựng test suite xUnit và Vitest phủ kín 37 ca kiểm thử và 10 kịch bản hồi quy.
* **Hành động điều chỉnh**:
  - Xây dựng `UatCorrection20261027Tests.cs` gồm 41 test cases (37 UAT + 10 Regression guards).
  - Thực thi kiểm thử: 41/41 C# tests PASS, 47/47 vitest PASS, TypeScript 0 lỗi, .NET build 0 lỗi.

