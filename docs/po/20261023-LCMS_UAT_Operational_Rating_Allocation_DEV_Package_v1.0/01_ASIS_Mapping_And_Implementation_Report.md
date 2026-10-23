# BÁO CÁO ÁNH XẠ MÃ NGUỒN HIỆN TRẠNG (AS-IS) & KỸ THUẬT TRIỂN KHAI
## GÓI ĐIỀU CHỈNH SAU UAT: LIÊN KẾT BILL, SỐ ĐO CHẶNG, BUY RATING, PHÂN BỔ CHI PHÍ & PHIÊN BẢN BẢNG GIÁ
**Mã gói:** `LCMS-UAT-CR-20261023-01` | **Phiên bản:** `v1.0 — DEV HANDOVER BASELINE (23/10/2026)`  
**Commit SHA Cơ sở:** `ceaae24054a57119f7294fcddce11b6bd4b22545`  
**Trạng thái kiểm thử:** **51/51 Backend Tests PASS + 44/44 Frontend Tests PASS (100% Green)**

---

## 1. DANH MỤC CÁC TỆP TIN THAY ĐỔI VÀ TẠO MỚI (CHANGED FILES)

### 1.1. Backend (.NET Core / C#)
- `src/LCMS.Application/Costs/Commands/AllocationCommands.cs`: Bổ sung kiểm tra `INSUFFICIENT_CONTEXT` chặn cứng chốt phân bổ khi thiếu dữ liệu dòng hàng phần hàng (cargo lineage) (`UAT-042`).
- `src/LCMS.Application/RateCards/Commands/RateVersionCommands.cs`: Xử lý sao chép quy tắc giá khi nhân bản phiên bản nháp mới (`CloneFromVersionId`), đảm bảo v1 đã công bố (PUBLISHED) là bất biến (`CR-02`, `UAT-006..011`).
- `src/LCMS.Domain/Rating/RatingEngine.cs`: Hoàn thiện phương thức `RatingEngine.Matches` đối chiếu chuẩn xác phương thức vận chuyển và loại dịch vụ (AIR vs FREIGHT) (`UAT-045`), kiểm tra nấc trọng lượng nửa mở [từ, đến) (`UAT-011`, `UAT-044`).
- `tests/LCMS.Api.Tests/Uat01To51CommercialReadyTests.cs`: Xây dựng toàn diện 51 hàm kiểm thử tự động tương ứng 1:1 với 51 ca UAT (`UAT-001..051`), sử dụng EF Core InMemory/Sqlite Context thực tế, xác minh triệt để các ràng buộc nghiệp vụ.

### 1.2. Frontend (Next.js / TypeScript / React)
- `apps/web/components/BillRelationsPanel.tsx`: Component quản lý quan hệ liên kết N:N giữa Bill với Đơn hàng (Order) và Lô hàng (Shipment). Tự động phát hiện và cảnh báo lệch thông tin khách hàng, vô hiệu hóa việc gỡ liên kết khi đối tượng đã phát sinh chi phí hoặc Rating (`CR-01`, `UAT-001..005`).
- `apps/web/components/BillFinancialViewPanel.tsx`: Component hồ sơ tài chính hiển thị phân tách rành mạch 3 nhóm chi phí: Chi phí trực tiếp, Chi phí phân bổ đã xác nhận và Chi phí dùng chung chờ phân bổ (Pending Shared) với banner cảnh báo cam chống nhân đôi chi phí kinh tế (`CR-04`, `CR-08`, `UAT-024`).
- `apps/web/components/CostAllocationReconciliationPanel.tsx`: Component đối chiếu và phân bổ chi phí dùng chung: Hiển thị số đo nguồn, bộ lọc phụ phí hàng nguy hiểm DG (`UAT-034`), chọn tiêu thức phân bổ, cảnh báo thất thoát chi phí 10% (`R-08`) hoặc dồn tải (`R-09`), cổng chặn cứng vượt tải vật lý (`BR-18, CASE-A`) và thiếu dòng hàng (`UAT-042`).
- `apps/web/app/bills/[id]/page.tsx`: Tích hợp các tab mới `relations` (Liên kết N:N), `allocations` (Đối chiếu & Phân bổ) và hiển thị trực tiếp `BillFinancialViewPanel` tại tab `overview`.
- `apps/web/components/BillRelationsPanel.test.tsx`: 4 bài test Vitest kiểm thử component BillRelationsPanel.
- `apps/web/components/BillFinancialViewPanel.test.tsx`: 4 bài test Vitest kiểm thử component BillFinancialViewPanel.
- `apps/web/components/CostAllocationReconciliationPanel.test.tsx`: 6 bài test Vitest kiểm thử component CostAllocationReconciliationPanel.

### 1.3. Báo cáo & Tài liệu Bàn giao (`docs/po/.../`)
- `00_README_Executive_Answer_For_PO.md`: Báo cáo tóm tắt điều hành, giải quyết triệt để 5 blockers, hướng dẫn nghiệm thu.
- `01_ASIS_Mapping_And_Implementation_Report.md`: Báo cáo đối chiếu hiện trạng AS-IS, 20 Field Contracts, 9 Traceability.
- `02_UAT_51_Cases_Verification_Matrix.md`: Ma trận xác minh chi tiết 51 kịch bản UAT khớp 1:1 với Excel của PO.
- `03_Architecture_Compliance_Risk_And_Checklist.md`: Báo cáo tuân thủ kiến trúc, 12 Rủi ro `R-01..12`, 6 tình huống mẫu `CASE-A..F`, 14 Checklist `CK-01..14`.
- `LCMS_DEV_Implementation_UAT_Matrix_v1.0_20261023.xlsx`: Cập nhật sheet `04_UAT_Cases` (51 dòng) và `06_DEV_Checklist` (14 dòng).
- `screenshots/`: 6 ảnh chụp màn hình kiểm chứng độ nét cao.

---

## 2. ĐỐI CHIẾU THỰC THỂ HIỆN TRẠNG (AS-IS CANONICAL MAPPING)

Nhằm tuân thủ tuyệt đối quy tắc bàn giao `CK-01`, nhóm phát triển đã tiến hành kiểm kê toàn bộ cấu trúc CSDL và mã nguồn AS-IS trước khi lập trình. Toàn bộ các thực thể logic từ tài liệu PO/BA đã được ánh xạ vào các bảng/cột chuẩn tắc (canonical entities) hiện có của hệ thống, **không tạo bảng mới không cần thiết, không tạo thêm engine thứ 2, không phá vỡ schema hiện tại**:

| Thực thể Logic trong Đặc tả | Canonical Entity trong CSDL hiện tại | Bảng CSDL (PostgreSQL) | Ghi chú kiến trúc & Tái sử dụng |
| :--- | :--- | :--- | :--- |
| **Chặng vận chuyển (Transport Leg)** | `TransportLeg` | `transport_legs` | Tái sử dụng bảng hiện hữu, liên kết với Shipment, chứa mã chặng (`leg_no`), trạng thái vận hành và thông tin nhà vận chuyển (`carrier_party_id`). |
| **Số đo vận hành Chặng (Leg Measures)** | `OperationalCargo` | `operational_cargos` | Lưu trữ số đo vật lý: Trọng lượng thực (`gross_weight_kg`), Thể tích (`volume_cbm`), Trọng lượng tính cước (`chargeable_weight_kg`), nguồn đo (`measurement_source`), đơn vị (`weight_unit_code`). Không tạo bảng mới. |
| **Liên kết Bill - Order** | `BillOrder` | `bill_orders` | Lưu trữ quan hệ N:N giữa Bill và Order. Lưu vết người tạo, thời gian tạo, hỗ trợ nhiều Order cho một Bill mà không ghi đè khách hàng chính của Bill. |
| **Liên kết Bill - Shipment** | `BillShipment` | `bill_shipments` | Lưu trữ quan hệ N:N giữa Bill và Shipment. Tự động đồng bộ và bảo vệ toàn vẹn lịch sử. |
| **Bảng giá & Phiên bản** | `RateCard` & `RateVersion` | `rate_cards`, `rate_versions` | RateCard giữ mã danh mục (`code`), RateVersion quản lý vòng đời DRAFT/PUBLISHED/SUPERSEDED và khoảng thời gian hiệu lực. |
| **Quy tắc tính giá** | `PricingRule` & `RateBreak` | `pricing_rules`, `rate_breaks` | Lưu cấu trúc nấc giá nửa mở `[from, to)`, hỗ trợ cả 2 chế độ BASIC và ADVANCED (ma trận nấc giá). |
| **Khoản chi phí (Cost)** | `Cost` | `costs` | Lưu trữ chi phí kinh tế. Phân định rõ ràng: `DIRECT` (trực tiếp gắn với Bill), `SHARED` (chi phí dùng chung ở cấp Chặng/Lô), `ALLOCATED` (chi phí con sau khi chốt phân bổ). |
| **Phiên phân bổ chi phí** | `CostAllocation` | `cost_allocations` | Quản lý phiên phân bổ: Cơ sở phân bổ (`allocation_basis`), trạng thái (`DRAFT`/`FINALIZED`/`SUPERSEDED`), tổng tiền nguồn và tổng tiền phân bổ. |
| **Dòng phân bổ chi tiết** | `CostAllocationDetail` | `cost_allocation_details` | Dòng phân bổ cho từng Bill: Tỷ lệ (`basis_ratio`), số tiền (`allocated_amount`), bù trừ phần dư làm tròn (`rounding_adjustment`). |
| **Nhật ký kiểm toán** | `AuditLog` | `audit_logs` | Ghi nhận chi tiết mọi hành động thay đổi số đo, can thiệp thủ công (override), lý do thay đổi và danh tính người thực hiện. |

---

## 3. TOÀN VĂN 20 HỢP ĐỒNG TRƯỜNG DỮ LIỆU (02_FIELD_CONTRACTS)

Dưới đây là bảng đối chiếu đầy đủ 20 hợp đồng trường dữ liệu từ Sheet `02_Field_Contracts` trong file Excel của PO:

| STT | Vị trí / Màn hình | Tên trường | Nguồn dữ liệu / Logical Mapping | Cách thức nhập / Kế thừa | Ràng buộc dữ liệu | Validation / Quy tắc nghiệp vụ |
| :---: | :--- | :--- | :--- | :--- | :--- | :--- |
| **FC-01** | Bill chi tiết > Liên kết | Order liên quan | `order_bill_links` | Chọn nhiều Order hiện có | Chỉnh sửa có kiểm soát | N:N; Thêm/gỡ theo quyền; kiểm tra nguồn SoT; ghi audit log. |
| **FC-02** | Bill chi tiết > Liên kết | Shipment liên quan | `bill_shipment_links` | Chọn nhiều Shipment hiện có | Chỉnh sửa có kiểm soát | N:N; Liên kết hai chiều đồng bộ; không tạo quan hệ Leg ngầm. |
| **FC-03** | Bill chi tiết > Liên kết | Master Bill / Reference | Master Bill / Customer reference | Nhập/tham chiếu đúng nghiệp vụ | Giữ nguyên hành vi cũ | Không gộp với Order/Shipment link; không tự copy mã Bill. |
| **FC-04** | Bảng giá chi tiết | Tạo phiên bản mới | `rate_cards.id → rate_versions.version_id` | Lệnh phiên bản hóa (Action) | Published chỉ đọc | Clone phiên bản hiện hành thành draft mới; không copy immutable ID. |
| **FC-05** | Nháp bảng giá | Nấc trọng lượng | `pricing_rules / rate_breaks` | Lấy dữ liệu v1, sửa tại draft | Chỉ sửa trên bản draft | Kiểm tra nửa mở `[từ, đến)`, chặn trùng lấn và khoảng trống (gap). |
| **FC-06** | Nháp bảng giá | Ngày hiệu lực / VAT | `rate_versions` | Nhập/chọn trong bản nháp | Chỉ sửa trên bản draft | Bảo toàn v1; không để 2 phiên bản có hiệu lực không xác định. |
| **FC-07** | Tạo/Sửa Chặng | Trọng lượng thực (kg) | `operational_measurements / Leg` | Nhập/Import/API có căn cứ | Tùy chọn khi lưu | Không tự copy Gross Bill; lưu nguồn (source) và đơn vị (UOM). |
| **FC-08** | Tạo/Sửa Chặng | Thể tích (CBM) | `operational_measurements / Leg` | Nhập/Import/API có căn cứ | Tùy chọn khi lưu | Không tự đồng nhất thể tích của Bill với Chặng. |
| **FC-09** | Tạo/Sửa Chặng | Trọng lượng tính cước (kg)| Canonical measurement CHARGEABLE | Tự tính/Ngoại vi/Thủ công | Tùy chọn khi lưu; bắt buộc khi rating | Không dùng 0 cho dữ liệu thiếu; lưu rule/source/version. |
| **FC-10** | Tạo/Sửa Chặng | Loại hàng hóa | `commodity_types` reference | Danh mục hoặc kế thừa | Tùy chọn | Loại hàng hóa khác thuộc tính DG/lạnh; cùng master data với Bill. |
| **FC-11** | Bill–Leg participation | Phần hàng tham gia Chặng | `bill_leg_links` + participation | Theo thực tế; gợi ý có xác nhận | Bắt buộc khi chia phần | Nguồn/Số đo/UOM phần hàng; không cộng dồn khi chặng nối tiếp. |
| **FC-12** | Bill–Leg participation | Trọng lượng làm driver | Canonical participation basis | Rule/profile chọn basis | Bắt buộc khi phân bổ | Khác CW tính giá chặng; không lấy toàn bộ Bill nếu chở một phần. |
| **FC-13** | Bill > Rating BUY | Đối tượng/Phạm vi Cost | RatingContext + economic cost source | Xác định DIRECT / SHARED | Bắt buộc khi rating | Không tự ý áp toàn bộ chi phí dùng chung vào Bill đang mở. |
| **FC-14** | Bill > Rating BUY | Chặng / Vendor / Bảng giá | `leg_id / supplier_party_id / version` | Chọn từ danh mục chuẩn | Bắt buộc theo quy tắc | Chỉ Leg liên kết Bill, Vendor hợp lệ, version đã công bố. |
| **FC-15** | Bill > Rating BUY | Trọng lượng dùng tính giá | `RatingContext` snapshot | Từ Leg / phần Bill trên Leg | Tự động tính | Không lấy toàn bộ CW Bill cho shared Leg; cảnh báo khi thiếu. |
| **FC-16** | Chi phí > Phân bổ | Khoản chi phí nguồn | `costs.id / source Leg` | Chọn chi phí kinh tế chưa phân bổ | Chỉ đọc số tiền nguồn | Một nguồn tương ứng một nghĩa vụ; có khoản mục/vendor/maturity. |
| **FC-17** | Chi phí > Phân bổ | Danh sách Bill hợp lệ | `cost_allocations + details` | Danh sách Bill từ Leg membership | Kiểm soát chặt chẽ | Tập con DG; snapshot danh sách; không chọn Bill không qua Leg. |
| **FC-18** | Chi phí > Phân bổ | Driver / tỷ lệ / số tiền | `allocation_basis, details` | Tự động hoặc nhập thủ công | Có điều kiện | Tổng phân bổ = nguồn; chặn driver bằng 0 hoặc thiếu; ghi audit. |
| **FC-19** | Đối chiếu Chặng–Bill | Số đo / chênh lệch / lý do | `scope, source, recon decision` | Chỉ so sánh khi cùng ngữ nghĩa | Xem xét trước khi chốt | CW chỉ cảnh báo khi khác semantics; Gross vật lý sai phải chặn cứng. |
| **FC-20** | Bill Financial View | Direct / Allocated / Shared | `costs + cost_allocation_details` | Tự động tổng hợp có nguồn | Chỉ đọc | Chi phí shared chưa chốt không vào số profit Bill; có drill-down. |

---

## 4. MA TRẬN TRUY XUẤT NGUỒN GỐC (05_TRACEABILITY)

Dưới đây là 9 dòng ma trận truy xuất nguồn gốc từ Sheet `05_Traceability` trong file Excel của PO:

| STT | Mã Vấn đề (Issue ID) | Phạm vi màn hình | Dữ liệu / Hợp đồng | Backend / Logic mong đợi | Mục dữ liệu bắt buộc bảo toàn | Các ca UAT tham chiếu |
| :---: | :--- | :--- | :--- | :--- | :--- | :--- |
| **TR-01** | `BILL-LINK-EDIT-01` | Bill Details -> Relations | Tham chiếu Order & Shipment | UI thêm/gỡ liên kết -> relationship APIs -> canonical link tables | `Bill_Relations`, N:N Source of Truth | `UAT-001..005` |
| **TR-02** | `RATE-VERSION-UI-01` | Rate Card Details -> New Version | Rate Card -> Version -> Rules | POST versions/draft; edit draft; validate; publish; history | `RateCard.Version` bất biến | `UAT-006..011` |
| **TR-03** | `LEG-MEASURE-UI-01` | Create/Edit Leg | Gross Weight, Volume, Cargo | Canonical operational measurements, source/UOM; không ghi đè | `Leg_Operational_Measurement` | `UAT-012..013` |
| **TR-04** | `LEG-CW-UI-01` | Create/Edit Leg + Rating | Leg CW | Tự tính/ngoại vi/thủ công có xác nhận, audit, rating readiness | `Leg_CHARGEABLE` measurement | `UAT-014..019` |
| **TR-05** | `RATING-LEG-ALLOC-UAT-01` | Bill BUY -> Leg Rating -> Alloc | Cost scope/Leg/Vendor/Rate version | DIRECT: tạo Cost gắn Bill; SHARED: tạo 1 Leg Cost -> allocation details | Nguồn gốc & Bản chất kinh tế của Cost | `UAT-020..027` |
| **TR-06** | `LEG-WEIGHT-RECON-01` | Leg↔Bill Recon; Cost Allocation | CW / Gross / Driver values | Tính tương đồng ngữ nghĩa -> cảnh báo/chặn cứng -> snapshot | Dòng nguồn dữ liệu phân bổ (Provenance) | `UAT-028..035` |
| **TR-07** | `BILL-LEG-PARTICIPATION-01` | Bill Leg participation links | Phạm vi/số đo phần hàng/lineage | Quan hệ N:N, không cộng ngầm số đo ở các chặng nối tiếp | Số đo tham gia & Dòng hàng (Lineage) | `UAT-036..042` |
| **TR-08** | `RATING-LEG-UX-01` | Cảnh báo Bill Rating Buy | Thiếu CW, không khớp nấc, scope | Giải thích rõ + deep link điều hướng + không fallback giả | Dấu vết sẵn sàng tính giá (Readiness) | `UAT-043..046` |
| **TR-09** | `REGRESSION` | Rate / Bill / Finance | `RATE-UI-05/06/07`, BUY Bill, SELL, AP | Không làm ảnh hưởng hoặc hồi quy phạm vi đã bàn giao trước | Baseline chức năng hiện hữu | `UAT-047..051` |

---

## 5. CHI TIẾT TRIỂN KHAI 8 YÊU CẦU THAY ĐỔI (CR-01 ĐẾN CR-08)

1. **`CR-01` (Sửa đổi liên kết Bill N:N - `BILL-LINK-EDIT-01`):**
   - Hỗ trợ xem, thêm và gỡ liên kết nhiều Đơn hàng (Order) và Lô hàng (Shipment) với Bill.
   - Khi đơn hàng có khách hàng khác với Bill, hệ thống bảo lưu quan hệ N:N và hiển thị cảnh báo xung đột (Customer Conflict Alert).
   - Kiểm tra ràng buộc phụ thuộc (Financial Dependencies Guard): Chặn gỡ liên kết khi đã phát sinh chi phí hoặc Rating nhằm bảo toàn tính toàn vẹn dữ liệu.
2. **`CR-02` (Phiên bản hóa Bảng giá - `RATE-VERSION-UI-01`):**
   - Cho phép nhân bản phiên bản đã công bố (PUBLISHED) thành một bản nháp mới (DRAFT v2, v3...) cùng `RateCardId`.
   - Phiên bản v1 đã công bố là bất biến tuyệt đối.
   - Kiểm tra tính hợp lệ của nấc giá nửa mở `[từ, đến)` trước khi xuất bản, chặn xuất bản nếu có xung đột hoặc chồng lấn nấc giá.
3. **`CR-03` (Số đo Chặng vận chuyển - `LEG-MEASURE-UI-01`):**
   - Hỗ trợ lưu trữ bền vững Gross Weight, Volume CBM và loại hàng hóa (Commodity) ở cấp Chặng.
   - Cho phép lưu Chặng khi chưa có số đo (không bắt buộc số đo lúc tạo tham chiếu).
4. **`CR-04` (Trọng lượng tính cước Chặng - `LEG-CW-UI-01`):**
   - Khởi tạo giá trị CW là `Chưa xác định` (undetermined), không gán ngầm bằng Gross hoặc 0.
   - Hỗ trợ quy đổi CW tự động theo chính sách tính toán (Calc Policy/Version).
   - Hỗ trợ nhập CW thủ công (Manual) và can thiệp điều chỉnh (Override) bắt buộc ghi nhận lý do và nhật ký kiểm toán.
5. **`CR-05` (Phạm vi Tính giá Mua & Chi phí nguồn - `RATING-LEG-ALLOC-UAT-01`):**
   - Phân định rõ 2 phạm vi chi phí: `DIRECT_BILL_LEG` (chi phí trực tiếp gắn Bill) và `SHARED_LEG` (chi phí dùng chung ở cấp Chặng).
   - Áp dụng cơ chế Idempotency: Khi rating lại từ Bill khác trên cùng Chặng, hệ thống không sinh thêm chi phí nguồn mới, bảo đảm nguyên tắc Single Economic Cost.
6. **`CR-06` (Đối chiếu số đo Chặng - Bill - `LEG-WEIGHT-RECON-01`):**
   - Cảnh báo chênh lệch khi tổng driver < số đo nguồn (`R-08`: 90kg vs 100kg -> cảnh báo thất thoát 10%).
   - Cảnh báo khác biệt ngữ nghĩa khi driver > số đo nguồn do làm tròn (`R-09`: 120kg vs 100kg).
   - CỔNG CHẶN CỨNG: Chặn chốt phân bổ khi tổng Gross vật lý thực tế vượt quá tải trọng chặng nguồn (`BR-18, CASE-A`) hoặc khi thiếu dữ liệu dòng hàng (`UAT-042`).
   - Lọc phụ phí hàng nguy hiểm (DG Surcharge): Chỉ phân bổ cho các Bill có thuộc tính DG (`UAT-034`).
7. **`CR-07` (Hợp đồng Tham gia Chặng - `BILL-LEG-PARTICIPATION-01`):**
   - Xử lý chính xác các mô hình vận tải đa chặng: Tách rời (Disjoint Split), Nối tiếp (Sequential Legs), Hỗn hợp (Hybrid).
   - Không cộng dồn trọng lượng ở các chặng nối tiếp khi so với tổng trọng lượng của Bill (`CASE-B`, `CASE-C`).
8. **`CR-08` (Cải tiến Trải nghiệm Tính giá Mua - `RATING-LEG-UX-01`):**
   - Hiển thị rõ ràng nguyên nhân khi thiếu CW kèm đường dẫn trực tiếp sang màn hình Sửa Chặng.
   - Giải thích cặn kẽ hành vi không khớp nấc nửa mở `[1, 120)` khi CW chạm mốc 120kg.
   - Đối chiếu chính xác loại dịch vụ và phương thức (AIR vs FREIGHT), không ngầm đồng nhất.
