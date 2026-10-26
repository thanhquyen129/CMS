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
   - Truy vấn thông tin snapshot từ `rating_details`, `ratings`, `rate_cards`, `rate_versions` và `pricing_rules`. Phân biệt rõ ràng giữa cước chính (`PricingRuleId`) và phụ phí độc lập (`SurchargeVersionId`, `IsSurcharge = true`). Trả về `sourceOrigin = "manual"` nếu không có liên kết rating.

### 1.2. Tầng Frontend (Next.js / TypeScript / React)
1. **Date Input Component**:
   - `apps/web/components/DateInputVi.tsx`: Component nhập ngày chuẩn hóa Việt Nam hiển thị trực tiếp chuỗi `DD/MM/YYYY`, đi kèm picker lịch và input ẩn chuẩn ISO `YYYY-MM-DD`.
   - Tích hợp vào `RateCardDeclarationForm.tsx` và `CreateRateVersionForm.tsx`.
2. **Operational Editor**:
   - `opref-edit.ts`: Khai báo loại trường `service_type` và mảng `services?: OprefOption[]`.
   - `OperationalReferenceEditor.tsx`: Render thẻ `<select>` dropdown cho trường loại dịch vụ; hiển thị cảnh báo nghiệp vụ khi phát hiện mã di sản `AIR`.
3. **Bill Rating Panel & Forms**:
   - `RateBillForm.tsx`: Hiển thị banner ngày áp dụng (`rateDateFormatted`) và nguồn xác định; phân nhóm rõ ràng phiên bản đủ điều kiện vs phiên bản chỉ tham khảo kèm lý do tiếng Việt.
   - `BillRatingPanel.tsx`: Bổ sung khối bóc tách VAT (Net, VAT 8%, Gross) trên kết quả Rating và lịch sử Rating. Lọc hiển thị nút Seed theo `direction`: BUY chỉ hiển thị nút Seed Chi phí; SELL chỉ hiển thị nút Seed Doanh thu.
4. **Seed Buttons**:
   - `SeedExpectedCostsButton.tsx` & `SeedExpectedRevenuesButton.tsx`: Bổ sung thuộc tính `billId` và cơ chế điều hướng deep-link tới tab tương ứng sau khi seed thành công.
5. **Cost Detail Page**:
   - `apps/web/app/costs/[id]/page.tsx`: Bổ sung section **Nguồn tính giá & Truy vết (Rating Traceability)** đầy đủ các trường truy vết #14..#21 và bảng công bố bóc tách VAT 8% (Net, VAT Rate, VAT Amount, Gross).

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
*(Ghi nhận chi tiết vị trí File, Hàm thực thi và Test case xác minh cho từng quy tắc)*

| Mã BR | Tên quy tắc | Nội dung quy tắc | Tệp mã nguồn & Hàm/Component thực thi | Hàm kiểm thử tự động xác minh |
|:---|:---|:---|:---|:---|
| **BR-01** | Phân biệt chiều Rating | BUY→Expected Cost, SELL→Expected Revenue; không suy ra từ tên khoản mục. | `src/LCMS.Application/Costs/Commands/SeedExpectedCostsFromRatingCommand.cs (Handle)`<br>`apps/web/components/BillRatingPanel.tsx` | `UatFollowup20261026Tests.TC07_BuyRating_History_RendersOnlySeedExpectedCosts`<br>`UatFollowup20261026Tests.TC08_SellRating_History_RendersOnlySeedExpectedRevenues` |
| **BR-02** | API seed direction guard | Trái chiều rating/seed phải trả lỗi nghiệp vụ, không ghi financial transaction. | `src/LCMS.Application/Costs/Commands/SeedExpectedCostsFromRatingCommand.cs`<br>`src/LCMS.Application/Revenues/Commands/SeedExpectedRevenuesFromRatingCommand.cs` | `UatFollowup20261026Tests.TC09_SeedExpectedRevenues_OnBuyRating_ThrowsValidationAppException`<br>`UatFollowup20261026Tests.TC10_SeedExpectedCosts_OnSellRating_ThrowsValidationAppException` |
| **BR-03** | Idempotent seed | Mỗi rating_detail + nature chỉ sinh đúng một economic fact theo key/policy; retry không nhân đôi. | `src/LCMS.Application/Costs/Commands/SeedExpectedCostsFromRatingCommand.cs`<br>`src/LCMS.Infrastructure/Persistence/LcmsDbContext.cs` | `UatFollowup20261026Tests.TC11_SeedIdempotency_RepeatSeed_ReturnsExistingCostsWithoutDuplicates`<br>`UatFollowup20261026Tests.TC31_FinancialIntegrity_TwoSeeds_ProduceExactlyTwoEconomicCostFacts` |
| **BR-04** | Component nature | Tất cả details của BUY rating đều có nature là COST (cả freight và surcharge). | `src/LCMS.Domain/Entities/RatingDetail.cs`<br>`apps/web/components/BillRatingPanel.tsx (RatingBreakdown)` | `UatFollowup20261026Tests.TC12_RatingDetail_ComponentNature_BothFreightAndSurchargeAreCost` |
| **BR-05** | Financial maturity | Expected cost chỉ là dự kiến, không tự ý chuyển sang Confirmed/Actual hay sinh AP. | `src/LCMS.Domain/Entities/Cost.cs (CreateExpected)`<br>`src/LCMS.Application/Costs/Commands/SeedExpectedCostsFromRatingCommand.cs` | `UatFollowup20261026Tests.TC30_FinancialMaturity_ExpectedCost_NeverAutoCreatesConfirmedOrActual` |
| **BR-06** | Single Economic Cost | Giữ nguyên số tiền kinh tế NET trước thuế; bóc tách VAT không làm biến dạng số tiền lưu DB. | `src/LCMS.Application/Costs/Queries/CostQueries.cs`<br>`src/LCMS.Domain/Enums/DeclaredVat.cs`<br>`apps/web/app/costs/[id]/page.tsx` | `UatFollowup20261026Tests.TC28_CostSeedVatDisclosure_Rating1740000_ItemizesNetVatGross`<br>`UatFollowup20261026Tests.TC29_Reconciliation_SeedCostLines_MatchesPreTaxNet_NoDoubleCount` |
| **BR-07** | Nấc trọng lượng nửa mở | Nấc trọng lượng [min, max) nửa mở; CW=120kg không khớp [1, 120) mà khớp [1, 121). | `src/LCMS.Application/Ratings/BillRateSuggestion.cs (WeightMatches)`<br>`src/LCMS.Application/Ratings/Queries/SuggestBillRatesQuery.cs` | `UatFollowup20261026Tests.TC18_RatingVersionMatch_CW120kg_FailsV1HalfOpenBound_EligibleForV2` |
| **BR-08** | Hiệu lực bảng giá As-of | Đánh giá hiệu lực theo ngày rateDate; trước ngày phát hành v2 thì v1 eligible. | `src/LCMS.Application/Ratings/BillRateSuggestion.cs (Classify)`<br>`src/LCMS.Application/Ratings/Queries/SuggestBillRatesQuery.cs` | `UatFollowup20261026Tests.TC17_RatingVersionMatch_AsOfBeforeV2_EligibleForV1` |
| **BR-09** | Version Precedence | Khi nhiều phiên bản cùng thỏa mãn, phiên bản có VersionNo cao nhất được ưu tiên. | `src/LCMS.Application/Ratings/BillRateSuggestion.cs (Classify: latestByCard)` | `UatFollowup20261026Tests.TC19_RatingVersionMatch_DeterministicPrecedence_SelectsHighestVersion` |
| **BR-10** | Đa Rate Card độc lập | Khi nhiều Rate Card độc lập cùng thỏa mãn, hệ thống trả Ambiguous=true để người dùng chọn. | `src/LCMS.Application/Ratings/BillRateSuggestion.cs (Classify: Ambiguous check)` | `UatFollowup20261026Tests.TC32_MultipleRateCards_Ambiguous_RequiresExplicitUserSelection` |
| **BR-11** | Lý do từ chối dịch vụ | Khác biệt loại dịch vụ nêu rõ tên trường và giá trị khác biệt (MISMATCH_BY_FIELD). | `src/LCMS.Application/Ratings/BillRateSuggestion.cs (RuleReason)`<br>`apps/web/components/RateBillForm.tsx` | `UatFollowup20261026Tests.TC21_RatingBlockReason_ServiceMismatch_MentionsFieldAndValue` |
| **BR-12** | Lý do từ chối trọng lượng | Khác biệt trọng lượng nêu rõ nằm ngoài nấc [min, max) (MISMATCH_BY_FIELD). | `src/LCMS.Application/Ratings/BillRateSuggestion.cs (RuleReason)`<br>`apps/web/components/RateBillForm.tsx` | `UatFollowup20261026Tests.TC22_RatingBlockReason_WeightMismatch_MentionsOutOfRange` |
| **BR-13** | Phân biệt thiếu vs lệch | Phân biệt rõ thiếu ngữ cảnh (MISSING_CONTEXT) khác với không khớp dữ liệu (MISMATCH_BY_FIELD). | `src/LCMS.Application/Ratings/BillRateSuggestion.cs (BlockReason)`<br>`apps/web/components/RateBillForm.tsx` | `UatFollowup20261026Tests.TC23_RatingBlockReason_MissingVsMismatch_DistinguishesContextFromMismatch` |
| **BR-14** | RateDate Precedence | Thứ tự ưu tiên xác định ngày tính giá: manual_override > etd > policy_fallback. | `src/LCMS.Application/Ratings/Queries/SuggestBillRatesQuery.cs`<br>`src/LCMS.Application/Ratings/Commands/CreateRatingCommand.cs` | `UatFollowup20261026Tests.TC24_RateDateTrace_EtdAbsent_FallbacksToBillCreatedAtWithClearLabel`<br>`UatFollowup20261026Tests.TC25_RateDateTrace_EtdSupplied_UsesEtdWithResolvedSourceLabel` |
| **BR-15** | RateDate Metadata Trace | Lưu trữ và công bố rateDate, rateDateFormatted, rateDateSource, rateDateSourceLabel. | `src/LCMS.Application/Ratings/RatingReadiness.cs`<br>`apps/web/components/RateBillForm.tsx` | `UatFollowup20261026Tests.TC25_RateDateTrace_EtdSupplied_UsesEtdWithResolvedSourceLabel` |
| **BR-16** | Date Localization DD/MM/YYYY | Chuẩn hóa hiển thị DD/MM/YYYY trên form khai báo và sửa bảng giá. | `apps/web/components/DateInputVi.tsx`<br>`apps/web/components/RateCardDeclarationForm.tsx`<br>`apps/web/components/CreateRateVersionForm.tsx` | `UatFollowup20261026Tests.TC26_DateLocale_Formatting_StrictlyProducesDdMmYyyy` |
| **BR-17** | Timezone Date-only safety | Lưu trữ chuẩn ISO YYYY-MM-DD không bị dịch ngày khi chuyển đổi múi giờ. | `apps/web/components/DateInputVi.tsx`<br>`apps/web/lib/money.ts (formatDateOnlyVi)` | `UatFollowup20261026Tests.TC27_DateTimezone_UtcDateOnlyPreservation_PreventsDayShift` |
| **BR-18** | Service Type Master Lookup | Trường loại dịch vụ liên kết chặt với danh mục master service_type active. | `src/LCMS.Application/OperationalReferences/Edit/OperationalFieldCatalog.cs`<br>`apps/web/components/OperationalReferenceEditor.tsx`<br>`apps/web/components/CreateBillWorkspaceForm.tsx` | `UatFollowup20261026Tests.TC01_CreateBill_ServiceTypeDropdown_OnlyActiveMaster`<br>`UatFollowup20261026Tests.TC02_EditBill_ServiceTypeDropdown_HasFreight_SeparatedFromMode` |
| **BR-19** | API Service Validation | Backend từ chối các mã serviceTypeCode tùy ý (GIBBERISH) bằng ValidationAppException. | `src/LCMS.Application/OperationalReferences/Edit/EditOperationalReferenceCommand.cs`<br>`src/LCMS.Application/Bills/Commands/UpdateBillContextCommand.cs` | `UatFollowup20261026Tests.TC03_Api_InvalidServiceTypeCode_ThrowsValidationAppException` |
| **BR-20** | Multi-Order Conflict Handling | Tạo Bill từ nhiều Order có serviceTypeCode khác nhau phải kích hoạt giải quyết xung đột. | `apps/web/components/CreateBillWorkspaceForm.tsx`<br>`apps/web/lib/input-optimization-inheritance.test.ts` | `UatFollowup20261026Tests.TC04_MultiOrder_DifferentServiceType_RequiresConflictResolution` |
| **BR-21** | Legacy AIR Service Warning | Phát hiện mã di sản AIR và hiển thị cảnh báo nghiệp vụ; không xóa hay đổi ngầm DB. | `apps/web/components/OperationalReferenceEditor.tsx` | `UatFollowup20261026Tests.TC05_LegacyServiceAir_WarnsWithoutSilentDeletionOrAutoConvert` |
| **BR-22** | Cross-Module Canonical Master | Đồng bộ sử dụng master service_type across Bill, Order, Shipment, Leg. | `src/LCMS.Application/OperationalReferences/Edit/OperationalFieldCatalog.cs (ByType)` | `UatFollowup20261026Tests.TC06_CrossModule_OrderShipmentLeg_UseCanonicalServiceMaster` |

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
