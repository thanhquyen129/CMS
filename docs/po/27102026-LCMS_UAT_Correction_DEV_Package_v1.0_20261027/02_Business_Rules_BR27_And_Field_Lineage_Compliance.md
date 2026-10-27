# BẢNG TUÂN THỦ QUY TẮC NGHIỆP VỤ (BR27-01..24), FIELD LINEAGE & HỒI QUY
## GÓI ĐIỀU CHỈNH SAU UAT NGÀY 27/10/2026 (`LCMS-UAT-CR-20261027-01`)

---

## PHẦN 1: MA TRẬN TUÂN THỦ 24 QUY TẮC NGHIỆP VỤ (BR27-01 → BR27-24)

| BR ID | Vấn đề liên quan | Quy tắc nghiệp vụ bắt buộc | Giải pháp & Vị trí code triển khai | Trạng thái DEV |
| :---: | :--- | :--- | :--- | :---: |
| **BR27-01** | `MASTER-MODE-01` | Phương thức vận chuyển có canonical code và nguồn danh mục được quản trị duy nhất. | Tích hợp `GetOperationalReferenceEditQuery.TransportModes` từ master catalog; loại bỏ hardcode. | **COMPLIANT** |
| **BR27-02** | `MASTER-MODE-01` | Dropdown chỉ chứa giá trị đủ điều kiện theo tenant/status; không tự bổ sung lựa chọn ngoài nguồn chuẩn. | Dropdown form Sửa Bill chỉ render các item từ API; loại bỏ option `express` chưa quản trị. | **COMPLIANT** |
| **BR27-03** | `MASTER-MODE-01` | Cấu hình hệ thống chỉ-đọc (nếu có) phải xuất hiện minh bạch trên trang danh mục; không tạo dữ liệu ảo. | Danh mục hiển thị rõ mã hệ thống vs tenant; không chạy silent-seed tùy tiện. | **COMPLIANT** |
| **BR27-04** | `MASTER-MODE-01` | Canonical mapping cho `AIR`/`air`; không tự gộp `EXPRESS`/`WATER`/`SEA`. | Chuẩn hóa ánh xạ chuỗi `air` → `AIR`; giữ nguyên phân định các mode độc lập. | **COMPLIANT** |
| **BR27-05** | `MASTER-SERVICE-01` | Loại dịch vụ và Phương thức là hai chiều nghiệp vụ độc lập, dùng 2 nguồn master riêng biệt. | Tách bạch `service_type_code` (`FREIGHT`) và `transport_mode` (`AIR`) trên toàn bộ hệ thống. | **COMPLIANT** |
| **BR27-06** | `MASTER-SERVICE-01` | Không cho chọn service `AIR` trên Bill mới; với Bill cũ mang `AIR` phải nhận diện legacy và cảnh báo. | Lọc `AIR` khỏi dropdown tạo mới; hiển thị banner cảnh báo di sản cho bản ghi lịch sử. | **COMPLIANT** |
| **BR27-07** | `MASTER-SERVICE-01` | API kiểm tra service master canonical; không tự động SQL mass-update `AIR` thành `FREIGHT` hoặc xóa cũ. | `EditOperationalReferenceCommand` chặn gán `AIR` mới; bảo toàn 100% dữ liệu lịch sử. | **COMPLIANT** |
| **BR27-08** | `BILL-SERVICE-DISPLAY-01` | Sửa Bill, Tổng quan và Rating Context phải đọc cùng `serviceTypeCode` persisted; không lấy `billType`. | Bổ sung `ServiceTypeCode` vào `BillDto`; `OperationalContextGrid` ưu tiên đọc cột này. | **COMPLIANT** |
| **BR27-09** | `BILL-SERVICE-DISPLAY-01` | Thao tác lưu/reload phản ánh đúng dữ liệu DB; phân biệt form nháp chưa lưu với giá trị đã lưu. | Sync dirty-state trên form; sau khi lưu reload hiển thị chính xác giá trị persisted. | **COMPLIANT** |
| **BR27-10** | `RATING-STALE-NORMALIZE-01` | Staleness so sánh theo canonical business value; không so sánh trực tiếp hoa/thường nếu mã tương đương. | Áp dụng `StringComparison.OrdinalIgnoreCase` khi so sánh `AIR` vs `air`; loại bỏ stale giả. | **COMPLIANT** |
| **BR27-11** | `RATING-STALE-NORMALIZE-01` | Thay đổi nghiệp vụ thực sự (service, mode, route, CW, date) phải kích hoạt stale với lý do cụ thể. | Đổi mode sang `SEA` hoặc đổi CW sinh đúng lý do stale tương ứng; không bỏ sót. | **COMPLIANT** |
| **BR27-12** | `RATING-STALE-NORMALIZE-01` | Không tự rerate, sửa Rating/Cost đã seed hoặc bỏ qua lý do stale khác khi sửa stale `AIR`→`air`. | Bảo toàn snapshot tính giá gốc; không tự ý chạy lại rating ngoài ý muốn người dùng. | **COMPLIANT** |
| **BR27-13** | `COST-RATING-SOURCE-01` | Nguồn của Cost được truy vết từ đúng `rating_detail` có định danh và snapshot bất biến. | Khắc phục liên kết 1-1 giữa chi phí và dòng chi tiết tính giá trong `CostQueries.cs`. | **COMPLIANT** |
| **BR27-14** | `COST-RATING-SOURCE-01` | Detail loại `base_rate` hiển thị Rate Card ID/code, Version, Pricing Rule, thông số tính giá và Vendor. | Trả về `TDNT_SGN_HAN` v2 / `QT0001`, CW `120 kg` × `12.000 ₫/kg`, gắn nhãn "Bảng giá cước chính". | **COMPLIANT** |
| **BR27-15** | `COST-RATING-SOURCE-01` | Detail loại `surcharge` hiển thị Surcharge ID/code, Version, Rule/mode, bối cảnh áp dụng. | Trả về `DONGGOI_SGN_HAN_MUA` v2, cách tính `fixed`, gắn nhãn "Phụ phí độc lập". | **COMPLIANT** |
| **BR27-16** | `COST-RATING-SOURCE-01` | Nếu lineage không đủ, hiển thị trạng thái thiếu nguồn và báo GAP; không bịa đặt thông tin nguồn. | Gắn cờ `missing_link` và hiển thị thông báo thiếu liên kết nếu không tìm thấy snapshot. | **COMPLIANT** |
| **BR27-17** | `COST-RATING-SOURCE-01` | Fix trace không tạo Cost mới hoặc sửa số Net/VAT/Gross; không viết lại published Version/Snapshot. | Giữ nguyên vẹn 2 dòng Expected Cost: Net `1.740.000 ₫`, VAT `139.200 ₫`, Gross `1.879.200 ₫`. | **COMPLIANT** |
| **BR27-18** | `SURCHARGE-FORMULA-TRACE-01` | Công thức hiển thị phản ánh `calculation_mode` và tham số gốc, phân biệt FIXED với UNIT_RATE theo kg. | Trả về `CalculationMode = 'fixed'`, hiển thị "Cố định (FIXED) — 300.000 ₫ / khoản". | **COMPLIANT** |
| **BR27-19** | `SURCHARGE-FORMULA-TRACE-01** | Nếu UI trình bày amount/CW tham khảo, phải gắn nhãn "đơn giá quy đổi tham khảo", không xem là đơn giá gốc. | Trường `ReferenceUnitRate = 2.500 ₫/kg` được chú thích rõ là đơn giá quy đổi tham khảo. | **COMPLIANT** |
| **BR27-20** | `SURCHARGE-FORMULA-TRACE-01` | Không đổi cách tính nguồn khi chứng cứ chỉ mâu thuẫn nhãn; kiểm tra `calculation_mode` trước khi sửa engine. | Động cơ tính giá giữ nguyên tính đúng; chỉ sửa tầng DTO projection và UI display label. | **COMPLIANT** |
| **BR27-21** | `REGRESSION` | BUY chỉ Seed Expected Cost, SELL chỉ Seed Expected Revenue; API chặn trái chiều; retry không tạo trùng. | Kiểm tra `IsBuyNature` trên lệnh seed; từ chối gọi trái chiều tại API domain level. | **COMPLIANT** |
| **BR27-22** | `REGRESSION` | Giữ Net `1.740.000 ₫`, VAT `139.200 ₫`, Gross `1.879.200 ₫`; hai Cost độc lập `1.440.000 ₫` và `300.000 ₫`. | Khóa bất biến 2 bản ghi trong `lcms_dev.db`; kiểm chứng qua test tự động `REG-05`. | **COMPLIANT** |
| **BR27-23** | `REGRESSION` | Giữ phiên bản `TDNT_SGN_HAN` v1/v2 bất biến; UAT không tự đánh PASS từ test DEV. | Không cho sửa published versions; cột PO UAT bảo lưu `NOT RUN` 100%. | **COMPLIANT** |
| **BR27-24** | `REGRESSION` | Quan hệ N:N (`Bill ↔ Order`, `Bill ↔ Shipment`, `Bill ↔ Leg`); bảo toàn Single Economic Cost. | Kiến trúc Shared Cost và phân bổ tài chính giữ nguyên vẹn, không ghi trùng fact. | **COMPLIANT** |

---

## PHẦN 2: MA TRẬN ĐỐI CHIẾU 12 TRƯỜNG DỮ LIỆU & SCHEMA THỰC TẾ (FIELD LINEAGE)

| Field Logic (từ Excel) | Tên thực tế trong DB / DTO | Nguồn chuẩn (Source of Truth) | Màn hình sử dụng | Hành vi TO-BE sau điều chỉnh |
| :--- | :--- | :--- | :--- | :--- |
| `transport_mode` | Cột `bills.transport_mode` | Master Catalog (`transport_modes`) | Order, Bill, Shipment, Leg, Rating | Dropdown lấy từ API `GetOperationalReferenceEditQuery`. |
| `service_type_code` | Cột `bills.service_type_code` | Master Catalog (`service_types`) | Order, Bill, Shipment, Leg, Rating | Phân định độc lập với mode; chặn `AIR` trên giao dịch mới. |
| `bill.serviceTypeCode` | `BillDto.ServiceTypeCode` | Bảng `bills` (Persisted column) | Bill Sửa, Bill Tổng quan, Rating | Thêm trường vào `BillDto`; hiển thị đồng bộ `FREIGHT`. |
| `bill.transportMode` | `BillDto.TransportMode` | Bảng `bills` (Canonicalized) | Bill Sửa, Bill Tổng quan, Rating | So sánh không phân biệt hoa thường (`OrdinalIgnoreCase`). |
| `rating.ContextJson` | `rating_snapshots.context_json` | Rating Immutable Snapshot | Rating Read / Stale check | So sánh bối cảnh chuẩn hóa; loại trừ stale giả `AIR` vs `air`. |
| `cost → rating_detail FK` | `costs.source_rating_id` / `charge_code` | Bảng `costs` & `rating_details` | Chi tiết Cost / API | Liên kết 1-1 giữa chi phí và dòng chi tiết tính giá nguồn. |
| `rating_detail.source_type` | `rating_details.source_type` | `rating_details` (`base_rate` / `surcharge`) | Rating Detail, Cost Trace | Đã xóa điều kiện `detail.SourceVersionId is not null` gây lỗi. |
| `rate_card / version / rule IDs` | `source_version_id`, `pricing_rule_id` | Snapshot base rate bất biến | Cost `FREIGHT` | Trả về `TDNT_SGN_HAN` v2 / `QT0001` (Cước chính 1.440.000 ₫). |
| `surcharge / version IDs` | `source_version_id` | Snapshot surcharge bất biến | Cost `DONGGOI` | Trả về `DONGGOI_SGN_HAN_MUA` v2 (Phụ phí 300.000 ₫). |
| `calculation_mode` | `CostRatingTraceDto.CalculationMode` | Surcharge version / Detail snapshot | Cost `DONGGOI` | Trả về `'fixed'`; UI hiển thị "Cố định (FIXED) — 300.000 ₫ / khoản". |
| `unit_rate / effective_unit_rate` | `DeclaredUnitRate` & `ReferenceUnitRate` | Snapshot tham số và số tính toán | Cost `DONGGOI` | `DeclaredUnitRate = null`; `ReferenceUnitRate = 2.500 ₫/kg` (quy đổi). |
| `net / vat / gross amounts` | `costs.amount`, `vat_rate`, `gross_amount` | Bảng `costs` (Persisted) | Cost `FREIGHT`, Cost `DONGGOI` | Net `1.740.000 ₫`, VAT `139.200 ₫`, Gross `1.879.200 ₫` giữ nguyên vẹn. |

---

## PHẦN 3: KẾT QUẢ KIỂM THỬ BẢO VỆ 10 HÀNG RÀO HỒI QUY (REG-01 → REG-10)

| Mã | Luồng được bảo vệ | Điều kiện bất biến bắt buộc | Test method kiểm chứng | Kết quả DEV |
| :---: | :--- | :--- | :--- | :---: |
| **REG-01** | Bảng giá `TDNT_SGN_HAN` v1/v2 | Giữ v1/v2 bất biến; phiên bản mới không thay thế hồi tố. | `UatCorrection20261027Tests.Regression_REG01_RateVersionImmutability` | **PASS** |
| **REG-02** | BUY Bill CW 120 kg × 12.000 ₫ | Đúng công thức cước chính `1.440.000 ₫`. | `UatCorrection20261027Tests.Regression_REG02_BuyBillFreightCalculation` | **PASS** |
| **REG-03** | Independent Surcharge `DONGGOI` | Đúng số tiền phụ phí `300.000 ₫`, không double-apply. | `UatCorrection20261027Tests.Regression_REG03_IndependentSurchargeDonggoi` | **PASS** |
| **REG-04** | Seed Expected Cost | Đúng 2 dòng `FREIGHT` + `DONGGOI`, không sinh dòng thứ 3. | `UatCorrection20261027Tests.Regression_REG04_SeedExpectedCostIdempotency` | **PASS** |
| **REG-05** | Công bố thuế VAT | Net `1.740.000 ₫`, VAT `139.200 ₫`, Gross `1.879.200 ₫`. | `UatCorrection20261027Tests.Regression_REG05_VatDisclosureIntegrity` | **PASS** |
| **REG-06** | Bản chất BUY/SELL | BUY chỉ sinh Cost, SELL chỉ sinh Revenue; chặn trái chiều. | `UatCorrection20261027Tests.Regression_REG06_BuySellNatureConstraint` | **PASS** |
| **REG-07** | Trạng thái Maturity & Audit | Expected ≠ Confirmed ≠ Actual; không sửa bản đã ghi. | `UatCorrection20261027Tests.Regression_REG07_MaturityAndAuditPreservation` | **PASS** |
| **REG-08** | Quan hệ N:N / Shared Cost | Phân bổ không làm thay đổi Single Economic Cost. | `UatCorrection20261027Tests.Regression_REG08_SharedCostAllocationSingleEconomicCost` | **PASS** |
| **REG-09** | Vòng đời Rate Card | Không mở lại phạm vi `RATE-UI-05..07` đã hoàn thành. | `UatCorrection20261027Tests.Regression_REG09_RateCardLifecyclePreserved` | **PASS** |
| **REG-10** | Quản trị Master Data | Không hardcode option rác; tôn trọng dữ liệu master. | `UatCorrection20261027Tests.Regression_REG10_CommodityDeletionIntegrity` | **PASS** |
