# TRUY VẾT QUY TẮC NGHIỆP VỤ (BR), TIÊU CHÍ NGHIỆM THU (AC) & KẾT QUẢ KIỂM THỬ (UAT)
**Mã Issue Delta:** `LCMS-UAT-SUR-OPTIONAL-SERVICE-CODE-01` | **Gói BA cha:** `LCMS-CR-SURCHARGE-APPLICATION-01`  
**Phiên bản:** `v1.0 (04/11/2026)` | **Mục tiêu chất lượng:** `100/100 Điểm — Commercial-Ready Product`

---

## 1. MA TRẬN ĐỐI CHIẾU 12 QUY TẮC NGHIỆP VỤ DELTA (`BR-DELTA-01` → `BR-DELTA-12`)

| Mã BR | Ưu tiên | Quy tắc nghiệp vụ bắt buộc | Phạm vi | Kỳ vọng kiểm chứng | Trạng thái DEV | Bằng chứng kiểm chứng DEV |
| :---: | :---: | :--- | :--- | :--- | :---: | :--- |
| **`BR-DELTA-01`** | **P0** | Khi `activation_policy = OPTIONAL_OPT_IN`, phiên bản phụ phí bắt buộc phải có `service_code` hợp lệ liên kết dịch vụ bổ sung đã được quản trị. | Surcharge version / master | Chặn publish khi thiếu hoặc sai `service_code`. | **ĐÃ HOÀN TẤT & PASS** | `UAT_SUR_SVC_01`, `UAT_SUR_SVC_03`, `UAT_SUR_SVC_04` PASS 100%. |
| **`BR-DELTA-02`** | **P0** | Form tạo/sửa nháp hiển thị trường **“Dịch vụ kích hoạt *”** dạng dropdown lookup danh mục chuẩn `active` của Tenant, không bắt gõ UUID tự do. | Quản lý phụ phí / FE | Dropdown hiển thị tên + mã, lọc theo Tenant và trạng thái `active`. | **ĐÃ HOÀN TẤT & PASS** | `SurchargeForm.tsx` combobox; `UAT_SUR_SVC_01` PASS; Screenshot 01. |
| **`BR-DELTA-03`** | **P0** | Cấm đánh đồng `service_code` với loại dịch vụ vận tải `FREIGHT` hay mã khoản mục kinh tế `DONGGO`. Dùng phân hệ `MasterCatalogItem` riêng. | Dữ liệu danh mục | Định nghĩa `MasterCatalogKinds.OptionalService` độc lập. | **ĐÃ HOÀN TẤT & PASS** | `OptionalServiceCatalogSeeder.cs`; `UAT_SUR_SVC_01` PASS. |
| **`BR-DELTA-04`** | **P0** | Tách biệt Lưu nháp (cho phép lưu kèm warning) và Phát hành (chặn triệt để khi thiếu `service_code`, trả HTTP 400 và field error). | Surcharge draft / publish | Validation field-level; API trả mã lỗi rõ ràng; F5 giữ nguyên dữ liệu. | **ĐÃ HOÀN TẤT & PASS** | `UAT_SUR_SVC_02`, `UAT_SUR_SVC_03`, `UAT_SUR_SVC_05` PASS; Screenshot 02. |
| **`BR-DELTA-05`** | **P0** | Lưu lựa chọn dịch vụ tại Bill/Shipment/Leg bền vững (`scope`, `qty`, `revision`). Không tự động tích chọn sẵn; chỉ kích hoạt khi đã xác nhận lưu. | Service selections | Ghi vào bảng `service_selections`; không default checked; reload giữ nguyên. | **ĐÃ HOÀN TẤT & PASS** | `UAT_SUR_SVC_07`, `UAT_SUR_SVC_09`, `UAT_SUR_SVC_12` PASS; Screenshot 04. |
| **`BR-DELTA-06`** | **P0** | Tính giá BUY và SELL độc lập hoàn toàn theo đúng phiên bản phụ phí và direction. Không tự copy giá BUY sang SELL. | Rating Engine | Rating SELL không copy 300.000 VND từ phụ phí BUY. | **ĐÃ HOÀN TẤT & PASS** | `UAT_SUR_SVC_11` PASS; direction filter kiểm tra nghiêm ngặt. |
| **`BR-DELTA-07`** | **P0** | Rating đọc persisted selection và phân nhóm rõ ràng (Applied, EligibleNotActivated, Excluded) kèm lý do chuẩn. | Rating UI / API | Trace đầy đủ nguồn version, rule và lý do `NOT_SELECTED`. | **ĐÃ HOÀN TẤT & PASS** | `UAT_SUR_SVC_08`, `UAT_SUR_SVC_10` PASS; trace v3 chuẩn xác. |
| **`BR-DELTA-08`** | **P0** | `FIXED_RATE` tính theo `basis` và `quantity` cấu hình (`per_bill` vs `per_unit`); không tự nhân trọng lượng tính cước 120 kg. | Surcharge calculation | Số tiền tính đúng phạm vi (ví dụ: `per_unit` qty 2 = 600.000đ; `per_bill` = 300.000đ). | **ĐÃ HOÀN TẤT & PASS** | `UAT_SUR_SVC_15` PASS 100%. |
| **`BR-DELTA-09`** | **P0** | Tuyệt đối không sửa trực tiếp dữ liệu của `Published v1/v2` hay Rating lịch sử cũ; phát hành phiên bản mới an toàn. | Versioning & Audit | `v2` giữ nguyên `UNCLASSIFIED_LEGACY`; snapshot bất biến. | **ĐÃ HOÀN TẤT & PASS** | `UAT_SUR_SVC_06`, `UAT_SUR_SVC_17` PASS; Screenshot 03. |
| **`BR-DELTA-10`** | **P0** | Tính thử không tự động sinh Expected Cost/Revenue; Thao tác Apply đảm bảo `idempotent` theo định danh kinh tế. | Rating Financials | Bấm Apply 2 lần hoặc sau F5 không sinh trùng dòng chi phí. | **ĐÃ HOÀN TẤT & PASS** | `UAT_SUR_SVC_08`, `UAT_SUR_SVC_13` PASS 100%. |
| **`BR-DELTA-11`** | **P1** | Đổi/bỏ chọn dịch vụ sau khi Apply phải tạo snapshot mới khi tính lại; bảo vệ tuyệt đối chi phí Confirmed/Actual. | Bill / Maturity | Không ghi đè ngầm; lưu vết lịch sử rating đầy đủ. | **ĐÃ HOÀN TẤT & PASS** | `UAT_SUR_SVC_14` PASS 100%. |
| **`BR-DELTA-12`** | **P0** | Trả mã lỗi API chuẩn (`REQUIRED_SERVICE_CODE`, `field: service_code`) và hiển thị lỗi ngay dưới trường trên UI. | Backend / BFF / FE | Không chỉ báo banner chung, không trả HTTP 500. | **ĐÃ HOÀN TẤT & PASS** | `UAT_SUR_SVC_03`, `UAT_SUR_SVC_04` PASS; Screenshot 02. |

---

## 2. BẢNG TIÊU CHÍ NGHIỆM THU (`AC-01` → `AC-11`)

*Ghi chú: Theo đúng nguyên tắc bàn giao, cột `Trạng thái PO` được bảo lưu nguyên vẹn giá trị **`Chưa kiểm thử`** để PO trực tiếp đánh giá và nghiệm thu.*

| Mã AC | Ưu tiên | Điều kiện nghiệm thu chi tiết | Trạng thái PO | Bằng chứng kiểm chứng DEV (DEV Evidence) |
| :---: | :---: | :--- | :---: | :--- |
| **`AC-01`** | **P0** | Chọn `OPTIONAL_OPT_IN` hiển thị trường Dịch vụ kích hoạt dạng lookup danh mục chuẩn `active` của đúng Tenant. | Chưa kiểm thử | PASS: `SurchargeForm.tsx` kết nối `/bff/surcharges/activation-services`; `UAT_SUR_SVC_01` PASS; Screenshot 01. |
| **`AC-02`** | **P0** | Lưu nháp thành công khi chưa có `service_code` kèm cảnh báo inline; reload F5 giữ nguyên toàn bộ dữ liệu đã lưu. | Chưa kiểm thử | PASS: API trả `warnings` chi tiết; `UAT_SUR_SVC_02`, `UAT_SUR_SVC_05` PASS. |
| **`AC-03`** | **P0** | Phát hành thiếu, sai hoặc inactive `service_code` bị chặn chính xác với HTTP 400 và lỗi mức trường; không trả 500. | Chưa kiểm thử | PASS: API trả `REQUIRED_SERVICE_CODE`, `UNKNOWN_SERVICE`, `INACTIVE_SERVICE` gắn `field: "service_code"`; `UAT_SUR_SVC_03`, `UAT_SUR_SVC_04` PASS; Screenshot 02. |
| **`AC-04`** | **P0** | Phát hành `v3` hợp lệ thành công; phiên bản cũ `v1/v2` vẫn giữ nguyên trạng thái `UNCLASSIFIED_LEGACY` và chỉ đọc. | Chưa kiểm thử | PASS: `v3` chuyển `published`, có `published_at`; `v2` bất biến; `UAT_SUR_SVC_06`, `UAT_SUR_SVC_17` PASS; Screenshot 03. |
| **`AC-05`** | **P0** | Dịch vụ `active` xuất hiện đồng bộ ở khu vực Dịch vụ bổ sung trên màn hình Bill và Workspace Tính giá; dữ liệu nhất quán. | Chưa kiểm thử | PASS: Cùng đọc/ghi vào bảng `service_selections` theo `tenant_id + context_id + service_code`; `UAT_SUR_SVC_05`, `UAT_SUR_SVC_09` PASS. |
| **`AC-06`** | **P0** | Không chọn dịch vụ: Rating không cộng tiền; phụ phí đưa vào `EligibleNotActivated` với lý do `NOT_SELECTED`; không tự seed chi phí. | Chưa kiểm thử | PASS: Rating BUY chỉ tính cước chính FREIGHT; `DONGGOI` excluded/not activated; `UAT_SUR_SVC_07`, `UAT_SUR_SVC_08` PASS; Screenshot 04. |
| **`AC-07`** | **P0** | Chọn và lưu dịch vụ: F5 giữ nguyên; Rating BUY tính đúng 300.000 VND theo cấu hình `basis` và snapshot `v3`. | Chưa kiểm thử | PASS: Lưu `service_selections` bền vững; Rating tính chính xác `300.000 VND`; `UAT_SUR_SVC_09`, `UAT_SUR_SVC_10`, `UAT_SUR_SVC_15` PASS; Screenshot 05. |
| **`AC-08`** | **P0** | Rating SELL không tự ý copy đơn giá phụ phí BUY; chỉ kích hoạt khi có phiên bản phụ phí SELL Published riêng biệt. | Chưa kiểm thử | PASS: SELL rating độc lập 100%, trả `EXCLUDED_WRONG_DIRECTION` cho phụ phí BUY; `UAT_SUR_SVC_11` PASS. |
| **`AC-09`** | **P0** | Bỏ chọn hoặc đổi số lượng và Apply lại không sinh trùng dòng chi phí; bảo vệ tuyệt đối các khoản `Confirmed / Actual`. | Chưa kiểm thử | PASS: Apply 2 lần hoàn toàn `idempotent`; rerate tạo snapshot mới bảo toàn dữ liệu cũ; `UAT_SUR_SVC_13`, `UAT_SUR_SVC_14` PASS. |
| **`AC-10`** | **P0** | Kiểm soát đa thuê bao (Tenant isolation), phân quyền và phạm vi ngữ cảnh (`context scope`) chặt chẽ cả trên API lẫn UI. | Chưa kiểm thử | PASS: Yêu cầu cross-tenant hoặc người dùng không có quyền bị từ chối 403/404 ngay lập tức; `UAT_SUR_SVC_12`, `UAT_SUR_SVC_16` PASS. |
| **`AC-11`** | **P0** | Đầy đủ commit, log kiểm thử tự động, 5 screenshots app thực tế; workbook bảo lưu cột PO UAT để PO nghiệm thu độc lập. | Chưa kiểm thử | PASS: 18/18 UAT tests PASS; 33/33 parent tests PASS; 5 ảnh chụp thực tế; workbook cập nhật chuẩn; `UAT_SUR_SVC_01..18` PASS. |

---

## 3. BẢNG CHI TIẾT 18 KỊCH BẢN KIỂM THỬ TỰ ĐỘNG (`UAT-SUR-SVC-01..18`)

Toàn bộ 18 test cases được hiện thực tự động hóa 100% tại `tests/LCMS.Api.Tests/SurchargeOptionalServiceCodeTests.cs`:

| Mã TC | Phân nhóm | Thao tác & Dữ liệu đầu vào | Kết quả mong đợi | Phương thức kiểm thử tự động | Kết quả DEV | Thời gian |
| :---: | :---: | :--- | :--- | :--- | :---: | :---: |
| **`UAT-SUR-SVC-01`** | Cấu hình | Gọi API lấy danh mục dịch vụ kích hoạt `activeOnly=true` và `activeOnly=false`. | Trả về `DONGGOI`, `KIEMDEM`, `NANGHA` (`active`); ẩn `BAOHIEM` (`inactive`). | `UAT_SUR_SVC_01_LookupActivationServices_ReturnsTenantActiveServices` | **PASS** | 216 ms |
| **`UAT-SUR-SVC-02`** | Validation | Tạo/Sửa bản nháp `v3` với `OPTIONAL_OPT_IN`, chưa khai `service_code`, bấm Lưu nháp. | Lưu thành công trạng thái `draft`; trả `warnings` thiếu `service_code`. | `UAT_SUR_SVC_02_DraftWithoutServiceCode_AllowedWithInlineWarning` | **PASS** | 312 ms |
| **`UAT-SUR-SVC-03`** | Validation | Bản nháp `v3` chưa có `service_code`, bấm Phát hành (`Publish`). | Chặn HTTP 400 Bad Request; trả `code: REQUIRED_SERVICE_CODE`, `field: "service_code"`. Không 500. | `UAT_SUR_SVC_03_PublishWithoutServiceCode_RejectedInlineErrorContract` | **PASS** | 185 ms |
| **`UAT-SUR-SVC-04`** | Validation | Phát hành với `service_code` ngoài danh mục (`UNKNOWN`) hoặc đang vô hiệu (`BAOHIEM`). | Chặn HTTP 400 với mã lỗi chuẩn `UNKNOWN_SERVICE` và `INACTIVE_SERVICE`. | `UAT_SUR_SVC_04_PublishWithInvalidServiceCode_UnknownOrInactiveOrWrongTenant_Rejected` | **PASS** | 240 ms |
| **`UAT-SUR-SVC-05`** | Versioning | Chọn dịch vụ hợp lệ `DONGGOI`, scope `BILL`, Lưu nháp `v3`, sau đó tải lại (F5 / GET). | `serviceCode`, `selectionScope`, `serviceDisplayName` được lưu giữ nguyên vẹn. | `UAT_SUR_SVC_05_SaveDraftWithValidServiceCode_PreservedOnReload` | **PASS** | 152 ms |
| **`UAT-SUR-SVC-06`** | Publish | Phát hành bản nháp `v3` hợp lệ với `service_code = DONGGOI`. | `v3` chuyển `published`, có `publishedAt`; `v2` giữ nguyên `UNCLASSIFIED_LEGACY`. | `UAT_SUR_SVC_06_PublishValidV3_PublishedStatusAndTimestampSet_V2Immutable` | **PASS** | 198 ms |
| **`UAT-SUR-SVC-07`** | Bill Data | Tạo Bill mới SGN-HAN CW 120 kg, chưa chọn dịch vụ đóng gói. | Không mặc định tích chọn bất kỳ dịch vụ nào; phụ phí tùy chọn chưa kích hoạt. | `UAT_SUR_SVC_07_CreateBill_NoDefaultServiceSelected` | **PASS** | 145 ms |
| **`UAT-SUR-SVC-08`** | Rating BUY | Tính thử BUY khi chưa chọn đóng gói trên Bill. | FREIGHT hợp lệ; DONGGOI vào `EligibleNotActivated` với lý do `NOT_SELECTED`; không tự seed. | `UAT_SUR_SVC_08_TryRatingBuy_PackagingNotSelected_FreightValid_PackagingExcludedOrEligibleNotActivated` | **PASS** | 12 ms |
| **`UAT-SUR-SVC-09`** | Selection | Chọn Đóng gói (`DONGGOI`), số lượng 1, Lưu lựa chọn, sau đó tải lại (F5). | Lựa chọn xuất hiện đồng nhất tại cả màn hình Bill và Tính giá; trạng thái lưu bền vững. | `UAT_SUR_SVC_09_SelectPackagingService_PersistedAndSharedBetweenViews` | **PASS** | 165 ms |
| **`UAT-SUR-SVC-10`** | Rating BUY | Tính thử BUY sau khi chọn và lưu Đóng gói (qty = 1). | DONGGOI được cộng đúng `300.000 VND`; nguồn trace về `v3` và `service_code = DONGGOI`. | `UAT_SUR_SVC_10_TryRatingBuy_PackagingSelected_Adds300kAndTracesToV3` | **PASS** | 8 ms |
| **`UAT-SUR-SVC-11`** | Direction | Tính thử SELL khi chỉ có phụ phí BUY cho `DONGGOI`. | Không copy giá BUY sang SELL; DONGGOI bị loại trừ `EXCLUDED_WRONG_DIRECTION`. | `UAT_SUR_SVC_11_TryRatingSell_WhenOnlyBuySurchargeExists_DoesNotCopyBuyPrice` | **PASS** | 6 ms |
| **`UAT-SUR-SVC-12`** | Scope | Tích chọn dịch vụ Đóng gói ở Bill A, sau đó thực hiện tính giá Bill B. | Lựa chọn của Bill A không bị rò rỉ sang Bill B; Bill B không kích hoạt DONGGOI. | `UAT_SUR_SVC_12_SelectionIsolation_BillADoesNotLeakToBillB` | **PASS** | 195 ms |
| **`UAT-SUR-SVC-13`** | Apply | Áp dụng chi phí BUY hợp lệ 2 lần liên tiếp hoặc sau F5. | Idempotent: không sinh trùng dòng chi phí kinh tế; các khoản Confirmed/Actual không bị sửa. | `UAT_SUR_SVC_13_ApplyBuyFinancials_Idempotent_DoesNotDuplicateCostOrMutateConfirmedActual` | **PASS** | 890 ms |
| **`UAT-SUR-SVC-14`** | Change | Bỏ chọn Đóng gói sau khi đã Apply, sau đó thực hiện tính giá lại (rerate). | Kết quả tính mới loại bỏ DONGGOI; lịch sử lần tính trước và chi phí đã ghi nhận được bảo vệ. | `UAT_SUR_SVC_14_DeselectPackaging_RerateCreatesNewRatingAndPreservesHistory` | **PASS** | 205 ms |
| **`UAT-SUR-SVC-15`** | Basis Calc | Tính `FIXED_RATE` với `basis = per_unit` (qty = 2) vs `basis = per_bill` (CW = 120 kg). | `per_unit` ra 600.000đ; `per_bill` ra 300.000đ (tuyệt đối không nhân cước theo 120 kg = 36tr). | `UAT_SUR_SVC_15_FixedRateBasisCalculation_PerUnitMultipliesQty_PerBillDoesNotMultiplyWeight` | **PASS** | 5 ms |
| **`UAT-SUR-SVC-16`** | Access | Người dùng Tenant B gọi API phát hành phụ phí của Tenant A. | Bị chặn với HTTP 403/404; trạng thái phụ phí Tenant A không đổi. | `UAT_SUR_SVC_16_Security_UnauthorizedOrCrossTenantPublishRejected` | **PASS** | 210 ms |
| **`UAT-SUR-SVC-17`** | Audit | Kiểm tra chi tiết phiên bản `v2`, `v3` và Rating lịch sử. | `v2` giữ nguyên `UNCLASSIFIED_LEGACY`; `v3` chỉ đọc sau khi publish; rating cũ bất biến. | `UAT_SUR_SVC_17_AuditAndImmutability_V2Immutable_V3ReadOnlyAfterPublish_HistoricalRatingIntact` | **PASS** | 195 ms |
| **`UAT-SUR-SVC-18`** | Regression | Bill kết hợp 3 loại: Mandatory (Fuel), Auto-conditional (DG=false), Optional (Packaging=true). | Fuel (100k) và Packaging (300k) được áp dụng; DG bị từ chối do điều kiện sai; không cộng trùng. | `UAT_SUR_SVC_18_Regression_MandatoryAndAutoConditionalSurchargesFunctionProperly` | **PASS** | 10 ms |

---

## 4. BẢNG LOG THỰC THI KIỂM THỬ THỰC TẾ (CONSOLE TEST LOGS)

### 4.1. Kết quả kiểm thử bộ test suite mới `SurchargeOptionalServiceCodeTests`
```text
Test run for C:\A1\git\cms\tests\LCMS.Api.Tests\bin\Debug\net8.0\LCMS.Api.Tests.dll (.NETCoreApp,Version=v8.0)
VSTest version 17.14.1 (x64)

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    18, Skipped:     0, Total:    18, Duration: 10 s - LCMS.Api.Tests.dll (net8.0)
```

### 4.2. Kết quả kiểm thử hồi quy gói cha `SurchargeApplicationP0Tests` (33 Test Cases)
```text
Test run for C:\A1\git\cms\tests\LCMS.Api.Tests\bin\Debug\net8.0\LCMS.Api.Tests.dll (.NETCoreApp,Version=v8.0)
VSTest version 17.14.1 (x64)

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    33, Skipped:     0, Total:    33, Duration: 11 s - LCMS.Api.Tests.dll (net8.0)
```

### 4.3. Kết quả kiểm thử giao diện Frontend (`apps/web`)
```text
> lcms-web@0.1.0 test
> vitest run

 RUN  v3.2.4 C:/A1/git/cms/apps/web

 ✓ lib/input-optimization-inheritance.test.ts (8 tests) 8ms
 ✓ lib/rate-field-policy.test.ts (4 tests) 5ms
 ✓ components/BillRelationsPanel.test.tsx (4 tests) 152ms
 ✓ components/BillFinancialViewPanel.test.tsx (4 tests) 210ms
 ✓ components/CostAllocationReconciliationPanel.test.tsx (6 tests) 253ms
 ✓ components/CreateRateVersionButton.test.tsx (3 tests) 304ms
 ✓ components/RateCardDeclarationForm.test.tsx (18 tests) 8347ms

 Test Files  7 passed (7)
      Tests  47 passed (47)
   Start at  12:30:04
   Duration  12.32s
```

**Tổng kết:** Toàn bộ 51 test cases backend và 47 test cases frontend đạt tỷ lệ **100% PASS**, không có bất kỳ test case nào bị bỏ qua hoặc thất bại.

