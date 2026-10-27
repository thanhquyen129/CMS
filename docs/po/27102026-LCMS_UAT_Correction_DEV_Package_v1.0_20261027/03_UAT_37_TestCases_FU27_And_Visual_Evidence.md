# BÁO CÁO 37 KỊCH BẢN KIỂM THỬ UAT (FU27-TC-001..037) & BẰNG CHỨNG HÌNH ẢNH
## GÓI ĐIỀU CHỈNH SAU UAT NGÀY 27/10/2026 (`LCMS-UAT-CR-20261027-01`)

---

## I. NGUYÊN TẮC QUẢN TRỊ KẾT QUẢ UAT (RED LINE #6)

> [!IMPORTANT]
> **Quy tắc bất di bất dịch về trạng thái kiểm thử**:
> 1. Toàn bộ **37 kịch bản UAT** trong tài liệu này và ma trận Excel đều có cột **`PO UAT = NOT RUN`**. DEV tuyệt đối không tự ý đánh PASS thay cho PO/BA.
> 2. Cột **`DEV Test`** ghi nhận kết quả xác minh kỹ thuật tự động của đội ngũ DEV (**`PASS` 37/37**).
> 3. Mọi kịch bản đều có test tự động tương ứng trong class `tests/LCMS.Api.Tests/UatCorrection20261027Tests.cs` và đã chạy thành công 100%.

---

## II. BẢNG CHI TIẾT 37 KỊCH BẢN KIỂM THỬ UAT

### 1. Nhóm `MASTER-MODE-01` (FU27-TC-001 → FU27-TC-005)
*Ảnh bằng chứng gốc: `E01`, `E02` | Ảnh khắc phục: `screenshots/E01_Remediation_Transport_Mode_Master_Admin.png`, `screenshots/E02_Remediation_Transport_Mode_Dropdown_Synced.png`*

| TC ID | Mức | Bước kiểm thử | Kết quả mong đợi | DEV Test | PO UAT | Bằng chứng kiểm thử tự động |
| :---: | :---: | :--- | :--- | :---: | :---: | :--- |
| **FU27-TC-001** | P1 | Đối chiếu API danh mục với màn quản trị khi chỉ AIR active. | Danh sách nguồn và dropdown nhất quán; mã hệ thống khác có mô tả nguồn/trạng thái. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_001_ModeCatalogConsistency_MatchesDropdown` |
| **FU27-TC-002** | P1 | Mở danh sách Phương thức vận chuyển. | Không có giá trị active không quản trị hoặc không có nguồn rõ ràng. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_002_ModeCatalog_NoUnmanagedOptions` |
| **FU27-TC-003** | P1 | So sánh dropdown phương thức ở Order / Bill / Shipment / Leg / RateCard. | Một canonical code, option/status nhất quán theo ngữ cảnh. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_003_ModeCanonicalAcrossEntities` |
| **FU27-TC-004** | P1 | Gửi mã phương thức không tồn tại hoặc ngừng dùng qua API. | API validation trả lỗi rõ ràng; từ chối ghi dữ liệu sai. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_004_ModeInvalid_RejectedByApi` |
| **FU27-TC-005** | P1 | Lưu AIR đọc lại dù backend từng lưu chuỗi air thường. | Hiển thị chuẩn hóa Air / AIR; canonical snapshot và trace ổn định. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_005_ModeCanonicalCaseNormalization` |

---

### 2. Nhóm `MASTER-SERVICE-01` (FU27-TC-006 → FU27-TC-010)
*Ảnh bằng chứng gốc: `E03`, `E04` | Ảnh khắc phục: `screenshots/E03_Remediation_Service_Type_Dropdown_Separation.png`, `screenshots/E04_Remediation_Legacy_Service_Warning_Banner.png`*

| TC ID | Mức | Bước kiểm thử | Kết quả mong đợi | DEV Test | PO UAT | Bằng chứng kiểm thử tự động |
| :---: | :---: | :--- | :--- | :---: | :---: | :--- |
| **FU27-TC-006** | P1 | Mở dropdown Loại dịch vụ của Bill mới. | Không cho chọn service AIR legacy như dịch vụ chuẩn; FREIGHT sẵn có. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_006_ServiceDropdown_NewBill_BlocksLegacyAir` |
| **FU27-TC-007** | P1 | Mở Bill mang service AIR lịch sử. | Hiển thị cảnh báo legacy; xem lịch sử an toàn, không bị tự sửa hoặc mất dữ liệu. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_007_ServiceLegacy_DisplaysWarning_PreservesHistory` |
| **FU27-TC-008** | P1 | Gửi service AIR trong request tạo Bill mới. | API từ chối theo chính sách dịch vụ di sản; không tự động convert thành FREIGHT. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_008_ServiceAir_OnNewBill_RejectedByApi` |
| **FU27-TC-009** | P1 | Giữ Mode = AIR, chọn Service = FREIGHT trên Bill thử nghiệm. | Các chiều độc lập, gợi ý bảng giá chuẩn xác dựa trên service code. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_009_ModeAir_ServiceFreight_IndependentSelection` |
| **FU27-TC-010** | P1 | Kiểm tra master dịch vụ có cùng chuẩn xuyên suốt các đối tượng. | Không hardcode riêng lẻ; không làm mất tham chiếu pricing lịch sử. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_010_ServiceMasterConsistencyAcrossObjects` |

---

### 3. Nhóm `BILL-SERVICE-DISPLAY-01` (FU27-TC-011 → FU27-TC-015)
*Ảnh bằng chứng gốc: `E03`, `E05` | Ảnh khắc phục: `screenshots/E05_Remediation_Bill_Overview_Freight_Synchronized.png`*

| TC ID | Mức | Bước kiểm thử | Kết quả mong đợi | DEV Test | PO UAT | Bằng chứng kiểm thử tự động |
| :---: | :---: | :--- | :--- | :---: | :---: | :--- |
| **FU27-TC-011** | P0 | Chọn FREIGHT, bấm Lưu, đóng và mở lại Bill TESTLSS002. | Cả Sửa Bill và Tổng quan đều hiển thị FREIGHT, không hiện AIR. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_011_BillService_EditAndOverviewSynchronized` |
| **FU27-TC-012** | P0 | So sánh GET Bill với dữ liệu DB và DTO trên hai màn hình. | Cùng serviceTypeCode canonical; mapping không lấy billType hoặc mode thay thế. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_012_BillDto_PersistedServiceTypeCodeMapping` |
| **FU27-TC-013** | P0 | Chọn FREIGHT trên form nhưng chưa lưu rồi rời màn hình. | Có cảnh báo unsaved changes; Tổng quan giữ nguyên giá trị persisted. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_013_UnsavedFormState_DoesNotCorruptPersistedView` |
| **FU27-TC-014** | P0 | Sau khi lưu service FREIGHT, mở màn hình Tính giá Rating. | Rating Context đọc cùng service persisted với Tổng quan và Sửa Bill. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_014_RatingContext_ReadsPersistedServiceTypeCode` |
| **FU27-TC-015** | P0 | Bill có service được sở hữu bởi API/import ngoài. | Không tự ý ghi đè ngầm (silent overwrite); hiển thị rõ quyền chỉnh sửa. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_015_BillServiceOwnership_NoSilentOverwrite` |

---

### 4. Nhóm `RATING-STALE-NORMALIZE-01` (FU27-TC-016 → FU27-TC-020)
*Ảnh bằng chứng gốc: `E05`, `E06` | Ảnh khắc phục: `screenshots/E06_Remediation_Rating_History_No_False_Stale.png`*

| TC ID | Mức | Bước kiểm thử | Kết quả mong đợi | DEV Test | PO UAT | Bằng chứng kiểm thử tự động |
| :---: | :---: | :--- | :--- | :---: | :---: | :--- |
| **FU27-TC-016** | P0 | Đổi AIR → air khi cùng phương thức vận chuyển canonical. | Không đánh dấu Rating stale do riêng thay đổi chữ hoa/thường. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_016_RatingStale_CaseInsensitive_AirVersusAir` |
| **FU27-TC-017** | P0 | Đổi AIR → SEA có ý nghĩa thay đổi nghiệp vụ vận tải. | Đánh dấu stale đúng lý do thay đổi phương thức và lưu audit log. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_017_RatingStale_TrueModeChange_TriggersStale` |
| **FU27-TC-018** | P0 | Đổi Loại dịch vụ từ AIR → FREIGHT có xác nhận của người dùng. | Stale nếu Context rating thực sự đổi; lý do không ghi sai thành mode. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_018_RatingStale_TrueServiceChange_TriggersStale` |
| **FU27-TC-019** | P0 | Cùng lúc thay đổi Loại dịch vụ và Trọng lượng (CW). | Hiển thị chính xác tất cả lý do stale liên quan; không bỏ sót. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_019_RatingStale_MultipleChanges_AllReported` |
| **FU27-TC-020** | P0 | Xem Rating và Cost đã seed sau khi fix chuẩn hóa mode. | Snapshot cũ, 2 dòng Expected Cost và số tiền giữ nguyên; không tự rerate. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_020_RatingSnapshotAndCosts_ImmutableAfterFix` |

---

### 5. Nhóm `COST-RATING-SOURCE-01` (FU27-TC-021 → FU27-TC-026)
*Ảnh bằng chứng gốc: `E07`, `E08` | Ảnh khắc phục: `screenshots/E07_Remediation_Cost_Freight_Base_Rate_Trace.png`*

| TC ID | Mức | Bước kiểm thử | Kết quả mong đợi | DEV Test | PO UAT | Bằng chứng kiểm thử tự động |
| :---: | :---: | :--- | :--- | :---: | :---: | :--- |
| **FU27-TC-021** | P0 | Mở chi tiết cước FREIGHT 1.440.000 ₫ từ Bill TESTLSS002. | Nguồn là Bảng giá cước chính: TDNT_SGN_HAN v2 / QT0001, CW 120 kg, 12.000 ₫/kg. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_021_FreightCost_TracesToBaseRate` |
| **FU27-TC-022** | P0 | Mở chi tiết phụ phí DONGGOI 300.000 ₫. | Nguồn là Biểu phụ phí độc lập DONGGOI_SGN_HAN_MUA v2; không nhầm sang base. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_022_DonggoiCost_TracesToIndependentSurcharge` |
| **FU27-TC-023** | P0 | So sánh rating_detail_id liên kết từ 2 cost cùng rating_id. | Mỗi Cost liên kết đúng detail riêng; không nhầm lẫn do chung rating ID cha. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_023_DistinctRatingDetailFkPerCost` |
| **FU27-TC-024** | P0 | Phát hành bảng giá mới (v3) rồi xem lại Cost cũ. | Nguồn v2 và các số liệu snapshot bất biến; không tự suy ra bản mới. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_024_PublishedNewRateVersion_PreservesHistoricalTrace` |
| **FU27-TC-025** | P0 | Xem cost seed lỗi legacy không đủ FK hoặc snapshot. | Thông báo trạng thái thiếu nguồn (missing link); không bịa đặt nguồn. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_025_LegacyMissingLink_DisplaysExplicitGap` |
| **FU27-TC-026** | P0 | Đối chiếu báo cáo tài chính sau khi fix trace. | Không đổi Net / VAT / Gross, maturity, kết quả profitability. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_026_CostFinancialTotals_PreservedAfterTraceFix` |

---

### 6. Nhóm `SURCHARGE-FORMULA-TRACE-01` (FU27-TC-027 → FU27-TC-031)
*Ảnh bằng chứng gốc: `E08` | Ảnh khắc phục: `screenshots/E08_Remediation_Cost_Donggoi_Fixed_Surcharge_Trace.png`*

| TC ID | Mức | Bước kiểm thử | Kết quả mong đợi | DEV Test | PO UAT | Bằng chứng kiểm thử tự động |
| :---: | :---: | :--- | :--- | :---: | :---: | :--- |
| **FU27-TC-027** | P1 | Mở phụ phí DONGGOI v2 snapshot fixed 300.000 ₫. | Thể hiện Cố định (FIXED) 300.000 ₫/khoản; không gọi 2.500 ₫/kg là đơn giá gốc. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_027_DonggoiFixed_DisplaysFixed300k` |
| **FU27-TC-028** | P1 | Nếu có đơn giá 300.000 / 120 = 2.500 ₫/kg. | Gắn nhãn minh bạch "Đơn giá quy đổi tham khảo", công thức nguồn rõ ràng. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_028_DerivedReferenceUnitRate_LabeledExplicitly` |
| **FU27-TC-029** | P1 | Tính trên Bill thử CW khác nhưng phụ phí giữ nguyên điều kiện. | FIXED vẫn giữ nguyên 300.000 ₫ không phụ thuộc CW. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_029_FixedSurcharge_DifferentCw_PreservesAmount` |
| **FU27-TC-030** | P1 | Kiểm thử phụ phí UNIT_RATE theo kg thực tế. | Hiển thị đúng đơn vị tính kg, công thức Số lượng × Đơn giá hợp đồng. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_030_UnitRateSurcharge_CalculatesQuantityTimesRate` |
| **FU27-TC-031** | P1 | So sánh phụ phí DONGGOI sau khi điều chỉnh giao diện. | Net 300.000 ₫, VAT 24.000 ₫, Gross 324.000 ₫; nguồn không thay đổi. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_031_DonggoiAmounts_NetVatGross_Preserved` |

---

### 7. Nhóm `REGRESSION` (FU27-TC-032 → FU27-TC-037)
*Bảo vệ toàn diện các ranh giới tài chính và kinh tế đơn hàng*

| TC ID | Mức | Bước kiểm thử | Kết quả mong đợi | DEV Test | PO UAT | Bằng chứng kiểm thử tự động |
| :---: | :---: | :--- | :--- | :---: | :---: | :--- |
| **FU27-TC-032** | P0 | Mở 1 Rating BUY và 1 Rating SELL chưa stale. | BUY chỉ Seed Expected Cost, SELL chỉ Seed Expected Revenue; API chặn trái chiều. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_032_BuyOnlySeedsCost_SellOnlySeedsRevenue` |
| **FU27-TC-033** | P0 | Bấm Seed lại trên Rating BUY đã seed của Bill TESTLSS002. | Tính idempotent: Không thêm dòng chi phí thứ 3, tổng giữ 1.740.000 ₫. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_033_SeedExpectedCost_Idempotency` |
| **FU27-TC-034** | P0 | Tính theo đúng ngày hiệu lực as-of và CW của v1/v2. | v1 giữ nguyên, v2 áp dụng đúng ngày 26/10/2026; không sửa Rating lịch sử. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_034_RateVersion_HistoricalAsOfDate_Preserved` |
| **FU27-TC-035** | P0 | Kiểm tra tổng hợp tài chính Bill TESTLSS002 sau fix. | Net 1.740.000 ₫, VAT 139.200 ₫, Gross 1.879.200 ₫; maturity giữ nguyên. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_035_FinancialIntegrity_NetVatGrossPreserved` |
| **FU27-TC-036** | P0 | Kiểm tra Shared Cost phân bổ quan hệ N:N. | Không tạo chi phí kinh tế trùng; tổng phân bổ bằng đúng chi phí nguồn. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_036_SharedCostAllocation_SingleEconomicCost` |
| **FU27-TC-037** | P1 | Kiểm tra định dạng ngày và thông số as-of. | Định dạng chuẩn DD/MM/YYYY; truy vết nguồn giá không bị sai lệch. | **PASS** | **NOT RUN** | `UatCorrection20261027Tests.FU27_TC_037_DateFormattingAndAsOf_UnchangedTrace` |

---

## III. MA TRẬN ĐỐI CHIẾU ẢNH BẰNG CHỨNG (E01..E08 vs REMEDIATION SCREENSHOTS)
> [!NOTE]
> Toàn bộ 08 ảnh nghiệm thu bên dưới được chụp trực tiếp từ ứng dụng LCMS đang chạy thực tế (Next.js AppShell với đầy đủ Sidebar điều hướng, Topbar tìm kiếm, .NET Backend và CSDL thực `lcms_dev.db`). Mọi mockup/card tĩnh trước đây đã được thay thế 100% bằng ảnh chụp live application.

| Mã gốc | Vấn đề phát hiện trong ảnh gốc (PO UAT) | Ảnh nghiệm thu thực tế của DEV (Live AppShell) | Nội dung chứng minh khắc phục thành công |
| :---: | :--- | :--- | :--- |
| **E01** | Quản trị Danh mục Phương thức chỉ hiển thị duy nhất `AIR`. | `screenshots/E01_Remediation_Transport_Mode_Master_Admin.png` | Màn hình quản trị Danh mục Phương thức vận chuyển (`/admin/catalog?kind=transport_mode`) hiển thị đúng 01 bản ghi active `air | Đường hàng không | — | Đang dùng`. Tuân thủ Red Line #3: không tự ý silent-seed mode ngoài danh mục. |
| **E02** | Sửa Bill có dropdown mode nhiều lựa chọn hardcode (`express`). | `screenshots/E02_Remediation_Transport_Mode_Dropdown_Synced.png` | Dropdown Phương thức trên form Sửa Bill `TESTLSS002` đồng bộ 100% từ danh mục active ở `E01` (chỉ hiển thị `air`), loại bỏ toàn bộ các option hardcode tĩnh ngoài danh mục. |
| **E03** | Dropdown dịch vụ trên Sửa Bill hiển thị cả `FREIGHT` và `AIR`. | `screenshots/E03_Remediation_Service_Type_Dropdown_Separation.png` | Loại dịch vụ tách biệt hoàn toàn với phương thức; mã `AIR` đã bị loại bỏ khỏi danh sách dịch vụ chuẩn trên giao dịch mới, chỉ hiển thị `FREIGHT — Vận tải hàng`... |
| **E04** | Chọn `AIR` trên Bill mới có cảnh báo nhưng người dùng vẫn lưu được. | `screenshots/E04_Remediation_Legacy_Service_Warning_Banner.png` | Bill mới chặn chọn `AIR` (API reject); Bill lịch sử (`BILL-LEGACY-009`) hiển thị banner cảnh báo di sản an toàn kèm hướng dẫn chuyển đổi sang `FREIGHT`. |
| **E05** | Sửa Bill chọn `FREIGHT`, sau reload Tổng quan vẫn hiển thị `AIR`. | `screenshots/E05_Remediation_Bill_Overview_Freight_Synchronized.png` | Bổ sung `ServiceTypeCode` vào `BillDto`; Tổng quan Bill `TESTLSS002` hiển thị đúng Dịch vụ `FREIGHT`, Phương thức `air`, và sạch bóng cảnh báo stale giả. |
| **E06** | Lịch sử Rating báo stale giả: *"Phương thức đổi AIR → air"*. | `screenshots/E06_Remediation_Rating_History_No_False_Stale.png` | So sánh case-insensitive (`OrdinalIgnoreCase`); xóa sạch stale giả; Rating Bill `TESTLSS002` giữ trạng thái Hợp lệ/Hoàn thành (Net `1.740.000 ₫`, Gross `1.879.200 ₫`, CW `120 kg`). |
| **E07** | Cost `FREIGHT` (1.440.000 ₫) truy vết sai thành *"Phụ phí từ Rating"*. | `screenshots/E07_Remediation_Cost_Freight_Base_Rate_Trace.png` | Truy vết định danh 1-1 theo `BR27-13` (`cost.source_id -> rating_detail.id`); hiển thị chuẩn xác "Cước chính từ Rating (Base Rate)", Bảng giá `TDNT_SGN_HAN` v2 / `QT0001`, CW `120 kg` × `12.000 ₫/kg`, Net `1.440.000 ₫`, VAT 8% `115.200 ₫`, Gross `1.555.200 ₫`. |
| **E08** | Cost `DONGGOI` hiển thị cách tính FIXED kèm nhầm nhãn đơn giá 2.500 ₫/kg. | `screenshots/E08_Remediation_Cost_Donggoi_Fixed_Surcharge_Trace.png` | Truy vết định danh 1-1 theo `BR27-13`; hiển thị chuẩn xác "Phụ phí từ Rating", Biểu phụ phí `DONGGOI_SGN_HAN_MUA` v2, công thức `Phí cố định (FIXED): 300.000 ₫ / khoản`, đơn giá quy đổi tham khảo `2.500 ₫/kg`, Net `300.000 ₫`, VAT 8% `24.000 ₫`, Gross `324.000 ₫`. |

