# Handoff

## 2026-09-24 — CRP-09 SMTP chưa có, CRP-10 sửa/xóa thành phần giá

### User
tạm thời chưa có cấu hình SMTP, cứ làm tiếp

### Answer
Không có SMTP thì email thông báo ghi outbox `skipped` với lỗi «SMTP chưa cấu hình. Thư chưa gửi.» Worker chỉ lấy `pending`, nên không đổi thành đã xử lý. Màn cài đặt vẫn nói chưa gửi. Gửi thử thật chờ khi có host/cổng/tài khoản. Trên phiên bản bảng giá nháp: sửa tên/số tiền và xóa thành phần. Đã phát hành thì xóa trả 409, dòng còn. Nút tạo doanh thu dự kiến từ tính giá vẫn ở Bill. Test: `EmailWithoutSmtp_IsSkipped_AndProcessOnceDoesNotMarkItSent`, `DraftComponent_CanUpdateAndDelete_PublishedVersionRejectsDelete`. Chưa bấm form trên trình duyệt.

### Files
- `NotificationPublisher.cs`, `OutboxMessage.cs`
- `PricingRuleCommands.cs`, `RatePricingEndpoints.cs`
- `PricingRuleComponentActions.tsx`, `rate-cards/[id]/page.tsx`
- `apps/web/app/bff/pricing-rules/components/[id]/route.ts`
- `Sprint12FullHardeningUatTests.cs`, `Sprint3RatePricingTests.cs`

## 2026-09-24 — CRP-08 quyền sở hữu trường

### User
ok tiếp

### Answer
`GET /api/field-ownerships?objectType=&objectId=` trả từng trường: tên, hệ thống sở hữu, lý do ghi đè mới nhất (`field.override`). Bill/đơn/lô/chặng/chuyến cần `bill.read`; doanh thu cần `revenue.read`. Không đủ quyền hoặc thuê bao khác thì danh sách rỗng. Badge trên Bill, chi tiết vận hành và doanh thu: «Điểm đi · nguồn tms-b · ghi đè: …». Không có dòng sở hữu thì không hiện ô. Test: `FieldOwnership_ReturnsOwnerAndOverrideReason_AndHidesOtherTenant`, `FieldOwnership_HidesRevenueOwnerFromCostAccountant`. Chưa bấm badge trên trình duyệt.

### Files
- `src/LCMS.Application/FieldOwnership/Queries/GetFieldOwnershipQuery.cs`
- `src/LCMS.Api/Endpoints/OperationalReferenceEndpoints.cs`
- `apps/web/lib/field-ownership.ts`, `apps/web/components/FieldOwnershipPanel.tsx`
- `apps/web/app/bills/[id]/page.tsx`, `operations/[kind]/[id]/page.tsx`, `revenues/[id]/page.tsx`
- `OperationalCargoTests.cs`, `CostRevenueSodWriteTests.cs`

## 2026-09-24 — Ô tìm Bill trên Tạo doanh thu bị kéo cao

### User
Form tạo doanh thu, ô tìm kiếm đang hiển thị lỗi.

### Answer
Ô tìm bọc trong `label.field` (flex cột). `flex: 1 1 220px` trên `input[type=search]` thành chiều cao, hộp viền trống nằm trên ô gõ. Đổi sang `FilterBar` (hàng ngang, nút Tìm cạnh ô). CSS search/filter khóa `max-height: 2.75rem` và `appearance: none`.

### Files
- `apps/web/app/revenues/new/page.tsx`
- `apps/web/app/globals.css`

## 2026-09-24 — CRP-07 tìm kiếm đa thực thể

### User
ok, tiếp

### Answer
Ô tìm trên thanh trên đã ra thanh toán, thu tiền và bảng giá, cùng Bill / chứng từ / đối tác sẵn có. Thanh toán chỉ với `cost.read`; thu tiền chỉ với `revenue.read`. Bảng giá mua (`vendor`) theo `rate.buy.read`, bảng giá bán (`customer`) theo `rate.sell.read`. Khớp theo số tham chiếu (thanh toán/thu) hoặc mã/tên/hãng/tuyến (bảng giá). Kết quả dẫn tới `/settlements/payments/{id}`, `/settlements/collections/{id}`, `/rate-cards/{id}`. Tenant khác không lộ. Test: `GlobalSearch_SeparatesPaymentCollectionAndBuySellRates` và `GlobalSearch_FindsOrderAndIgnoresOtherTenant`. Chưa bấm ô tìm trên trình duyệt (máy local không chạy web).

### Files
- `src/LCMS.Application/Search/Queries/SearchGlobalQuery.cs`
- `apps/web/components/GlobalSearch.tsx`
- `tests/LCMS.Api.Tests/CostRevenueSodWriteTests.cs`

## 2026-09-24 — CRP-06 If-Match đảo phân bổ và mở lại chốt

### User
TLS, hostname và chứng chỉ chưa có. Tạm thời chạy test vậy đã. Tiếp theo làm gì triển khai luôn nhé

### Answer
TLS để đó (chưa có hostname/chứng chỉ; không cắt HTTP). Lát còn lại của AC-010: đảo phân bổ thanh toán/thu và mở lại lần chốt đọc `If-Match` ngay sau khi tải dòng, trước kiểm tra trạng thái. Token cũ → 409 `concurrency_conflict`, không đảo và không mở. Token đúng vẫn đảo / mở. Nút Đảo phân bổ và Mở lại chốt gửi `rowVersion` qua BFF. Development vẫn cho phép thiếu header. ADR-0036 cập nhật. Test: `StaleIfMatch_DoesNotReopenCloseAfterSnapshot` (chốt xong, mở bằng token lúc còn open → vẫn `locked`; mở bằng token mới → `reopened`).

### Files
- `ReversePaymentAllocationCommand.cs`, `ReverseCollectionAllocationCommand.cs`, `ReopenFinancialCloseCommand.cs`
- `SettlementEndpoints.cs`, `FinancialCloseEndpoints.cs`
- `ReverseAllocationButton.tsx`, `ReopenCloseButton.tsx`
- `settlements/payments/[id]/page.tsx`, `settlements/collections/[id]/page.tsx`, `financial-closes/[id]/page.tsx`
- `docs/adr/ADR-0036-money-if-match.md`, `CostRevenueSodWriteTests.cs`

## 2026-09-24 — CRP-06 If-Match trên lệnh tiền

### User
ok, tiếp đi chuyên gia

### Answer
Màn hình cũ không còn cộng điều chỉnh chi phí lần hai. `If-Match` (base64 `rowVersion`) lệch bản đang lưu thì 409 `concurrency_conflict`, không ghi. Áp cho điều chỉnh / xác nhận / thực tế hóa chi phí và doanh thu, phân bổ và chốt thanh toán-thu, tạo bản chốt. Tạo phân bổ đụng `row_version` của phiếu tiền để lần phân bổ sau phải tải lại. Cùng idempotency key vẫn trả id cũ trước khi xét phiên bản. Chốt phân bổ đã chốt vẫn no-op. `Concurrency:RequireIfMatch=true` trên Production; Development bỏ qua header thiếu. ADR-0036. Test: `StaleIfMatch_DoesNotApplySecondCostAdjustment` (1000+100, lần hai +50 với token cũ → vẫn 1100).

### Files
- `RowVersionGuard.cs`, `appsettings.Production.json`
- Adjust/confirm/actualize cost+revenue, allocate/finalize payment+collection, close snapshot
- GET DTOs `rowVersion`; web gửi `If-Match` qua BFF
- `docs/adr/ADR-0036-money-if-match.md`
- `CostRevenueSodWriteTests.cs`

## 2026-09-24 — CRP-05 ngưỡng phê duyệt chặn ghi nhận và chốt phân bổ chi phí

### User
ok, tiếp đi chuyên gia

### Answer
Khi tenant đặt `confirmApprovalThresholdBase`, ghi nhận phải trả/phải thu vượt trần bị 409 và không tạo AP/AR cho đến khi có Approval `approved`. Exposure gắn chi phí/doanh thu dùng trạng thái duyệt của dòng nguồn; exposure không gắn nguồn xét trên số trần của exposure (`payable_exposure` / `receivable_exposure`). Lần bị chặn ghi `accounts_payable.recognize_blocked` (và bản phải thu) rồi mới trả 409. Chốt phân bổ chi phí chung cũng bị chặn cho đến khi chi phí được duyệt. Gửi duyệt phiên phân bổ tạo Approval `cost_allocation`; chốt từ `pending_approval` cần Approval đó đã duyệt. Ngưỡng null giữ hành vi cũ. ADR-0035. Test: `OverThreshold_BlocksRecognizeAndAllocationFinalizeUntilApproved`.

### Files
- `RecognitionApprovalGate.cs`, recognize payable/receivable handlers
- `AllocationCommands.cs` finalize + submit
- `Approval.cs` object types, `RequestApprovalCommand.cs`, `AuditEvent.cs`
- `docs/adr/ADR-0035-recognition-approval-gate.md`
- `CostRevenueSodWriteTests.cs`

## 2026-09-24 — CRP-04 phân bổ nháp không tạo phiên đôi

### User
tiếp đi chuyên gia

### Answer
Cùng `Idempotency-Key` trên tạo phân bổ chi phí chung, phân bổ thanh toán và phân bổ thu trả lại id phiên đầu, không mở phiên thứ hai. Find chạy trước guard “đã có phiên chưa chốt” và trước kiểm trần số dư. Form phân bổ và nút chốt giữ một key đến khi thành công. Chốt phân bổ thanh toán/thu vốn đã no-op khi đã chốt; chốt phân bổ chi phí lần hai vẫn 409 (trạng thái). Test: `SameIdempotencyKey_DoesNotCreateSecondCostAllocation`.

### Files
- `IdempotencyGate.cs` scopes `cost_allocation`, `payment_allocation`, `collection_allocation`
- `AllocationCommands.cs` CreateCostAllocation, `AllocatePaymentCommand.cs`, `AllocateCollectionCommand.cs`
- Cost/Settlement allocation endpoints đọc `Idempotency-Key`
- `AllocateSharedCostForm.tsx`, `AllocateCashForm.tsx`, `FinalizeCostAllocationButton.tsx`, `FinalizeAllocationButton.tsx`
- `CostRevenueSodWriteTests.cs`

## 2026-09-24 — CRP-04 điều chỉnh và bản chốt không ghi đôi

### User
tiếp

### Answer
Cùng `Idempotency-Key` trên điều chỉnh chi phí/doanh thu trả lại dòng cũ, không cộng delta lần hai. Bản chốt cũng nhớ key (`financial_close_snapshot`) trước khi thấy lần chốt đã khóa. Nút điều chỉnh, chuyển maturity và tạo bản chốt giữ một key đến khi thành công. Test: `SameIdempotencyKey_DoesNotApplyCostAdjustmentTwice` (1000 + 100 hai lần → 1100).

### Files
- `AdjustCostCommand.cs`, `AdjustRevenueCommand.cs`, `IdempotencyGate.cs` scopes
- `CreateFinancialCloseSnapshotCommand.cs`, Cost/Revenue/FinancialClose endpoints
- `AdjustCostRevenueButton.tsx`, `MaturityTransitionButton.tsx`, `CloseSnapshotButton.tsx`
- `CostRevenueSodWriteTests.cs`

## 2026-09-24 — CRP-04 Idempotency client trên lệnh tạo tiền

### User
ok, tiếp

### Answer
Form tạo chi phí / chi phí chung / doanh thu / thanh toán / thu / mở chốt giữ một `Idempotency-Key` đến khi thành công và khóa cú bấm đôi trong cùng nhịp. Ghi nhận AP/AR, nhận chứng từ, mở phiên khớp trước đó không gửi key — giờ có. BFF `forwardApiMutation` đã chuyển header. Nút xác nhận/phân bổ/chốt vẫn sinh key mới mỗi lần; server chưa dedupe các lệnh đó.

### Files
- `apps/web/lib/use-idempotency.ts`
- CreateCost/Revenue/SharedCost, CreateCashTxn, StartFinancialClose
- RecognizeExposureForm, ReceiveDocumentForm, StartDocumentMatchForm

## 2026-09-24 — CRP thứ tự khóa + SoD ghi tiền + FX production

### User
hãy chốt thứ tự rồi bắt đầu làm tiếp theo ưu tiên từ cao đến thấp, làm ở mức commercial ready product nhé

### Answer
Thứ tự khóa: **P0** TLS (chờ hostname + chứng chỉ, không cắt HTTP) → SoD Cost≠Revenue → FX không stub trên Production → idempotency client còn sót → approval enforce → AC-010. Rồi P1 (search, ownership, SMTP, components, series, backup, bootstrap) → P2 UAT/09B → P3 OIDC/soak/OTLP/bank chỉ khi PO mở.

Đã ship lát P0 code: quyền `revenue.create` (Admin, Kiểm soát tài chính, Kế toán doanh thu). Tạo chi phí cần `cost.create`, tạo doanh thu cần `revenue.create`. Điều chỉnh theo maturity (dự kiến/xác nhận/thực tế). Production `AllowStubFxFallback=false` — thiếu `fx_rates` thì từ chối, không lấy tỷ giá config. Dev/test vẫn dùng stub. Dashboard bỏ qua tiền tệ thiếu tỷ giá thay vì 500.

### Files
- `PermissionCodes.cs`, `SystemRoleCatalog.cs`
- `CreateCostCommand.cs`, `CreateRevenueCommand.cs`, `AdjustCostCommand.cs`, `AdjustRevenueCommand.cs`
- `CostFxStub.cs`, `RevenueFxStub.cs`, `SettlementFxStub.cs`, options, `GetDashboardSummaryQuery.cs`
- `src/LCMS.Api/appsettings.Production.json`
- ADR-0033, ADR-0034
- Test: `CostRevenueSodWriteTests`

## 2026-09-23 — Bậc kg bảng giá bán seed thành doanh thu dự kiến

### User
Bậc 21–100 kg = 5 USD, rating 60 kg = 300 USD đúng, nhưng seed doanh thu báo không có dòng doanh thu. Làm engine ghi 300 USD đó thành Expected Revenue mà không cộng thêm tiền.

### Answer
Dòng tính từ chính quy tắc (bậc kg, bậc bước, container, đơn giá không có thành phần) lấy tính chất theo loại bảng giá: `customer` = doanh thu, còn lại = chi phí. Thành phần giá vẫn dùng tính chất riêng và vẫn cộng thêm vào tổng. Bảng giá bán 60 kg × 5 = 300 USD seed một doanh thu dự kiến 300 USD. Seed chi phí trên lần tính đó bị từ chối vì không còn dòng chi phí. Bảng giá mua giữ dòng chi phí như cũ.

### Files
- `src/LCMS.Application/Ratings/Commands/CreateRatingCommand.cs`
- Test: `RatingModeTests.SellWeightBreak_SeedsExpectedRevenue_WithoutAnExtraComponent`

## 2026-09-23 — Gán lại vai trò đối tác và danh sách hãng vận chuyển

### User
Bỏ vai trò Nhà cung cấp / Hãng vận chuyển rồi chọn lại thì báo «Vai trò đối tác đã tồn tại». Đối tác chỉ có vai trò Hãng vận chuyển không hiện trong danh sách Khách hàng hay Nhà cung cấp, phải tìm mã trên ô tìm kiếm.

### Answer
Thu hồi vai trò là xóa mềm, unique `(tenant, party, role)` vẫn giữ dòng cũ nên gán lại bị 409. Gán lại giờ mở dòng đã xóa. Danh sách Khách hàng gồm cả bên trả tiền, người gửi, người nhận, bên nhận hóa đơn. Danh sách Nhà cung cấp gồm cả bên nhận tiền, hãng vận chuyển, đại lý. Ô chọn đối tác trên Bill/chi phí vẫn lọc đúng một vai trò.

### Files
- `src/LCMS.Application/PartyRoles/Commands/PartyRoleCommands.cs`
- `src/LCMS.Domain/Entities/PartyRole.cs` (`ForDirectory`)
- `src/LCMS.Application/BusinessParties/Queries/BusinessPartyDirectoryQueries.cs`
- `apps/web/components/PartyRolesPanel.tsx`
- Test: `PartyRoles_AssignListRevoke_AndOrgTreeChildren` (gán lại sau thu hồi), `CarrierOnly_AppearsOnVendorDirectory_NotCustomer`

## 2026-09-23 — Nhập đối tác dùng được cho kế toán

### User
Hoàn thiện chức năng Nhập đối tác ở mức commercial ready.

### Answer
`/admin/parties/import` có file mẫu `mau-nhap-doi-tac.csv`, nút chọn file tiếng Việt, và bảng xem trước (mã, tên, MST, vai trò, tài khoản, liên hệ, đạt/lỗi) trước khi ghi. CSV nhận dấu phẩy hoặc chấm phẩy, tên có dấu phẩy trong ngoặc kép, header tiếng Việt hoặc tiếng Anh. Vai trò ghi «Khách hàng;Nhà cung cấp». Một dòng có thể kèm địa chỉ, một tài khoản ngân hàng mặc định và một người liên hệ chính (Kế toán). Một dòng lỗi thì không ghi gì. Không gộp hồ sơ trùng mã/MST.

### Files
- `apps/web/components/ImportPartiesForm.tsx`, `apps/web/lib/party-csv.ts`, `apps/web/app/admin/parties/import/page.tsx`
- `src/LCMS.Application/BusinessParties/Commands/ImportBusinessPartiesCommand.cs`
- Test: `PartyImportTests.Commit_VietnameseRoles_WritesBankAndContact`

### API
`POST /api/party-imports/preview` và `/commit`. Thêm trường tùy chọn: `addressLine1`, `city`, `province`, `bankName`, `bankAccountNumber`, `bankAccountName`, `bankCurrencyCode`, `contactName`, `contactPhone`, `contactEmail`, `contactFunction`. `roleCodes` và `partyKind` nhận nhãn tiếng Việt.

## 2026-09-23 — Tạm tắt dotnet test trên CI

### User
Bước test trên Actions lâu. Tạm bỏ qua đến khi được báo bật lại. Bỏ qua thì ảnh hưởng gì?

### Answer
Chỉ tắt bước `dotnet test` trong job `test` (`.github/workflows/ci.yml`, `if: false`). Build, health smoke (API + Postgres), build web và deploy vẫn chạy. Ảnh hưởng: lỗi biên dịch, API không lên, web không build vẫn chặn deploy. Lỗi nghiệp vụ tiền, đăng nhập, đơn hàng, tính giá không còn bị chặn trước production. Bật lại bằng cách xóa `if: false`.

## 2026-09-23 — Tạo đơn hàng báo lỗi hệ thống

### User
Tạo order bị lỗi (banner: «Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.» trên `/orders/new`, đơn ORD-UAT-001).

### Answer
Form tạo luôn gửi `context`. Handler thêm đơn vào change tracker rồi gọi cargo store, store lại `FirstAsync` xuống database trước `SaveChanges` nên ném `InvalidOperationException` → HTTP 500. Cùng lỗi với tạo Shipment khi có `context`. Sửa: lấy Bill/đơn/Shipment đang theo dõi trong context trước, chỉ query database khi bản ghi đã có.

### Files
- `src/LCMS.Application/OperationalReferences/OperationalCargoStore.cs`
- Test: `Ui02OperationalCreateContextTests.UpsertOrder_WithCustomerAndContext_PersistsOnFirstSave`, `UpsertShipment_WithContextDocument_PersistsOnFirstSave`

### API
`PUT /api/orders` và `PUT /api/shipments` với `applyContext` + `context` (khách hàng, điểm đi/đến, kiện). Không đổi contract.

## 2026-09-23 — Màn tạo bảng giá theo lưới khung × loại hàng

### User
Làm tiếp màn tạo bảng giá giống bảng Air đã phát hành (khung kg/CBM × cột loại hàng).

### Answer
`/rate-cards/new` soạn đúng lưới đó: khung Air hoặc Sea, ô trống = không có giá, mức Đến trùng mức Từ khung sau thì số lẻ thuộc khung dưới. Lưu một lần thành phiên bản nháp (`POST /api/rate-cards/compose`). Phát hành vẫn trên hồ sơ bảng giá. Phiên bản nháp chưa có quy tắc cũng dùng form này. Phí giao dưới ngưỡng và phụ phí vùng (SBH/SWK, đ/kg) là tuỳ chọn.

### Files
- `src/LCMS.Application/RateCards/Commands/ComposeTariffCommand.cs`
- `apps/web/components/ComposeTariffForm.tsx`, `app/rate-cards/new/page.tsx`, `app/bff/rate-cards/compose/route.ts`
- Test: `ComposeTariff_UsesNextBandAsCeiling_AndSkipsEmptyExpressCell` (10,5 kg = 682.500 đ; chuyển nhanh dưới 11 kg bị từ chối)

## 2026-09-23 — Bảng giá mẫu NewSkyExpress VN–MY

### User
`docs/po/Bảng giá tham khảo` là bảng giá tham khảo. Sabah/Sarawak là 85.000 đ/kg. Nhập hai bảng thành bảng giá mẫu (Published, tính được trên Bill) và lấy chúng làm khuôn khi sửa màn bảng giá.

### Answer
Hai bảng NewSkyExpress thành bảng giá nhà cung cấp đã phát hành, idempotent theo mã, gắn vào seed dữ liệu mẫu (kể cả tenant đã có marker demo):

- `NSE-AIR-VN-MY` — AVMCC26_002, hiệu lực 24/03/2026, VND/kg, bậc trọng lượng × Hàng thường / Thực phẩm khô / Mỹ phẩm / Chuyển nhanh. Chuyển nhanh không có giá dưới 11 kg. Phí giao dưới 2 kg: 50.000 đ/đơn. 10,5 kg hàng thường = 682.500 đ.
- `NSE-SEA-VN-MY` — SYMCC23_002_CBM, hiệu lực 05/08/2023, USD/CBM, tối thiểu 1 CBM. Cột Hàng thường và Mỹ phẩm/thực phẩm. Khung 4–6 gồm đến dưới 7 CBM.
- Sabah/Sarawak: 85.000 đ/kg khi điểm đến `SBH` hoặc `SWK`. Air nhân trên kg tính cước. Sea nhân trên kg thực (`per_gross_kg`) và quy đổi VND→USD; thiếu kg hoặc thiếu tỷ giá thì từ chối, không cộng nhầm vào tổng USD.

Màn chi tiết bảng giá có bậc thì hiện lưới khung × loại hàng và bảng phụ phí, theo khuôn ảnh tham khảo. Form tính giá trên Bill có loại hàng, điểm đến vùng, kg thực.

### Files
- `src/LCMS.Application/Demo/ReferenceTariffCatalog.cs`, `DemoDataSeeder.EnsureReferenceTariffsAsync`
- `src/LCMS.Application/Ratings/Commands/CreateRatingCommand.cs` (loại hàng trên Bill, phụ phí theo kg thực)
- `apps/web/components/TariffMatrix.tsx`, `app/rate-cards/[id]/page.tsx`, `components/RateBillForm.tsx`
- Test: `RatingModeTests.ReferenceTariff_RatesBillByBandCommodityAndRemoteFee`

### API
Không endpoint mới. Seed qua `POST /api/sample-data/ensure` (quyền `settings.manage`). Production `Demo:SeedOnStartup` = false — bấm Cài đặt → Dữ liệu mẫu để có hai bảng.

## 2026-09-22 — Xuất canvas Commercial Ready → Word

### User
xuất canvas sang file word

### Answer
Sinh `docs/reports/CMS_Commercial_Ready_Product_Plan_2026-09-22.docx` từ nội dung canvas (verdict, maturity L0–L5, 4 pha, backlog CRP-01…21, OUT, DoD). Script: `_gen_commercial_ready_plan_docx.py`.

### Files
- `docs/reports/CMS_Commercial_Ready_Product_Plan_2026-09-22.docx`
- `docs/reports/_gen_commercial_ready_plan_docx.py`

## 2026-09-22 — Rà soát hệ thống → kế hoạch Commercial Ready Product

### User
rà soát toàn hệ thống 1 lần nữa, lên kế hoạch làm ở cấp độ Commercial Ready Product toàn dự án.

### Answer
Verdict: L0 Financial Go-Live + L1 Standalone SCP (ADR-0017) đã đủ vận hành Bill→Close; commercial bị chặn ở Trust Gate + Acceptance, không thiếu module M04–M11. Kế hoạch 4 pha: **P0 Trust** (TLS 443, SoD Cost≠Revenue UAT, FX bỏ stub, Idempotency client, approval enforce, AC-010) → **P1 Package** (Global Search, field ownership UI, SMTP, pricing components, dashboard series, backup runbook, tenant bootstrap) → **P2 Accept** (UAT Wave2 N1–N5, 09B Reviewer, a11y, multi-tenant UAT) → **P3 Enterprise PO-gate** (OIDC, soak/SLO, OTLP, bank connector). OUT: TMS, Budget/Forecast, party merge, AP/AR full-page split, fake MoM/Excel. DoD CRP = P0+P1 prod + P2 ký + AC-SCP-01…10 trên tenant không connector.

### Files
- Canvas: `commercial-ready-product-plan.canvas.tsx` (IDE canvases)
- Tham chiếu: `docs/ops/go-live-checklist.md`, `docs/reports/CMS_PixelPerfect_Gap_Index_2026-09-22.md`, ADR-0017, handoff A–H

## 2026-09-22 — UX-07 period_locked vs concurrency (commercial money path)

### User
tiếp bạn, làm ở cấp độ commercial ready product luôn nha

### Answer
(1) API: `PeriodLockedAppException` code `period_locked` (tách `concurrency_conflict`); copy VI bỏ marker EN. (2) Web: `formatHttpError` phân loại 409; wire maturity/allocate/adjust/finalize/cash. (3) UAT script + Sprint10 assert `period_locked`.

### Files
- `AppException.cs`, `PeriodLockGate.cs`
- `lib/api-error.ts`, MaturityTransition/BillCostRevenue/AllocateCash/Adjust/Finalize*/CreateCashTxn
- `Sprint10FullFinancialCloseTests.cs`, `scripts/uat-vps-p04-strict-period-lock.ps1`

## 2026-09-22 — W-L4 Settlement allocate timeline (Pixel UI-09)

### User
làm tiếp

### Answer
Payment/Collection detail: rail timeline (ghi nhận tiền → từng phân bổ nháp/chốt/đảo) thay bảng phẳng; CTA chốt/đảo giữ trên từng bước. API allocation DTO thêm `CreatedAt`.

### Files
- `SettlementQueries.cs`, `settlements-shared.ts`
- `SettlementAllocationTimeline.tsx`, `globals.css`
- `settlements/payments|collections/[id]/page.tsx`

## 2026-09-22 — W-K5 Bill drawer tab Tính giá (Pixel UI-02)

### User
tiếp nào chuyên gia, vẫn Pixel-perfect nhé

### Answer
Bill Financial View drawer: tab «Tính giá» + CTA deep-link `/bills/{id}?tab=rating` (không nhúng form Rating vào client drawer); quick action trên Tổng quan. Khớp tabs hồ sơ Bill / mockup UI-02.

### Files
- `BillFinancialDrawer.tsx`
- gap index + UAT UI-02

## 2026-09-22 — W-L5 close CTA gate + W-J6 FX canonical

### User
tiếp nào chuyên gia

### Answer
(1) W-L5: `CloseSnapshotButton` nhận `eligible` — disabled + ghi chú khi còn gate Chặn; note N/M điều kiện đạt. (2) W-J6: `/admin/fx-rates` redirect → `/rate-cards/fx`; currencies link cập nhật.

### Files
- `CloseSnapshotButton.tsx`, `financial-closes/[id]/page.tsx`
- `admin/fx-rates/page.tsx`, `admin/currencies/page.tsx`, `AppShell.tsx`

## 2026-09-22 — Nav RBAC (H-009) + UX-13 correlation trên lỗi

### User
còn gì nữa làm tiếp đi chuyên gia

### Answer
(1) AppShell: costs/revenues = license ∧ financialVisibility; ẩn báo cáo lãi nếu thiếu margin. (2) CTA tạo trên `/costs` `/revenues` theo quyền đọc. (3) UX-13: `lib/api-error.ts` — message + «Mã hỗ trợ: {correlationId}» trên bills/cost-revenue GET + form tiền chính.

### Files
- `AppShell.tsx`, `revenues/page.tsx`, `costs/page.tsx`
- `lib/api-error.ts`, `lib/bills.ts`, `lib/costs-revenues-server.ts`
- CreateCost/Revenue/SharedCost/CashTxn + StartFinancialClose forms

## 2026-09-22 — Live verify cost@ after cb7f1b7 deploy

### User
tiếp tục nào chuyên gia

### Answer
CI `35720900541` success. `cost@`: profit+revenues API/UI 403; costs 200; dashboard note ẩn DT/biên. UX-04 Pass* (residual nav/CTA Doanh thu).

## 2026-09-22 — UX-04/12 + J5 + CostAccountant revenue grant leak

### User
tiếp tục nào chuyên gia

### Answer
(1) Idempotency-Key + double-submit trên CreateCost/SharedCost/Revenue + StartFinancialClose. (2) UX-04: `cost@`/`revenue@` — CostAccountant trong DB lệch catalog (có `revenue.*`); soft-delete prod + `TenantAccessSeeder` prune system-role extras; profit board bắt CostRead+RevenueRead. Dashboard cost ẩn DT/biên. (3) W-J5 So sánh: toolbar + grid 3 cột + badge Tốt nhất. Nav Doanh thu theo license còn hiện (follow-up UI).

### Files
- `CreateCostForm.tsx`, `CreateSharedCostForm.tsx`, `CreateRevenueForm.tsx`, `StartFinancialCloseForm.tsx`
- `TenantAccessSeeder.cs`, `ProfitabilityBoardQueries.cs`
- `CompareRatesForm.tsx`, `globals.css`
- UAT + gap index

## 2026-09-22 — UAT Pixel Wave 2 (admin demo) + hotfix slug

### User
user demo: admin@cms.local (password local-only) — chạy UAT.

### Answer
Prod web 500 do slug `billId`≠`id` → fix `0a2df35` + rebuild VPS; CI success. Đăng nhập demo → walk UI-01…15 + Wave 2; điền `docs/uat/UAT-PIXEL-PERFECT-WAVE2.md`. P0 Fail = 0 trên phần đã chạy. Blocked: UX-04/06. Not run: UX-07/10/12/13. Residual P1: “Best Available” EN; help text unlink trên legs/new lệch. **Không** ghi password.

### Files
- `apps/web/app/bff/bills/[id]/legs|movements|shipments/…` (slug fix)
- `docs/uat/UAT-PIXEL-PERFECT-WAVE2.md`, `docs/uat/evidence-2026-09-22/README.md`

## 2026-09-22 — Hotfix: Next.js slug `billId` ≠ `id` làm web 500

### User
user demo: admin@cms.local (password local-only) — chạy UAT Pixel Wave 2 trên prod.

### Answer
Prod web 500: `You cannot use different slug names for the same dynamic path ('billId' !== 'id')`. Unlink BFF tạo `bff/bills/[billId]/…` song song `bff/bills/[id]/…` (và `orders/[orderId]` vs `[id]`). Gộp về `[id]`; DELETE unlink gắn vào route orders đã có. **Không** ghi password vào repo. UAT tiếp sau deploy.

### Files
- `apps/web/app/bff/bills/[id]/legs|movements|shipments/…/route.ts` (thay `[billId]`)
- `apps/web/app/bff/orders/[id]/bills/[billId]/route.ts` (+ DELETE)
- Xóa `bff/bills/[billId]/…`, `bff/orders/[orderId]/…`

## 2026-09-22 — Trình tự tối ưu: K3 unlink + J3 breaks + N template + L3 ADR

### User
bạn là chuyên gia mà, hãy làm theo trình tự mà bạn nhận định là tối ưu nhất

### Answer
Thứ tự chuyên gia: (1) Unlink quan hệ — soft-delete + `link.remove` + CTA drawer khi có `linkId` trực tiếp; chặng/chuyến suy ra không gỡ giả. ADR-0032 + `UnlinkRelationTests`. (2) Panel bậc trọng lượng/container trên `/rate-cards/[id]`. (3) Checklist UAT Pixel Wave 2 (Pass trống). (4) ADR-0031 giữ `/ap-ar?tab=`. Commit `cc6918a`.

### Files
- `UnlinkCommands.cs`, `GetBillGraphQuery` (+linkId), DELETE API/BFF, `UnlinkRelationButton`, `BillFinancialDrawer`
- Pricing rules breaks DTO + rate detail UI
- `docs/uat/UAT-PIXEL-PERFECT-WAVE2.md`, ADR-0031, ADR-0032

## 2026-09-22 — W-I3: Business Party CSV import (preview → all-or-nothing)

### User
Implement W-I3: Business Party Excel/CSV import with preview → commit all-or-nothing. … Do not commit/push. Return files + sample CSV header.

### Answer
Thêm `PreviewPartyImport` / `CommitPartyImport` (create-only, không merge trùng MST/mã). API `POST /api/party-imports/preview|commit`. Web `/admin/parties/import` + `ImportPartiesForm` (CSV) + BFF + nav. Test `PartyImportTests`. Chưa commit/push theo yêu cầu.

Sample CSV header:
`code,name,taxId,roleCodes,isCustomer,isVendor,isPayer,isPayee,legalName,phone,email,partyKind,countryCode,defaultCurrencyCode,paymentTermDays,creditLimit,groupCode,externalCode,shortName,notes`

### Files
- `src/LCMS.Application/BusinessParties/Commands/ImportBusinessPartiesCommand.cs`
- `src/LCMS.Application/DependencyInjection.cs`, `src/LCMS.Api/Endpoints/MasterDataEndpoints.cs`
- `apps/web/app/admin/parties/import/page.tsx`, `components/ImportPartiesForm.tsx`
- `apps/web/app/bff/party-imports/preview/route.ts`, `commit/route.ts`
- `apps/web/app/admin/parties/page.tsx`, `components/AppShell.tsx`
- `tests/LCMS.Api.Tests/PartyImportTests.cs`
- `docs/reports/CMS_PixelPerfect_Gap_Index_2026-09-22.md` (W-I3 DONE)

## 2026-09-22 — Wave 2 polish: W-K4 / W-J4 / W-L6

### User
Implement remaining small Wave 2 polish: W-K4 field ownership note, W-J4 rate history filter+CSV, W-L6 reports honest copy. Do not commit/push. Return files changed.

### Answer
W-K4: API bill detail / financial-view chưa expose `field_ownerships` → ghi chú VI dưới điểm đi/đến trên form tạo Bill và Order (không matrix UI). W-J4: `/rate-cards/history` lọc từ/đến ngày (client trên list đã tải) + `ExportCsvButton` nhãn «Xuất CSV». W-L6: copy trên `/reports` và `/revenues/report` nêu rõ số từ API. Chưa commit/push.

### Files
- `apps/web/components/CreateBillWorkspaceForm.tsx`, `CreateOrderForm.tsx`
- `apps/web/app/rate-cards/history/page.tsx`
- `apps/web/app/reports/page.tsx`, `revenues/report/page.tsx`
- `docs/reports/CMS_PixelPerfect_Gap_Index_2026-09-22.md`

## 2026-09-22 — W-K2: CreateWorkspace Chặng / Chuyến

### User
In c:\A1\git\cms implement W-K2: CreateWorkspace for Leg (Chặng) and Movement (Chuyến) under /operations. … Do not implement unlink (K3 blocked). Do not commit/push. Return files changed.

### Answer
Thêm CreateWorkspace tạo Chặng (`/operations/legs/new`) và Chuyến (`/operations/movements/new`) — chỉ field bắt buộc API (số + shipmentId cho chặng; source/externalId mặc định `lcms_manual`). CTA + empty-state trên `/operations`, nav AppShell «Tạo chặng / Tạo chuyến». Không TMS; không unlink (K3). Chưa commit/push theo yêu cầu.

### Files
- `apps/web/components/CreateLegForm.tsx`, `CreateMovementForm.tsx`
- `apps/web/app/operations/legs/new/page.tsx`, `movements/new/page.tsx`
- `apps/web/app/operations/page.tsx`, `components/AppShell.tsx`
- `docs/reports/CMS_PixelPerfect_Gap_Index_2026-09-22.md` (W-K2 DONE)

## 2026-09-22 — Wave 2 Pixel-perfect đợt 1 (I1/I2/J1/J2/K1/L1/M1–M2)

### User
làm lần lượt đến hết nhé

### Answer
Đợt 1 Wave 2: màn nhập nghiệp vụ CSV (`/operations/import`) và nhập bảng giá JSON (`/rate-cards/import`) — preview → commit all-or-nothing. Phụ phí/phụ lục có KPI + filter + CTA trung thực. SoD PC-21: người tạo phiên phân bổ không tự chốt (`AllocationSodTests`). Lưới kiện/container trên tạo Bill/Order. Workflow map thêm bước import + link từ Cài đặt. Helper `Idempotency-Key` trên import và chốt phân bổ. Còn I3, K2/K3, L2–L6, M5 rộng, N UAT.

### Files
- `apps/web/app/operations/import/page.tsx`, `rate-cards/import/page.tsx`, BFF operational-import + rate-imports
- `apps/web/components/ImportOperationalForm.tsx`, `ImportRateCardsForm.tsx`, `CreateCargoLineGrid.tsx`
- `apps/web/app/rate-cards/surcharges/page.tsx`, `appendices/page.tsx`
- `src/LCMS.Application/Costs/Commands/AllocationCommands.cs` (PC-21), `CostQueries` CreatedBy
- `tests/LCMS.Api.Tests/AllocationSodTests.cs`
- `docs/reports/CMS_PixelPerfect_Gap_Index_2026-09-22.md` §10

## 2026-09-22 — Chỉ mục Pixel-perfect còn thiếu (toàn dự án)

### User
rà soát và lập chỉ mục toàn dự án các chức năng chưa làm đến mức Pixel-perfect đảm bảo đủ độ sâu và mở rộng tối đa.

### Answer
Gói A–H = domain DONE trên production; claim «Pixel-perfect» ≠ UAT mockup. UX-01…14 vẫn «Chưa kiểm thử». Module UI-01…15: 0 MISSING cấp 1; ~1 PIXEL_OK (Dashboard); đa số STRUCTURAL; SKELETON: phụ phí, phụ lục, operations, workflow map. Wave 2 (I–N): ~35 việc — P0 gồm import UI (ops + rate), phụ phí/phụ lục, SoD phân bổ, idempotent client, UAT evidence + 09B Reviewer. Không mở lại bulk/% MoM/Excel giả (INTENTIONAL).

### Files
- `docs/reports/CMS_PixelPerfect_Gap_Index_2026-09-22.md` — chỉ mục đầy đủ
- Canvas IDE: `pixel-perfect-gap-index.canvas.tsx` (lọc theo gói/maturity/prio)

## 2026-09-22 — Fix UI tạo doanh thu: chọn Bill dạng danh sách

### User
trang tạo doanh thu đang bị lỗi hiển thị. Đề xuất hiển thị như mockup danh sách bill

### Answer
`/revenues/new` trước đây dump `<ul class="inline-list">` toàn số Bill (VOL-BILL-…) không bảng. Đổi thành bảng chọn Bill theo cột UI-02 (số Bill, khách, tuyến, loại, ngày, trạng thái, DT/CP/LN + nút Chọn). Có ô tìm. Khi đã chọn: thẻ Bill + form tạo; nút Đổi Bill quay lại bảng.

### Files
- `apps/web/components/BillPickTable.tsx`
- `apps/web/app/revenues/new/page.tsx`

## 2026-09-22 — Gói H: policy, idempotency, observability, 09B

### User
làm tiếp gói H Pixel-perfect

### Answer
Gói H (W-H1…H4) xong. Sổ 13 khóa chính sách (`policies`) có chủ sở hữu, phiên bản, ngày hiệu lực; UI `/settings/policies`. Header `Idempotency-Key` trên tạo chi phí/doanh thu/thu/chứng từ/thanh toán/khớp/ghi nhận AP·AR/mở chốt. Lỗi tích hợp che secret; `job-health` đưa hành động gợi ý; `/metrics` Production cần `Metrics:ScrapeToken` hoặc 404. 09B DEV Status điền DONE + evidence. ADR-0030.

### Files / API / schema
- Migration `20260922085232_PolicyRegistryPackageH` → `policies`.
- `POST /api/policies/ensure-catalog`, `GET/PUT /api/policies`, `GET /api/policies/{key}/active`.
- `IIdempotencyGate` + scopes; `SecretRedactor`; `GET /api/integration-errors/job-health`.
- MetricsProtectMiddleware; gauges `lcms_outbox_pending` / `lcms_integration_errors_*`.
- 09B xlsx cập nhật; `HardeningPackageHTests`.

### UI
Hub cài đặt thêm tab Sổ chính sách; Tích hợp API hiện tình trạng job + gợi ý xử lý.

## 2026-09-22 — Gói G: chốt kỳ và báo cáo

### User
làm tiếp gói G mức Pixel-perfect

### Answer
Gói G (W-G1…G7) xong. Chốt bị chặn khi còn ngoại lệ nghiêm trọng (kể cả chờ miễn), phân bổ/chia doanh thu chưa chốt, tiền thanh toán/thu chưa gán vượt ngưỡng, chứng từ chưa khớp, hoặc công nợ mở. Miễn đã duyệt ghi vào snapshot. Chứng từ trễ trọng yếu vào kỳ đã khóa bắt mở lại; bản chốt cũ không bị viết lại. Hub báo cáo hỏi maturity và as-of tường minh; danh mục chỉ trỏ màn thật (lãi, Bill, chi phí, tuổi nợ, tiền, ngoại lệ, chốt). Tuổi nợ tại as-of không tính thanh toán/thu sau mốc. Bucket tuổi nợ cấu hình được trong `FinancialJson`. Xuất aging ghi audit. Chưa bấm từng nút trên trình duyệt vì không có phiên đăng nhập local.

Gói H chưa làm.

### Files / API / schema
- Close eligibility: open allocations, unallocated cash, waiting critical, waiver metrics in snapshot.
- `ILateDocumentGate` trên nhận chứng từ.
- `GET /api/reports/cash-settlement?asOf=`.
- Aging as-of settlement + tenant bucket days; `report.export` audit.
- ADR-0029.
- UI: `/reports` (maturity/as-of + catalogue), `/reports/cash`, lede chốt, bảng miễn trên chi tiết chốt.

### UI
Không thêm biểu đồ giả hay Excel. Export aging vẫn qua màn tuổi nợ hiện có.

## 2026-09-22 — Gói F: chứng từ, đối soát, phê duyệt

### User
làm tiếp gói F, vẫn mức Pixel-perfect

### Answer
Gói F (W-F1…F7) xong. Khớp tự động chỉ chọn khi một ứng viên có chênh lệch nhỏ nhất. Hai ứng viên cùng mức trả `MATCH_AMBIGUOUS`, không lưu. Người dùng vẫn chọn tay. Dòng khớp lưu `matched` hoặc `matched_with_tolerance` và ngưỡng đã dùng. Tất toán khác tiền tệ chỉ khi có tỷ giá đã lưu; thiếu tỷ giá vẫn `Không phân bổ khác tiền tệ (C-014).` Số tiền thanh toán/thu giữ nguyên tiền gốc. Số áp vào AP/AR là số đã quy đổi. Không sửa chi phí hay doanh thu vì chênh lệch tỷ giá. Ngoại lệ nghiêm trọng khi miễn chuyển `waiting` và mở phê duyệt; mức khác miễn ngay. Duyệt xong thì `waived`. Người tạo không tự duyệt khi có user. Đổi số tiền sau yêu cầu thì `needs_rereview`. Chạy lại đối soát tạo phiên nháp mới, phiên đã chốt giữ nguyên. Header `Idempotency-Key` trên nhận chứng từ và tạo thanh toán chỉ tạo một bản ghi. Sổ chính sách đầy đủ và sổ idempotency rộng vẫn là gói H.

Menu kiểm soát giữ các màn đang có. Hàng đợi ngoại lệ đọc “Chênh lệch & Ngoại lệ”, phê duyệt đọc “Phê duyệt chứng từ”. Hàng đợi chênh lệch vẫn riêng vì chênh lệch không phải ngoại lệ. Không thêm Danh sách công việc, Báo cáo kiểm soát, biểu đồ giả, hay Excel. Nút Miễn gọi API thật. Chưa bấm từng nút trên trình duyệt vì không có phiên đăng nhập local.

Gói G chưa làm.

### Files / API / schema
- Migration `20260922073936_ControlMatchFxApprovalIdempotency`: `document_match_details.outcome_code`, `applied_tolerance`; snapshot FX trên `payment_allocations` và `collection_allocations`; `approvals.object_fingerprint`; bảng `idempotency_records`.
- `POST /api/document-matches/{id}/resolve`.
- `POST /api/exceptions/{id}/waive`.
- `POST /api/reconciliations/{id}/replay`.
- `Idempotency-Key` trên `POST /api/financial-documents` và `POST /api/payments`.
- ADR-0028.
- UI: lede `/control`, nhãn nav, trạng thái `waiting` / `waived` / `needs_rereview`, nút Miễn.

### UI
Tổng quan kiểm soát vẫn là số đếm thật. Hàng đợi ngoại lệ có Miễn. Không thêm cột ngày phát sinh vì API chưa trả ngày tạo.

## 2026-09-22 — Gói E: chia doanh thu và lãi gộp theo maturity

### User
tiếp theo làm phần nào? triển khai Pixel-perfect luôn nhé

### Answer
Phần tiếp theo là gói E (W-E1…E5). Một doanh thu kinh tế chia nhiều Bill, tổng dòng bằng số gốc, phần dư trên Bill cuối, chốt thì bất biến, chia lại thì thay phiên cũ. Lợi nhuận theo một lớp: Dự kiến, Đã xác nhận, hoặc Thực tế. Không cộng hai lớp. Doanh thu 0 thì tỷ suất là N/A, lãi gộp vẫn hiện. Đổi về một tiền báo cáo thì kèm trace tỷ giá; thiếu tỷ giá thì không cộng USD với VND. Tổng hợp theo khách, dịch vụ, phương thức, tuyến, chuyến — chuyến là tổng Bill, không phải sổ lãi riêng. Nguồn ngoài đang sở hữu doanh thu thực tế thì từ chối `RV-06`, trừ khi có lý do ghi đè.

Menu: Danh sách doanh thu, Tạo doanh thu, Báo cáo doanh thu. Không thêm Đối soát vì chưa có luồng. Chưa bấm từng nút trên trình duyệt vì không có phiên đăng nhập local.

Gói F chưa làm.

### Files / API / schema
- Migration `20260922061634_RevenueMappingProfitability`: `revenue_mappings`, `revenue_mapping_details`.
- `POST /api/revenues/{id}/mappings`, `POST /api/revenue-mappings/{id}/finalize|cancel`.
- `GET /api/bills/{id}/profitability?view=&reportingCurrency=` thêm `marginRate`, trace FX.
- `GET /api/profitability/bills`, `GET /api/profitability/groups?groupBy=&view=`.
- Tạo doanh thu nhận `actualRevenueOwner`. Thực tế hóa nhận `sourceSystem`, `overrideReason`.
- Quyền `revenue.mapping.override`.
- ADR-0027.
- UI: `/revenues`, `/revenues/new`, `/revenues/report`, form chia trên `/revenues/{id}`.

### UI
Bảng Bill: số bill, khách, tuyến, loại DV, ba lớp doanh thu, lợi nhuận, tỷ suất, trạng thái. Báo cáo là bảng nhóm, không biểu đồ giả.

## 2026-09-22 — Gói D: cơ sở phân bổ, phạm vi Bill, dư làm tròn

### User
tiếp tục làm pixel-perfect gói D

### Answer
Gói D (W-D1…D6) xong. Cơ sở thêm kg thực, trọng lượng tính cước, CBM, số kiện, TEU, phần trăm và số tiền. Equal, quantity, manual ratio giữ. Số đo lấy từ operational measurements; tổng cơ sở bằng 0 trả `ZERO_ALLOCATION_BASIS`, không chia đều. Phần trăm phải cộng đủ 100. Số tiền tay phải khớp số cần phân bổ. Làm tròn theo Bill id, dòng dương cuối nhận phần dư, tổng dòng bằng số gốc. Phạm vi explicit, chặng, chuyến hoặc điều kiện dịch vụ: không gán mọi Bill liên kết khi rule giới hạn. Sửa kết quả tự động cần quyền `cost.allocation.override`, lý do, và lưu số trước/sau. Trạng thái: nháp, đã tính, chờ duyệt, đã chốt, đã hủy, đã thay. Chốt vẫn bất biến; phân bổ lại thì supersede. Tách người tạo và người duyệt (W-F5) chưa làm — người tạo vẫn chốt được.

Menu Chi phí chỉ gắn ba mục có màn thật: Danh sách chi phí, Tạo chi phí, Phân bổ chi phí. Không thêm Duyệt, Đối soát, theo Shipment, theo Chuyến, Báo cáo vì chưa có luồng. Màn phân bổ hiện Đối tượng, Mã, Số tiền, Tỷ lệ, Dư làm tròn và nút Tính phân bổ, Gửi duyệt, Hủy phiên, Chốt. Chưa bấm từng nút trên trình duyệt vì không có phiên đăng nhập local.

Gói E chưa làm.

### Files / API / schema
- Migration `20260922053957_AllocationBasesScopeRounding`: `cost_allocations.scope_id`, `condition_code`; `cost_allocation_details.override_before_amount`.
- `POST /api/costs/{id}/allocations` nhận applicabilityMode, scopeId, conditionCode.
- `POST /api/cost-allocations/{id}/calculate|submit|cancel` (BFF cùng đường).
- Quyền `cost.allocation.override` cho Financial Controller và Cost Accountant.
- ADR-0026.
- UI: `/costs` lede theo UI-04; `/costs/shared` và `/costs/shared/{id}`.

### UI
Ba nhãn nav có trang. Form chọn mười cơ sở. Phiên đang mở chặn phiên nháp thứ hai. `next build` xanh. Chưa click trình duyệt.

## 2026-09-22 — Gói C: sáu mode tính giá, snapshot, phụ phí, so sánh

### User
làm tiếp gói C, mức Pixel-perfect

### Answer
Gói C (W-C1…C11) xong. Tính giá lắp Rating Context rồi lưu snapshot bất biến: sửa Bill sau đó không đổi kết quả cũ. Trọng lượng tính cước air lấy max(thực, thể tích × 167), biển lấy W/M; ghi đè số đã xác nhận cần lý do và quyền `rate.quantity.override`. Sáu mode: fixed, unit_rate, weight_break_pivot, weight_step, container_rate, composite. Quy tắc không có mã phí vẫn cộng hết như Sprint 3. Cùng mã phí thì quy tắc cụ thể hơn thắng; hòa thì `AMBIGUOUS_RATE_RULE`; không khớp thì `NO_APPLICABLE_RATE`, không ghi 0. Chỉ phiên bản Published còn trong ngày hiệu lực được chọn. FX lưu nguồn, ngày, tỷ giá, số gốc và số đã làm tròn; cùng tiền tệ thì tổng không đổi. So sánh giá không ghi Rating. Nhập bảng giá xem trước, chặn mã trùng và bậc chồng, commit tất cả hoặc không ghi gì, phiên bản mới là nháp. Quyền xem/sửa/phát hành tách giá mua và giá bán.

Menu Bảng giá & Tính giá có đủ bảy màn mockup UI-03: danh sách (cột mã, tên, loại giá, hãng, tuyến, hiệu lực, trạng thái, KPI, tính giá nhanh), tính giá, so sánh, phụ phí, tỷ giá, phụ lục, lịch sử. Chưa bấm từng nút trên trình duyệt vì không có phiên đăng nhập local.

### Files / API / schema
- Migration `20260922045304_RatingModesChargeableFx`: cột rating/rule/rate card, bảng `rate_breaks`, `container_rates`.
- `POST /api/ratings` nhận ngày, điểm đi/đến, phương thức, hàng, kg, CBM, lý do ghi đè, tiền đích, container.
- `POST /api/ratings/compare`, `POST /api/rate-imports/preview|commit`, `GET /api/surcharges`, `/api/rate-appendices`, `/api/rating-history`.
- UI: `/rate-cards`, `/rate-cards/rate`, `/compare`, `/surcharges`, `/fx`, `/appendices`, `/history`.
- ADR-0025.

## 2026-09-22 — Gói B: đo lường, hàng hóa, sở hữu trường, nhập liệu

### User
làm tiếp gói B ở mức pixel-perfect

### Answer
Gói B (W-B1…B9) xong. Form tạo Bill, đơn hàng và Shipment giữ đúng các mục mockup UI-02. Loại hàng là danh sách canonical. Dưới trọng lượng tính cước có dòng “Có thể được hệ thống tính theo cấu hình nghiệp vụ.” Mô tả hàng hóa nằm trong khối hàng của Bill và đơn hàng. Ô lý do ghi đè nằm sau khối đó.

Số đo (kiện, kg, CBM, trọng lượng tính cước, container, TEU) thành dòng `operational_measurements`. Trọng lượng tính cước đã xác nhận không đổi nếu số mới khác mà không có lý do. Kiện và container là bản ghi con, không vẽ lưới dòng trên màn tạo. Trường `origin_code` do hệ thống ngoài ghi lần đầu thì LCMS sửa phải có lý do; lưu lại mà không đụng trường đó thì không chặn. Nhập tệp: xem trước từng dòng, có lỗi thì không ghi gì; mã ngoài trùng thì cập nhật. Liên kết Order–Bill, Bill–Shipment, Bill–chặng, Bill–chuyến, chặng–chuyến ghi audit `link.create` khi tạo mới.

Gói C chưa làm.

### Files / API / schema
- Bảng: `operational_measurements`, `cargo_packages`, `cargo_containers`, `field_ownerships`
- Cột Bill: `bill_date`, `service_type_code`, `incoterm_code`, `preferred_currency`, `commodity_type_id`, `master_bill_no`, `rate_date_policy`, `vendor_party_id`
- Cột Order: `order_date`, `service_type_code`, `incoterm_code`, `requested_at`, `commodity_type_id`, `contact_name`, `contact_channel`, `pickup_place`, `delivery_place`
- Cột Shipment: `service_type_code`, `commodity_type_id`, `carrier_name`, `carrier_party_id`, `origin_location_id`, `destination_location_id`, `route_id`
- Cột chặng: `sequence_no`, `origin_code`, `destination_code`, location ids
- Cột chuyến: `movement_on`, `carrier_party_id`, `transport_mode`, location ids
- Migration `20260922041527_OperationalCargoAndFieldOwnership`
- API: `POST /api/operational-import/preview`, `/commit`, `/packages`, `/containers`
- ADR-0024
- UI: `CreateCargoFields` — select loại hàng, gợi ý trọng lượng tính cước, xác nhận, lý do ghi đè

### UI
Ba màn `/bills/new`, `/orders/new`, `/shipments/new`. Không thêm lưới kiện. Chưa bấm được trên trình duyệt (cần đăng nhập); `next build` là bước kiểm UI.

---

## 2026-09-22 — Gói A: địa điểm, tuyến, loại hàng, vai trò Bill, snapshot đối tác

### User
Triển khai làm lần lượt từng phần. Làm đầy đủ, chi tiết nhất cho từng chức năng. Không làm lát cắt mỏng đủ vận hành.

### Answer
Gói A (W-A1…A6) xong. Địa điểm canonical (loại, IATA, UN/LOCODE, alias), tuyến (điểm đi ≠ điểm đến, điểm trung gian), loại hàng (cờ DG/lạnh/quá khổ/quá tải/giá trị cao, loại cha). Bill gắn payer / shipper / consignee / bill_to. Chính sách thuê bao chọn vai trò bắt buộc và cho phép vãng lai. Snapshot đóng băng tên đối tác; sửa master không viết lại dòng cũ. Khi thuê bao đã có địa điểm, mã điểm không khớp bị từ chối; chưa có địa điểm thì vẫn nhập tự do. A5 (gợi ý đối tác) giữ nguyên.

Gói B trở đi chưa làm. Test host tắt outbox worker nền để lệnh process-once không bị worker lấy mất message.

### Files / API / schema
- Bảng: `locations`, `location_aliases`, `routes`, `route_stops`, `commodity_types`, `operational_party_snapshots`
- Cột Bill: `payer_party_id`, `shipper_party_id`, `consignee_party_id`, `bill_to_party_id`, `origin_location_id`, `destination_location_id`, `route_id`
- Cột Order: `origin_location_id`, `destination_location_id`, `route_id`
- `tenant_settings.bill_party_policy_json`
- Migration `20260922021952_CanonicalReferenceMasters` (copy `master_catalog` kind location / transport_route)
- API: `PUT/GET /api/locations` (+ `/resolve`), `/api/routes`, `/api/commodities`, `GET/POST /api/party-snapshots`, `GET/PUT /api/bill-party-policy`
- ADR-0023
- UI: `/admin/locations`, `/admin/routes`, `/admin/commodities`, snapshot trên Bill, chính sách ở Cấu hình nghiệp vụ, form tạo Bill gửi vai trò + tuyến danh mục

### UI
Tạo Bill: chọn khách / bên trả tiền / người gửi / người nhận / bên nhận hóa đơn; chọn tuyến thì điền điểm đi và điểm đến. Snapshot đối tác trên tab tổng quan Bill.

---

## 2026-09-22 — UI-02: danh sách Shipment khớp mockup workspace

### User
làm luôn trang danh sách shipment theo mockup `@docs/po/Mockup html/LCMS_UI02_Don_hang_van_chuyen_Danh_sach_Bill_Mockup.html`

### Answer
`/shipments` dùng cùng khung Bill/Order: lọc có nhãn (tìm, từ/đến ngày, trạng thái, tuyến — **không** dropdown khách vì Shipment không có customer), 4 KPI icon, lưới bảng + panel chi tiết, CSV, widget chưa gắn Bill + ETD 14 ngày. Panel: Tổng quan / Bill liên kết / Chặng. Chọn dòng = shallow URL. Không checkbox bulk, không % kỳ trước, không cột tiền giả.

### Files / API
- `apps/web/app/shipments/page.tsx`, `ShipmentListWorkspace.tsx`
- BFF `GET /bff/shipments/:id` → `/api/shipments/:id`
- `fetchShipmentClient` trong `operational-refs-client.ts`

### UI
CTA **Tạo Shipment**. Empty/error thật. Xuất = CSV.

---

## 2026-09-21 — Client navigation: không reload, không nhảy về đầu trang

### User
khi click vào link, bill hoặc menu trái thì trang reload và hiện lại từ đầu trang, cái này gọi là gì, làm sao để chuyển mượt hơn, không load lại và không nhảy nội dung lên đầu trang từ đầu

### Answer
Đó là **full-page navigation / RSC refetch + scroll restoration** (cảm giác F5), không phải SPA client-side routing. Click Bill đổi `?selected=` gọi `router.replace` → Next tải lại server page, `loading.tsx` vẽ skeleton cả sidebar, window scroll = 0. Menu trái remount `AppShell` vì chrome nằm trong từng `page.tsx`.

Cách làm: (1) chọn dòng Bill/đơn = **shallow URL** (`history.replaceState`, không RSC); (2) **persistent shell** trong `app/layout.tsx` khi đã đăng nhập — sidebar không tháo; `AppShell` trên page chỉ passthrough; (3) `loading.tsx` chỉ skeleton vùng nội dung; (4) FilterBar / phân trang dùng `router.push(..., { scroll: false })` thay vì GET form / `window.location`.

Click sang module khác vẫn cuộn lên đầu **trang mới** (đúng SPA); sidebar và topbar đứng im.

### Files / API
- `apps/web/app/layout.tsx` — `PersistentShell` khi có cookie
- `apps/web/components/AppShell.tsx` — `persist` mới vẽ chrome; page wrapper passthrough + `PageTopbar`
- `BillListWorkspace` / `OrderListWorkspace` — `replaceSearchShallow`
- `NavLink` / `NavGroup` — active theo `usePathname`
- `FilterBar`, `ListPagination`, `QuerySelectLink`, `loading.tsx` content-only
- Không đổi API/schema

### UI
Sidebar không nháy. Click Bill chỉ đổi panel phải, URL `?selected=` vẫn share được.

---

## 2026-09-21 — UI-02: danh sách Bill / đơn hàng khớp mockup workspace

### User
danh sách bill và danh sách đơn hàng chưa giống mockup `@docs/po/Mockup html/LCMS_UI02_Don_hang_van_chuyen_Danh_sach_Bill_Mockup.html`

### Answer
`/bills` và `/orders` dùng cùng khung mockup: thanh lọc có nhãn (tìm, từ/đến ngày, trạng thái, tuyến, khách), 4 KPI icon trái, lưới 2 cột (bảng + panel chi tiết dính phải), xuất CSV từ dòng đã lọc, widget dưới (hạn ETD thật / top khách theo doanh thu best-available; đơn: chưa gắn Bill + ETD). Không làm checkbox bulk, “Bộ lọc khác”, % so kỳ trước, hay “Xem tất cả”. Chọn dòng mở panel; đóng panel không giả Excel.

### Files / API
- Pages: `apps/web/app/bills/page.tsx`, `apps/web/app/orders/page.tsx`
- Workspace: `BillListWorkspace`, `OrderListWorkspace`, `list/FilterBar` `showLabels`, `StatCardGrid` icon
- CSS: `.list-workspace`, `.list-detail-panel`, `.stat-kicon`, `.filter-bar-labeled`
- List DTO: `BillListItemDto` thêm `EtdAt`/`EtaAt`; BFF `GET /bff/orders/:id`
- Client `OrderListWorkspace` không import `operational-refs` (kéo `next/headers`) — CI web build.

### UI
Copy tiếng Việt. Empty/error thật. CTA **Tạo Bill / Tạo đơn hàng**. Xuất = CSV. Hạn = ETD trong 14 ngày.

---

## 2026-09-21 — Checkbox form tạo đơn: cùng cỡ native, không ô vuông to

### User
Form tạo đơn hàng vì sao có vài checkbox lớn hơn các checkbox khác

### Answer
`.cw-field input` gắn `width:100%` + `min-height:38px` + border/padding cho **mọi** input, kể cả checkbox. Nhãn ngắn (DG, Pickup) co về cỡ native; nhãn dài (Hàng lạnh, Hải quan, Tự động tạo mã) nở thành ô vuông. Loại checkbox/radio khỏi chrome ô chữ; `.create-checks` khóa 1rem.

### Files
- `apps/web/app/globals.css`
- `.cursor/rules/ux-ui.mdc`

---

## 2026-09-21 — Full slice vận hành + dữ liệu mẫu ≥120 / loại

### User
Kiểm tra toàn hệ thống, phần nào hiện đang làm lát cắt mỏng thì triển khai làm full chức năng. Xong tạo dữ liệu mẫu để test, mỗi loại >100 record

### Answer
API P01–P25 / Pass 2 đã đủ cho go-live tài chính. Phần còn mỏng là **chi tiết ngữ cảnh** Order/Bill/Shipment (mới tạo đủ form UI-02 nhưng màn xem chỉ còn identity). Đã hiện đầy đủ tuyến, ETD/ETA, hàng hóa, dịch vụ thêm. Dữ liệu demo 12 Bill không đủ test list — thêm catalog **VOL-** ≥120 mỗi loại (khách, NCC, đơn, Bill, Shipment, chặng, chuyến, chi phí, doanh thu, chứng từ, AP, AR, thanh toán, thu, bảng giá, sao kê, địa điểm, tuyến). Idempotent. Tạo từ **Cài đặt → Dữ liệu mẫu** (`POST /api/sample-data/ensure`, quyền `settings.manage`). Tạo record mẫu **không** sinh Cost/Revenue từ chứng từ.

### Files / API / schema
- `src/LCMS.Application/Demo/DemoVolumeCatalogSeeder.cs` — TargetCount=120, prefix `VOL-`
- `GET /api/sample-data`, `POST /api/sample-data/ensure`
- UI: `/settings/sample-data`, `OperationalContextGrid` trên Bill + chi tiết tham chiếu
- Tests: `DemoVolumeCatalogTests`

### UI
Chi tiết đơn/Bill/Shipment hiện ngữ cảnh vận hành. Nút **Tạo dữ liệu mẫu**. Empty/error thật.

---

## 2026-09-21 — UI-02: danh sách đơn hàng + tạo Bill / Order / Shipment

### User
`@docs/po/Mockup html` mockup html phần ds đơn hàng, tạo bill, order, shipment

### Answer
Đã đưa mockup UI-02 vào sản phẩm: danh sách đơn hàng `/orders`, tạo đơn `/orders/new`, tạo Bill `/bills/new`, danh sách + tạo Shipment `/shipments`. Order/Shipment vẫn là tham chiếu vận hành (C-002, ADR-0017); không TMS/GPS/e-POD. Form lưu ngữ cảnh rating (khách, tuyến, ETD/ETA, hàng hóa) trên cột + `context_json`. Tạo record **không** sinh Cost/Revenue/AP/AR. Upsert identity TMS không xóa context LCMS (`ApplyContext`). List API sort `CreatedAt` in-memory vì SQLite test không `ORDER BY DateTimeOffset`.

### Files / API / schema
- Nav: `apps/web/components/AppShell.tsx` — 6 mục con + Chặng & Chuyến
- Pages: `apps/web/app/{orders,shipments}/**`, `apps/web/app/bills/{page,new/page}.tsx`
- Forms: `CreateOrderForm`, `CreateBillWorkspaceForm`, `CreateShipmentForm`
- API: `PUT /api/orders`, `GET /api/orders`, `PUT /api/shipments`, `GET /api/shipments`, `PATCH /api/bills/{id}/context`
- Migration: `20260921140450_UI02_OperationalCreateContext`
- ADR: `docs/adr/ADR-0022-ui02-order-bill-shipment-create.md`
- Tests: `tests/LCMS.Api.Tests/Ui02OperationalCreateContextTests.cs` + Sprint2 list orders

### UI
Copy tiếng Việt CP6.5. CTA **Tạo đơn hàng / Tạo Bill / Tạo Shipment**. Empty/error thật. Nháp = `draft`, dùng để liên kết = `active`.

---

## 2026-09-21 — Menu ngang: submenu overlay, không cắt/cuộn trong header

### User
giao diện menu ngang mở menu ngang bị cắt mất, dường như header và thân trang tách biệt nên header chưa menu bị scroll khi có menu con

### Answer
`overflow-x: auto` trên `.sidebar-nav-scroll` khiến `overflow-y` thành `auto` (spec) → header thành scrollport, cắt dropdown. Đổi overflow visible; `.nav-group-children` luôn trong DOM, hiện hover/focus-within (không bung hết group `is-open`/`openByDefault`). Mobile drawer vẫn accordion theo click.

### Files
- `apps/web/app/globals.css`
- `apps/web/components/NavGroup.tsx`

---

## 2026-09-21 — Menu ngang: thanh top nav, không còn phủ trắng che trang

### User
chọn cấu hình giao diện menu ngang bị lỗi hiển thị

### Answer
`data-layout=horizontal` vẫn giữ `.nav-item { width:100%; color: sidebar-ink trắng }` + footer cảng → menu xếp dọc full-width, chữ trắng trên nền trắng. Override: item `width:auto` + màu topbar, group dropdown, ẩn ảnh footer, nav cuộn ngang. Dashboard nhìn thấy dưới thanh.

### Files
- `apps/web/app/globals.css`

---

## 2026-09-21 — Trang Người dùng: bảng không còn vỡ trong lưới 50/50

### User
lỗi hiển thị giao diện trang người dùng

### Answer
`/settings/users` nhét bảng 6 cột (form trong ô) cạnh form tạo → cell hẹp, nút/input quấn. Danh sách full width (`stack-panels`), form tạo dưới (`.form-aside`). CSS: bảng có `.field` không chia 50/50; ô form gọn; nút `nowrap`.

### Files
- `apps/web/app/settings/users/page.tsx`
- `apps/web/components/UserAccountTable.tsx`
- `apps/web/app/globals.css`
- `.cursor/rules/ux-ui.mdc`

---

## 2026-09-21 — UI-13/UI-14: người dùng, doanh nghiệp, danh mục, audit, license, thông báo, sao lưu

### User
làm lần lượt từ 1 đến 5, đầy đủ chi tiết như 1 hệ thống chuẩn, không lát mỏng, không sơ sài tạm đủ dùng. làm xong kiểm tra deploy luôn nhé.

### Answer
Đã làm đủ 5 hạng mục System/Danh mục (ADR-0021), không control giả.

1. **Người dùng** `/settings/users`: tạo + mật khẩu bắt buộc (10 ký tự, chữ+số), đặt lại mật khẩu (thu hồi refresh token), ngừng/mở, chặn ngừng Admin cuối, chỗ license.
2. **Doanh nghiệp** `/settings/company`: MST, địa chỉ, IANA timezone (chỉ hiển thị; sổ UTC), tiền tệ mặc định, logo PNG/JPEG/WebP ≤512KB.
3. **Danh mục** tab UI-13: khách/NCC, dịch vụ, loại CP/DT, tuyến, phương thức, cảng/sân bay/cửa khẩu (`attributes_json`), tiền tệ/tỷ giá, danh mục khác + tổ chức.
4. **Nhật ký** `/settings/audit`: lọc hành động/đối tượng/ngày, phân trang, JSON trước/sau. Quyền `audit.read`.
5. **License / thông báo / sao lưu**: chỗ+module server-enforced (H-001: tắt chỉ ẩn UI); in-app + email outbox, SMTP chưa cấu hình = “chưa gửi”; backup logic danh mục/cấu hình, confirm `RESTORE {tenantCode}`, không PITR/sổ tiền.

Push `23cc254` rồi CI **test fail** (`InactiveUser_Denied_EvenWithRole` ngừng Admin cuối → 409). Đã giữ thêm Admin keeper rồi push lại.

### Files
- `docs/adr/ADR-0021-tenant-admin-license-backup-notifications.md`
- `src/LCMS.Api/Endpoints/TenantAdminEndpoints.cs`, `IdentityEndpoints.cs`
- `src/LCMS.Application/{Users,Tenants,Licenses,Notifications,Backups,Catalog}`
- `src/LCMS.Infrastructure/Persistence/Migrations/20260921034927_P28_TenantAdminSettingsFull.cs`
- `apps/web/app/settings/{users,company,audit,license,notifications,backup,business,integrations}/page.tsx`
- `tests/LCMS.Api.Tests/AdminSettingsFullTests.cs`

---

## 2026-09-21 — Deploy UI chrome (sidebar, KPI, tab, filter)

### User
deploy

### Answer
Push `main` (5 commit UI: sidebar footer, StatCardGrid tone, ops tabs, party KPI, primitive containment). GitHub Actions `CI` test → deploy `/opt/cms`. Không scp. Không ghi đè `infra/.env`.

### Commits
- `08ef47c` sidebar footer thấp hơn
- `98c2c31` KPI tone nền cả card
- `777ac42` tab vận hành + ô tìm
- `314ee4d` KPI đối tác StatCardGrid
- `4bce613` khóa primitive CSS + ux-ui.mdc

---

## 2026-09-21 — Sửa UI hàng loạt: khóa primitive KPI / tab / ô tìm

### User
Sao UI nhiều lỗi vặt? Có cách sửa hàng loạt một lần trên toàn hệ thống không?

### Answer
Có. Lỗi vặt vì 4–5 class làm cùng một việc; reskin form/dashboard đè class list (`.po-kpi` 124px, `.filter-bar` mất flex). Đã: (1) `po-kpi` 124px chỉ khi `.po-card`; `.po-kpi:not(.po-card)` + `.po-kpi-row` thành card gọn; (2) `.search-bar`/`.filter-bar` cùng hợp đồng flex; (3) `stat-grid` `auto-fit`; (4) rule `ux-ui.mdc` — list KPI = StatCardGrid, dashboard = po-card po-kpi. Không push/deploy.

### Files
- `apps/web/app/globals.css`
- `apps/web/app/operations/page.tsx`
- `.cursor/rules/ux-ui.mdc`

---

## 2026-09-21 — KPI đối tác: hết dùng po-kpi dashboard (bung dọc)

### User
Các ô card và Tổng hồ sơ / Đang dùng / Ngừng / Bị chặn giao dịch bị lỗi hiển thị.

### Answer
Trang `/admin/parties` gắn `po-kpi` (dashboard: flex ngang + min-height 124px) nhưng không có `.po-kpi-row` / `po-card` — bốn số xếp dọc, khoảng trống lớn, đẩy thanh lọc xuống đáy. Đổi sang `StatCardGrid` (cùng KPI thanh toán). `.filter-field` flex để ô lọc không tràn. Không push/deploy.

### Files
- `apps/web/app/admin/parties/page.tsx`
- `apps/web/app/globals.css`

---

## 2026-09-21 — Tab tham chiếu vận hành + ô tìm không đè nút

### User
Chỉnh lại các ô Đơn hàng / Lô hàng / Chặng / Chuyến cho đẹp.

### Answer
`hub-module-tab` chỉ có nhãn: căn giữa, cao ~41px, 4 cột đều; tab có mô tả giữ layout cũ. `.filter-bar` thành hàng flex — input và nút Tìm không còn đè nhau. Không push/deploy.

### Files
- `apps/web/app/globals.css`
- `apps/web/app/operations/page.tsx`

---

## 2026-09-21 — KPI card: nền cả khối theo màu ô vuông tone

### User
Các khối KPI có ô vuông nhỏ góc phải — cho màu nền toàn khối như màu ô đó.

### Answer
Ô vuông là chip `tone` trên `StatCardGrid` (xanh `#e6f4ff` / đỏ `#ffccc7`…). Bỏ chip; gán class tone lên cả card. Cùng bộ KPI (thanh toán, AP/AR, chứng từ…). Không push/deploy.

### Files
- `apps/web/components/list/StatCardGrid.tsx`
- `apps/web/app/globals.css`

---

## 2026-09-21 — Sidebar footer ảnh cảng thấp lại, nhường chỗ menu

### User
Phần hình ảnh cố định dưới menu quá cao làm giới hạn tầm nhìn menu. Trong chat này không deploy.

### Answer
Footer sidebar bỏ `padding-top: 130px` + `min-height: 205px` (ảnh cảng chiếm ~280px cùng tagline/logout). Ảnh giữ làm nền khối nội dung gọn (~137px): slogan, version, nút thu gọn, Đăng xuất. Menu nhìn thêm ~3 mục trước khi phải cuộn. **Không push/deploy** theo yêu cầu lượt này.

### Files
- `apps/web/app/globals.css` — `.sidebar-footer`, `.sidebar-footer-visual`, `.sidebar-tagline`, `.sidebar-logout`

---

## 2026-09-20 — Fix form layout: legend float tràn mép phải

### User
giao diện form bị lỗi rồi

### Answer
`fieldset.group-box > legend { float: left; width: 100% }` khiến browser đẩy nội dung form (Điện thoại, Website, VAT, tài chính…) thành dải dọc mép phải. Bỏ float; legend `display:block` + border-bottom trong card. Verify `/admin/parties/new`.

### Files
- `apps/web/app/globals.css`

---

## 2026-09-20 — Reskin toàn bộ form (bố cục 3 cột / card)

### User
bố cục và reskin lại tất cả các giao diện form cho đẹp và chuyên nghiệp hơn nhé

### Answer
Skin form dùng chung trong `globals.css` (mọi `form-grid` / `group-box` / `receive-form`): lưới mặc định **3 cột** (2 cột ≤1100px, 1 cột ≤520px) — hết hàng 5–6 ô 180px. Nhãn sentence-case, fieldset dạng card + tiêu đề gạch chân, input focus ring, checkbox/radio/địa chỉ kiểu chip, CTA dính đáy form. Vận đơn: 2 cột người gửi/nhận, lưới trong card 2 cột, viền accent trên. Không invent skin catalog thứ hai.

### Files
- `apps/web/app/globals.css` — form tokens + grid + waybill/address/checkbox/radio.
- `CreateBusinessPartyForm.tsx`, `EditBusinessPartyForm.tsx`, `PartyRolesPanel.tsx`, `VnAddressFields.tsx`, `CreateSharedCostForm.tsx` — `field-span` / `cols-2`.

### Verify
- `/admin/parties/new` và `/bills/new` trên production sau CI.

---

## 2026-09-20 — Deploy CI fail: client import `next/headers`


### User
deploy bị lỗi. Từ nay sau khi deploy thì đợi vài phút kiểm tra xem có lỗi hay không nhé.

### Answer
Job `test` pass; `deploy` fail lúc `docker compose --build` vì `next build`: `EditBusinessPartyForm` / typeahead import `@/lib/parties` (server, `cookies` từ `next/headers`). Host đã rsync source nhưng container **không** recreate (vẫn bản 22h). Sửa: `lib/parties-client.ts` cho fetch BFF; CI thêm `npm run build` trước deploy. Sau mỗi push: đợi run CI **success**, rồi mới tin `/health`.

### Verify
- Actions: https://github.com/thanhquyen129/CMS/actions/runs/35516034801 (fail)
- Fix follow-up run after this commit.

---

## 2026-09-20 — Module khách hàng–đối tác đầy đủ (D02 / ADR-0020)

### User
Phân tích và triển khai phát triển, hoàn thiện module khách hàng-đối tác. phiên bản đầy đủ nhất. Làm bản đầy đủ tính năng, không làm qua loa tối thiểu hoặc vừa đủ dùng.

### Answer
Không tách bảng Customer/Vendor. Một `BusinessParty` + `party_roles` (`customer`/`vendor`/`payer`/`payee`). Transactability: `IsActive` và không `IsBlocked` mới được gắn Bill / chi phí / doanh thu / chứng từ mới. Hạn mức AR theo ADR-0020: `advisory` | `warn` | `block` — chặn cứng chỉ khi **ghi nhận phải thu** cùng tiền tệ với hạn mức (không quy đổi FX im lặng; không chặn lúc tạo Revenue — C-004). Soft-delete từ chối khi còn AP/AR mở. View Cost ≠ View Revenue trên 360°.

### ADR / schema
- `docs/adr/ADR-0020-party-transactability-credit-control.md` — Accepted.
- Migration `20260920140048_P27_BusinessPartyDirectoryFull` — `party_kind`, `short_name`, `legal_type`, `group_code`, `external_code`, `industry_code`, `invoice_email`, `vat_registered`, `assigned_user_id`, `parent_party_id`, `credit_control_mode`, `is_blocked` + bank `bank_code`/`swift_bic`, contact `function_code`.

### API
- `GET /api/business-parties/lookup?q=&roleCode=&usableOnly=&take=` — typeahead mã/tên/MST/SĐT.
- `GET /api/business-parties/directory` + `/summary` — KPI + phân trang + AP/AR nếu có quyền.
- `GET /api/business-parties/duplicates?taxId=&phone=&email=&excludeId=`
- `GET /api/business-parties/export` — CSV UTF-8 BOM (tối đa 5000 dòng; AP/AR theo quyền).
- `GET /api/business-parties/{id}/financial` — 360° hạn mức, AP/AR, Bill, chứng từ.
- `POST /api/business-parties/{id}/block|unblock`
- Create tự cấp mã `DT-yyMMdd-xxxx` nếu để trống. MST VN 10/13 số, unique theo tenant.

### Web
- `/admin/parties` danh sách + KPI + lọc vai trò/trạng thái/loại/nhóm + xuất CSV.
- `/admin/parties/new` hồ sơ đầy đủ + cảnh báo trùng.
- `/admin/parties/{id}` tab: hồ sơ / tài chính / vai trò / TKNH / liên hệ / nhật ký + chặn giao dịch.
- Typeahead trên tạo Bill, chi phí (kể cả chi phí chung), doanh thu, nhận chứng từ.

### Tests
- `BusinessPartyDirectoryFullTests` + `BusinessPartyMasterFullTests` (lookup SĐT, chặn, NCC ngừng, hạn mức AR, role-gate chi phí, xóa mềm khi còn AR, export CSV).
- Full suite: 140 pass; 2 fail `SprintP10ReverseRecognizeTests` do SQLite file lock Windows (không liên quan module này).

### Follow-up
- Merge trùng party (P1 ADR-0019) — chưa làm; cảnh báo trùng MST/SĐT/email trên form.
- Gán người phụ trách (`assignedUserId`) chưa có UI chọn user.
- Import Excel danh sách đối tác — chưa.

---

## 2026-09-19 — PO: “thêm TMS” vì khách chưa có hệ thống (Word/Excel)


### User
Khách chưa có hệ thống, thao tác tay Word/Excel. Yêu cầu thêm TMS: quản lý khách hàng (autocomplete tên/SĐT khi tạo Bill); quản lý cước (mode air/sea/truck/train, loại hàng, chặng, giá Q, giá kg bước 0.5). Chuyên gia bổ sung điểm mấu chốt còn thiếu.

### Decision (proposed — ADR-0019)
Không mở TMS. Đây là **D02 Party + D04 Bảng cước** cho tenant standalone (ADR-0017). Excel cước của khách = SoT rating, không phải điều vận.
- Giữ H-002 / SCP-003 / ADR-0018: không GPS, xe, tài xế, e-POD, kho, matching xe trống.
- Party master đã có (`BusinessParty` + roles). Gap: tìm theo SĐT, typeahead trên form Bill, snapshot, bắt trùng MST/SĐT.
- Rating đã có (fixed / unit_rate / % / min-max, RouteCode free-text). Gap: Q-break + pivot, kg-step 0.5, catalog mode/loại hàng/chặng, chargeable weight round-up, buy≠sell, import Excel.

### ADR
- `docs/adr/ADR-0019-freight-tariff-not-tms.md` — **Proposed**, chờ PO chốt 3 câu.

### Blockers (hỏi PO)
1. Forwarder (bán + mua cước) hay tự vận hành (có xe)?
2. File Excel cước thật: Q-break hàng không, kg-step chuyển phát, hay đường bộ/biển theo chuyến/CBM?
3. Bill bắt buộc 3 bên (gửi / nhận / trả cước) hay 1 khách hàng đủ cho P0?

### Follow-up (sau khi PO chốt)
P0: typeahead Party trên Bill; catalog mode/cargo/lane; chargeable kg + round 0.5; calc `weight_break` + `weight_step`; import Excel; rate → Expected Revenue/Cost.
P1: volumetric, sea W/M, FSC/SSC, rate card theo khách, merge trùng party.

---

## 2026-09-19 — Waybill capture on Bill (not TMS)

### User
Nhờ chuyên gia tham khảo bill này và triển khai module TMS đầy đủ. (Ảnh vận đơn Vietnam Post EE5556576340VN)

### Decision
Không mở TMS (H-002 / ADR-0017 SCP-003). Vận đơn giấy = form nhập Bill neo tài chính (ADR-0018).
- Hồ sơ 1:1 `bill_waybills`: người gửi/nhận, kiện, cước, thu hộ, điểm chấp nhận.
- Dòng cước → Chi phí dự kiến (mặc định) hoặc Doanh thu dự kiến. Tổng giấy lệch dòng (56.700 vs 42.000+10.500) → phần dư vào Thu khác.
- Thu hộ ≠ doanh thu. Cước GET ẩn nếu thiếu CostRead/RevenueRead.
- Upsert Shipment mỏng + link. Không GPS / điều vận / e-POD.

### API
- `POST /api/bills/waybills` — tạo Bill + hồ sơ + Shipment + Expected costs
- `GET/PUT /api/bills/{id}/waybill`
- `GET /api/bills/{id}/financial-view` thêm `waybill`

### Web
- `/bills/new` — form vận đơn (mục 1–14). CTA: Lưu vận đơn
- Drawer Bill tab **Vận đơn**
- BFF: `/bff/bills/waybills`, `/bff/bills/{id}/waybill`

### Schema
- `bill_waybills` — migration `20260919160816_BillWaybillProfile`

### Tests
- `BillWaybillApiTests` (case VNPost EE5556576340VN) + full API **139 passed**
- `npm run build` OK

### Follow-up
- Import ảnh/OCR vận đơn
- Connector VNPost
- Gắn party master từ snapshot người gửi/nhận

---

## 2026-09-20 — UI-01 Dashboard V2 skin (system-wide + home 1:1)

### User
Áp skin `docs/po/LCMS_UI01_Dashboard_V2_Mockup.html` toàn hệ thống; trang chủ làm lại giống demo 100%.

### Done
- Token CSS V2: navy sidebar gradient, `#1677e8`, `#f5f8fc`, card `#dfe7f1`, semantic green/red/orange/purple.
- Shell: topbar full-bleed + hamburger, search mockup-style, footer ảnh logistics, nav active gradient.
- Dashboard `/dashboard`: welcome + quote, KPI 5 ô, việc cần xử lý 7 ô, chart tháng (tháng hiện tại = Best Available thật; không bịa chuỗi 12 tháng), Best Available, độ chín, Bill gần đây, chứng từ / AP-AR / thông báo — layout khớp mockup; số liệu từ API (Cost ≠ Revenue vẫn gate).
- “Tổng đơn hàng” = count tham chiếu Order LCMS (`/operations`), không phải TMS.

### Files
- `apps/web/app/globals.css`, `ShellChrome.tsx`, `GlobalSearch.tsx`, `NavIcon.tsx`, `app/dashboard/page.tsx`

### Follow-up
- Chuỗi CP/DT theo tháng khi API series có sẵn.
- Skin login page tinh chỉnh nếu PO muốn 1:1 luôn màn auth.

---




### User
Rà soát toàn bộ hệ thống vs yêu cầu PO; chức năng nào chưa có UI thì triển khai. Chuyên gia tự đề xuất khi gặp vấn đề.

### Decision (operable slice)
Không mở TMS / Budget-Forecast / OIDC. Làm P0 standalone (AC-SCP-06/07, UI-03/13, tìm kiếm):
- D03 Manual Reference Entry + LIST/DETAIL/RELATIONSHIP/CROSS-NAV cho Order/Lô/Chặng/Chuyến.
- D02 danh mục loại (cost/revenue/service/document/payment term) + UI tỷ giá (API sẵn).
- D04 thành phần quy tắc bảng giá + seed Doanh thu dự kiến từ rating (song song Chi phí dự kiến).
- Tìm kiếm toàn cục đa đối tượng (gated Cost ≠ Revenue).

### API
- `GET /api/orders|shipments|transport-legs|transport-movements` (+ `/{id}` với related Bills)
- `GET /api/search` (global); giữ `/api/search/operational`
- `GET/PUT /api/master-catalog`
- `POST /api/ratings/{id}/seed-expected-revenues`
- `PricingRuleDto.components` trên `GET /api/rate-versions/{id}/rules`

### Web
- `/operations` (+ `/operations/{orders|shipments|legs|movements}/{id}`)
- `/admin/catalog`, `/admin/fx-rates`
- Thành phần giá trên `/rate-cards/[id]`; seed doanh thu trên Bill rating panel
- Top-bar search typeahead; Bill drawer tab Liên quan cross-nav
- Middleware: mọi route app (trừ login/BFF/static) yêu cầu cookie — không sót `/operations` `/costs` `/rate-cards`

### Schema
- `master_catalog_items` — migration `20260919145247_D02_MasterCatalog`
- Permission `master.catalog.manage` (Admin + MasterData + FinancialController)

### Tests
- `StandalonePoUiGapTests` + full API suite 134 passed (SQLite: không ORDER BY DateTimeOffset trên search cost/revenue)
- `npm run build` (Next.js) — bắt buộc: CI #174 deploy fail vì `next build` (client import `next/headers` qua `catalog.ts` + JSX kề nhau ở `BillRatingPanel`)

### Ship
- Commit `01eb920` — API/UI slice. CI [run #174](https://github.com/thanhquyen129/CMS/actions/runs/35450577515): **test OK**, **deploy fail** (`Compose up` / `next build`).
- Follow-up commit: sửa build web + middleware; push lại để Actions deploy.

### Follow-up
- Import CSV/preview (AC-SCP-09) P1
- Unlink relationship
- Ngân sách/Dự báo (P13) khi PO mở hạng mục
- UAT 2 user Cost ≠ Revenue trên dashboard/aging

---

## 2026-09-18 — PO/BA confirm D03 relationships + TD2 integration + UI feasibility


### User
PO/BA thống nhất: Ops System = Operational SoT; LCMS ≠ TMS. D03 vẫn gồm Order/Bill/Shipment/Leg/Movement + relationships. Trước khi PO chốt phạm vi, xác nhận 3 điểm: (1) Relationship Matrix IMPLEMENTED/PARTIAL/MISSING; (2) extract/orchestrator/webhook/integration_records — bắt buộc TD2 baseline vs chỉ khi có Ops cụ thể; (3) UI LIST/SEARCH/DETAIL/RELATIONSHIP/CROSS-NAV khả thi trên API hiện có (không bàn CRUD Order/Shipment; chưa mở TMS).

### Answer (team)
1. **Matrix (domain+DB+link/graph API):** cả 6 quan hệ = **IMPLEMENTED** (`order_bill_links`, `bill_shipment_links`, `transport_legs.shipment_id`, `bill_leg_links`, `leg_movement_links`, `bill_movement_links`). Gap chung: không unlink; UI Bill drawer chỉ hiện Order/Shipment; entity thin (C-002).
2. **Integration:** TD2 baseline = E03 push upsert/link + C-002 + D12 `integration_records`/`errors` skeleton (+ outbox stub). **Không bắt buộc** extract / webhook / Ops orchestrator / broker đầy đủ cho đến khi PO chọn Ops SoT cụ thể.
3. **UI (trước Ops prod):** Bill-centric RELATIONSHIP VIEW **Ready** (`GET /api/bills/{id}/graph`). Order LIST/DETAIL Ready. Shipment/Leg/Movement LIST/DETAIL/SEARCH = cần thin Get/List/search (+ seed demo). Entity-centric CROSS-NAV = cần include related ids. **Không blocked** bởi Ops Sync.

### Files referenced
- `OperationalReferenceEndpoints.cs`, link entities, `GetBillGraphQuery`, Sprint2* tests
- D12: `IntegrationRecord`, `AuditIntegrationEndpoints`, Sprint 12 DoD
- UI: `BillFinancialDrawer` related tab; no `/orders|/shipments` pages
- Blocker Ops SoT: `docs/implementation-plan.md`

### Follow-up
PO/BA quyết định scope hoàn thiện (API list/get, Bill drawer legs/movements, search đa entity, demo seed) — chưa TMS.

---

## 2026-09-18 — PO Baseline_tra_bo_sung (Standalone Addendum) ingested

### User
@Baseline_fix_bo_sung PO trả lời trong folder này

### PO package
- `docs/po/Baseline_fix_bo_sung/LCMS_Standalone_Commercial_Product_Architecture_Addendum_v1.0.docx`
- `docs/po/Baseline_fix_bo_sung/LCMS_Standalone_Input_Source_Implementation_Fix_Matrix_v1.0.xlsx`
- Extract: `docs/reports/_extract_baseline_tra_bo_sung.txt`

### Decision (SCP-001…005)
- LCMS = **standalone commercial product**; thiếu connector không khóa core.
- Input channels: Manual / Import / API theo object + Tenant capability + SoT policy; cùng canonical command.
- D03: Manual Reference Entry + LIST/SEARCH/DETAIL/RELATIONSHIP/CROSS-NAV; **không TMS**.
- Derived/control ≠ free CRUD. External SoT chỉ khi Tenant cấu hình.
- P0: D02, D03, D04, D09 + shared input-source framework. AC-SCP-01…10 (DEV Status TODO).

### ADR
- `docs/adr/ADR-0017-standalone-commercial-product-input-channels.md`

### Implication vs prior H-002 note
- H-002 vẫn: Ops = Operational SoT khi tích hợp; LCMS ≠ TMS.
- **Narrow:** Order/Shipment Manual Reference Entry + read UI **vào baseline** (không còn “CRUD Order/Shipment out of scope” theo nghĩa khóa core).

### Next slice đề xuất
AC-SCP-06 P0: D03 List/Get/Search + Bill relationship UI + Manual Reference Entry (thin) — chưa import UX đầy đủ / chưa connector.

---

## 2026-09-18 — Export Word xác nhận D03/TD2/UI cho PO

### User
xuất câu trả lời sang file word

### Done
- Generator: `docs/reports/_gen_d03_td2_ui_confirm_docx.py`
- File: `docs/reports/CMS_XacNhan_D03_TD2_UI_2026-09-18.docx`

---

## 2026-09-17 — Fix CI: SQLite ORDER BY DateTimeOffset on Ratings

### User
CI run #169 failed (test ✕, deploy skipped) after UI-02 Bill Financial View.

### Done
- Root cause: `AttachFinancialSummariesAsync` / financial-view ordered Ratings by `CreatedAt` in SQL — SQLite rejects DateTimeOffset in ORDER BY → GET `/api/bills` 500 (5 tests).
- Fix: load then order in-memory in `GetBillByIdQuery.cs` and `GetBillFinancialViewQuery.cs`.
- Verified locally: BillListFinancialSummary + Sprint1/2 filters pass.

---

### User
triển khai làm đầy đủ chức năng theo hình (mockup UI-02 panel chi tiết Bill)

### Done
- **API:** `GET /api/bills/{id}/financial-view` — Bill + profile + graph + progress + costs/revenues/docs (gated CostRead/RevenueRead).
- **API:** `PATCH /api/bills/{id}/context` — customer/route/ETD/ETA/assignee/description/internalNote (`bill.update` hoặc `bill.create`).
- **Schema:** bills thêm CustomerPartyId, RouteCode, EtdAt, EtaAt, AssignedUserId, Description, InternalNote — migration `UI02_BillFinancialContext`.
- **List enrichment:** khách hàng, tuyến, cost E/C/A, counts; cột bảng khớp mockup.
- **Web:** `BillFinancialDrawer` master-detail: tab Tổng quan / Chi phí / Doanh thu / Chứng từ / Lịch sử / Liên quan; chỉ số maturity; tiến độ; hành động nhanh; lưu ghi chú.
- **BFF:** `/bff/bills/[id]/financial-view`, `/bff/bills/[id]/context`.

### Files
- `src/LCMS.Domain/Entities/Bill.cs`, `PermissionCodes`, `SystemRoleCatalog`
- `src/LCMS.Application/Bills/Queries/GetBillFinancialViewQuery.cs`, `GetBillByIdQuery.cs` (list enrich)
- `src/LCMS.Application/Bills/Commands/UpdateBillContextCommand.cs`
- `src/LCMS.Api/Endpoints/TenantBillEndpoints.cs`
- `src/LCMS.Infrastructure/.../20260917130144_UI02_BillFinancialContext*.cs`
- `apps/web/components/BillFinancialDrawer.tsx`, `BillListWorkspace.tsx`, `DetailDrawer.tsx`
- `apps/web/lib/bill-financial-view.ts`, `bills-shared.ts`, `bff-api.ts`
- `apps/web/app/bff/bills/[id]/financial-view/route.ts`, `context/route.ts`
- `apps/web/app/globals.css`, `app/bills/page.tsx`

### Follow-up
- Form tạo/sửa Bill nhập đủ khách hàng/tuyến/ETD/ETA (hiện drawer lưu note + API context sẵn).
- Seed `bill.update` vào role Ops/Cost/Revenue đã tồn tại (Admin tự nhận qua catalog).
- Pixel polish UI-02 (widget Bill đến hạn / Top KH dưới list — ngoài scope drawer).

---

### User
Tổng hợp lại riêng 1 file các việc chưa làm/chưa xong

### Done
- Xuất Word chỉ residual (đã loại trùng Phần 1–3): P0 UAT quyền; P1 components/seed revenue/search; P2 polish; P3 optional; chờ PO/ADR/Ops.
- File: `docs/reports/CMS_Viec_Chua_Xong_2026-09-17.docx`
- Generator: `docs/reports/_gen_open_items_docx.py`

### Follow-up (cùng ngày)
- Viết lại file: tối thiểu tiếng Anh; bảng chú giải viết tắt + nghĩa VI; SoT = Source of Truth (Nguồn dữ liệu gốc).
- File mới (bản cũ đang mở bị khóa): `docs/reports/CMS_Viec_Chua_Xong_TIENG_VIET_2026-09-17.docx` — đóng Word rồi chạy lại generator để ghi đè tên gốc nếu cần.

---

## 2026-09-17 — Báo cáo Word Phần 3 (BR CP1–CP5 + UI mockup)

### User
làm luôn phần 3

### Done
- Xuất Word Phần 3: CST/REV/PROF/BR-FIN/BR-FC/BR-SEC + UI-01…15 vs mockup + UX-01…14 acceptance + checklist 15 việc.
- File: `docs/reports/CMS_PO_YeuCau_vs_TienDo_Phan3_BR_UI_2026-09-17.docx`
- Generator: `docs/reports/_gen_po_status_part3_docx.py`
- Bộ đủ 3 phần: Epic → Domain CP6/TD1 → BR+UI.

---

## 2026-09-17 — Báo cáo Word Phần 2 (domain CP6 + TD1)

### User
ok, làm luôn phần 2

### Done
- Xuất Word Phần 2: Process P01–P15, UC-001…017, FR-*, SCR-001…017, VAL-001…012, TD1 D01–D12, UI-001…008, checklist 15 việc domain.
- File: `docs/reports/CMS_PO_YeuCau_vs_TienDo_Phan2_Domain_2026-09-17.docx`
- Generator: `docs/reports/_gen_po_status_part2_docx.py`
- Điểm nổi bật chưa làm: CP6 **P13 Budget/Forecast**; UI pricing components + seed Expected Revenue; Ops sync UI; UAT 2-user Cost≠Revenue.

---

## 2026-09-17 — Báo cáo Word PO yêu cầu vs tiến độ

### User
Lập bảng báo cáo 2 cột (Word): yêu cầu PO đầy đủ vs đã/chưa làm + lý do; checklist phần còn thiếu. Hỏi nếu vượt ngữ cảnh thì chia phần.

### Done
- Xuất 1 file Word tổng hợp (không cần chia phần): Epic E01–E16, H-001…H-012, AC-001…AC-015, M01–M15, non-goals, checklist triển khai còn thiếu.
- File: `docs/reports/CMS_PO_YeuCau_vs_TienDo_2026-09-17.docx`
- Generator: `docs/reports/_gen_po_status_docx.py` (nguồn trích `LCMS_BA_docs` + UAT/P-series/go-live-checklist).

### Note
- Mức chi tiết = Epic/Gate/Module (đủ PO review). Chưa bung từng dòng Data Dictionary TD1 / mọi use case CP6 — có thể xuất Phần 2 theo domain nếu PO yêu cầu.

---

## 2026-09-16 — Sidebar match designer mockup

### User
Menu sidebar chưa giống nhé (+ ảnh mockup)

### Done
- Icon trái từng module; active pill `#1890ff` bo góc; density sát mockup.
- Brand mark glow; footer port-style + tagline italic 3 dòng + version/© + nút collapse (`data-sidebar` / cookie `lcms_sidebar`).
- `NavIcon` / `NavLink` / `NavGroup` / `SidebarCollapseButton`; `ShellChrome` footer tách khỏi scroll nav.

### Files
- `apps/web/components/{AppShell,ShellChrome,NavGroup,NavLink,NavIcon,SidebarCollapseButton}.tsx`
- `apps/web/app/globals.css`, `lib/ui-preferences.ts` (boot cookie sidebar)

---

## 2026-09-16 — Designer layout pass UI-01…UI-15 (all list/hub screens)

### User
làm lần lượt tất cả các màn đến hết

### Done
- Shell: `TopbarAccount` (bell → ngoại lệ, help → workflow, VI, avatar) — không badge giả.
- KPI cards: tone icon + `designer-kpi`; shared `ListPageHeader` / `FilterBar` / `StatCardGrid` / `hub-module-tabs`.
- Restyle operable lists/hubs: Documents, Costs (FilterBar denser), AP/AR, Settlements, Control, Reports, Bank feed, Reconciliations, queues, Financial closes, Rate cards, Admin (+ parties/access/orgs/currencies), Settings, Integration errors, Workflow.
- Dữ liệu KPI/filter vẫn từ API thật — không chart/widget trang trí giả.

### Files
- `apps/web/components/TopbarAccount.tsx`, `AppShell.tsx`, `list/StatCardGrid.tsx`
- `apps/web/app/globals.css` (topbar + hub tabs + KPI icon)
- List/hub pages under `apps/web/app/**/page.tsx` (documents, control, admin, settings, reports, settlements, bank-feed, reconciliations, queues, rate-cards, workflow, …)

### Verify
- `npx tsc --noEmit` apps/web OK

### Follow-up
- Detail forms (new/edit) giữ pattern cũ — polish chrome nhẹ nếu cần.
- Dashboard đã có charts thật; không clone mock chart giả.

---

## 2026-09-16 — Single LCMS Designer theme (purge old skins)

### User
Đây là bộ theme designer gửi qua, hãy làm theo giống hệt 100%, có thể bỏ các theme cũ chỉ để lại 1 theme này.

### Done
- Skin duy nhất `lcms`: sidebar `#001529`, accent `#1890ff`, bg `#f0f2f5`.
- Gỡ Ledger / Harbor Dawn / Invoika / Soft Purple / Classic.
- Cookie/theme cũ migrate → `lcms`; Settings chỉ hiện skin chuẩn.
- Status pill kiểu designer (nền nhạt + chữ màu).
- Brief: `docs/design/LCMS-DESIGNER-THEME.md`

### Files
- `apps/web/app/globals.css`, `lib/ui-preferences.ts`, `SettingsForm.tsx`

### Note
Mockup còn chart/drawer/KPI giả — chưa clone widget trang trí; chỉ skin shell đúng designer.

---

## 2026-09-16 — Triển khai checklist go-live (A→C)

### User
triển khai lần lượt đến hết

### Done
- **A:** Ghi nhận UAT spine PASS (2026-09-13) trên checklist; đóng P1 variance + recon UX.
- **Variance:** `TransitionVarianceCommand` + `POST /api/variances/{id}/accept|clear|write-off` + BFF + `VarianceTransitionButtons`.
- **Recon:** datalist gợi ý bank unmatched / payments / collections trên form chi tiết.
- **C paging/filter:** Documents + Rate cards `PagedResult`; Cost `vendorPartyId`/`fromDate`/`toDate`; web list pages truyền `page`/`pageSize` server-side.
- **UI-15:** Workflow map thêm Sao kê + Đối soát.
- Checklist: `docs/ops/go-live-checklist.md` đánh dấu tiến độ.

### Verify
- `dotnet build` LCMS.Api OK
- `npx tsc --noEmit` apps/web OK

### Follow-up (còn)
1. UAT 2 user Cost≠Revenue
2. Rate-card components UI + seed revenue Expected
3. Screenshot pixel UI-09…14
4. Global Search đa entity

---

## 2026-09-16 — Checklist go-live tuần tự

### User
lên checklist làm tuần tự

### Done
- Thêm `docs/ops/go-live-checklist.md`: A UAT luồng tiền (A1–A12) → B fix P0 rồi pixel UI-09…14 → C nợ paging/filter → D ADR Order/Shipment → E DoD. Gợi ý lịch D1–D6.

### Follow-up
- Chạy checklist từ A1 trên prod; đánh dấu `[x]` / ghi P0 vào handoff từng ngày.

---

## 2026-09-16 — Dashboard UI-01 pixel closer to PO mockup

### User
Chưa thấy giống giao diện PO (UI-01 Dashboard V2).

### Done
- Restructure `/dashboard` to PO layout: greeting + date, KPI strip (CP/DT/LN/Bill — **không** fake TMS đơn hàng), horizontal task strip, chart + Best Available maturity, maturity charts + Bill gần đây, docs/AP-AR/thông báo (từ queue thật).
- Ledger nav active → blue `#2563eb` (PO).
- Login sets `lcms_dn` displayName cookie for “Xin chào, …” (cleared on logout).

### Files
- `apps/web/app/dashboard/page.tsx`, `globals.css`
- `lib/auth.ts`, `bff/auth/login|logout/route.ts`

### Verify
- `npm run build` pass

### Follow-up
- Monthly Th01–Th12 series when dashboard API exposes time buckets; customer/route columns when Bill list has them; shell topbar user chip.

---
## 2026-09-16 — Fix Next.js build (client imports next/headers)

### User
Deploy CI fail: `npm run build` in Dockerfile.web (Compose up).

### Done
- Split client-safe modules so `"use client"` workspaces no longer import server `lib/*` that pull `next/headers` via `getSessionToken`:
  - `ap-ar-shared.ts`, `settlements-shared.ts`, `documents-shared.ts` (+ existing `bills-shared.ts`)
- Client imports updated: `ApArListWorkspace`, `SettlementListWorkspace`, `DocumentListWorkspace`, `DocumentStatusTriad`.
- Verified `npm run build` succeeds locally.

### Files
- `apps/web/lib/*-shared.ts`, `ap-ar.ts`, `settlements.ts`, `documents.ts`
- workspace + DocumentStatusTriad components

### Verify
- `npm run build` (apps/web) pass

---
## 2026-09-16 — Pixel-match PO UI-01→15 (shared list kit + drawers + hubs)

### User
Implement plan pixel-match PO UI-01 → UI-15 (shared toolkit, drawers, denser list/KPI/charts; no TMS Order).

### Done
- **P0 toolkit:** `ListPageHeader`, `FilterBar`, `StatCardGrid`, `DataTableShell`, `DrawerTabs`, `AnalyticsRow` under `apps/web/components/list/`.
- **API paging:** `PagedResult<T>` + optional `page`/`pageSize` on Bills/Costs/Revenues lists; bare array when paging omitted (compat). Web `lib/paging.ts` unwrap.
- **Drawers + tabs:** Cost/Rate/Revenue/Document/AP-AR/Settlements/Admin parties; maturity stepper on Cost/Revenue.
- **Pages polished:** bills (FilterBar/StatCardGrid), costs/revenues analytics, ap-ar aging KPIs+drawer, settlements allocation drawer, dashboard greeting, control/reports/closes hubs, admin tabs, settings hub, workflow 11-step map.
- **Client boundary fix:** `lib/bills-shared.ts` so `BillListWorkspace` does not import `next/headers` via `lib/api`.

### Files
- `src/LCMS.Application/Common/Paging/*`, Bill/Cost/Revenue list queries + endpoints
- `apps/web/components/list/*`, `*ListWorkspace.tsx` (new ApAr/Settlement/AdminParty/Revenue)
- pages: bills, costs, revenues, ap-ar, settlements, dashboard, control, financial-closes, reports, admin, parties, settings, workflow
- `lib/bills.ts`, `bills-shared.ts`, `costs-revenues-server.ts`, `paging.ts`, `globals.css`

### Verify
- `npx tsc --noEmit` apps/web pass
- `dotnet test --filter BillListFinancialSummaryTests` pass

### Follow-up
- denser cost filters (vendor/date) when API exposes; Export Excel only when endpoint exists; notification badge from real queue count in shell.

---

## 2026-09-16 — Drawer Rate/Cost/Document + list pagination + denser filters

### User
Drawer Rate card / Cost / Document; pagination list; filter denser khi API hỗ trợ.

### Done
- **Drawers (master-detail):** `RateCardListWorkspace`, `CostListWorkspace`, `DocumentListWorkspace` + reusable `DetailDrawer` (Cost drawer shows Expected/Confirmed/Actual layers from enriched list DTO).
- **API Cost list:** `CostListItemDto` + `ListCosts` Select thêm `VendorPartyId`, `ExpectedAmount`, `ConfirmedAmount`, `ActualAmount`.
- **Client pagination:** `lib/list-paging.ts` + `ListPagination` — URL `page`/`pageSize` (API vẫn trả full array; slice phía web). Wired: bills, costs, revenues, documents, rate-cards.
- **Denser filters (khi API hỗ trợ):** documents — `documentType` + triad `receiptStatus`/`acceptanceStatus`/`matchingStatus`; rate-cards — partyType/active/q client; costs — maturity/attribution đã có.

### Files
- `CostQueries.cs` (CostListItemDto enrichment)
- `ListPagination.tsx`, `list-paging.ts`, `RateCardListWorkspace.tsx`, `CostListWorkspace.tsx`, `DocumentListWorkspace.tsx`
- pages: `bills`, `costs`, `revenues`, `documents`, `rate-cards`; `globals.css` (`.list-pagination`, `.denser-filters`)

### Verify
- `npx tsc --noEmit` (apps/web) pass
- `dotnet build` LCMS.Application pass

### Follow-up
- Server-side skip/take khi list lớn; drawer detail GET nếu list DTO thiếu field; denser cost filters (vendor/date) khi API expose query.

---

## 2026-09-16 — Bill list financial summary API + drawer + UI-03…14 KPIs

### User
API list Bill kèm financial summary (bỏ N+1), drawer master-detail, pixel-match UI-03→UI-14.

### Done
- **API:** `GET /api/bills` enrich batch: `summaryCurrencyCode`, revenue/cost/profit best-available + revenue Expected/Confirmed/Actual totals (3 queries: revenues, direct costs, finalized allocations) — không N+1 profile.
- **UI-02:** `BillListWorkspace` + `DetailDrawer` master-detail; KPI dùng summary API.
- **UI-03…08 KPI/layout:** bảng giá, chi phí, doanh thu, AP/AR (stat cards + header PO).
- Test: `BillListFinancialSummaryTests`.

### Files
- `GetBillByIdQuery.cs` (ListBills + summary), `BillListFinancialSummaryTests.cs`
- `BillListWorkspace.tsx`, `DetailDrawer.tsx`, `bills/page.tsx`, `lib/bills.ts`, `globals.css`
- `rate-cards`, `costs`, `revenues`, `ap-ar` pages

### Verify
- `dotnet test --filter BillListFinancialSummaryTests` pass
- `npx tsc --noEmit` apps/web pass

### Follow-up
- Drawer cho Rate card / Cost / Document; API list pagination; pixel denser filters (date range, vendor) khi có query hỗ trợ.

---

## 2026-09-16 — UI-02 Bill list/detail + PO breadcrumbs (slice 2)

### User
tiếp tục bạn (baseline UI/UX PO)

### Done
- **UI-02 Danh sách Bill:** KPI doanh thu Dự kiến/Đã xác nhận/Thực tế; lọc trạng thái; cột DT/CP/LN (best available, tối đa 40 hồ sơ); nhãn VI cho loại/trạng thái.
- **UI-02 Hồ sơ Bill:** tab Tổng quan / Chi phí / Doanh thu / Chứng từ / Tính giá / Lịch sử; hành động nhanh; breadcrumb PO.
- **UI-01:** “Việc cần xử lý của bạn”; độ chín bằng thuật ngữ VI (không lộ Actual/Confirmed/Expected).
- Breadcrumb/header: chứng từ, thanh toán & thu tiền, bảng giá, chốt tài chính.
- `BillCostRevenuePanel` hỗ trợ `focus` theo tab.

### Files
- `app/bills/page.tsx`, `app/bills/[id]/page.tsx`, `lib/bills.ts`
- `BillCostRevenuePanel.tsx`, `dashboard`, `documents`, `settlements`, `rate-cards`, `financial-closes`, `globals.css`

### Verify
- `npx tsc --noEmit` apps/web pass

### Follow-up
- List Bill API kèm financial summary (tránh N+1 profile); drawer master-detail; pixel-match UI-03…UI-14.

---

## 2026-09-16 — PO UI/UX Baseline v1.0 (shell + nav + hub screens)

### User
Làm bố cục/giao diện theo `docs/po/` Final Frontend Baseline v1.0 (UI-01→UI-15 APPROVED). Giữ guardrail tài chính; Vietnamese UX; khi khác mockup vs code thì bám Spec/Traceability.

### Done
- **Navigation Contract (14 module):** `AppShell` theo UI-15 — Trang chủ, Đơn hàng vận chuyển (Bill), Bảng giá, Chi phí, Doanh thu, Chứng từ, AP, AR (tách nav), Thanh toán & Thu tiền, Kiểm soát tài chính, Chốt, Báo cáo, Danh mục dữ liệu, Hệ thống & Cài đặt.
- **Shell:** brand LCMS, topbar Global Search (Ctrl+K → tìm Bill), sidebar navy Ledger, nhóm con collapse.
- **Hub mới:** `/costs`, `/revenues`, `/control`, `/reports`, `/workflow` (UI-15 map).
- Nhãn/breadcrumb PO trên dashboard, bills, admin, settings.

### Architecture note (không tự phá H-002)
- Mockup UI-02 có subnav Order/Shipment CRUD — **chưa dựng**: CMS là lớp kiểm soát tài chính; SoT vận hành ngoài CMS. Entry “Đơn hàng vận chuyển” = danh sách/tạo **Bill** (Financial Anchor). Cần ADR/PO nếu muốn TMS surface trong CMS.

### Follow-up (screenshot UAT từng màn)
- Master-detail drawer Bill (UI-02), denser filters/KPI cards trên từng list, AP/AR tách route vật lý nếu PO yêu cầu (hiện `?tab=`), Global Search multi-entity API.

### Files
- `components/AppShell.tsx`, `ShellChrome.tsx`, `NavGroup.tsx`, `GlobalSearch.tsx`
- `app/costs/page.tsx`, `revenues/page.tsx`, `control/page.tsx`, `reports/page.tsx`, `workflow/page.tsx`
- `globals.css`, `lib/ui-preferences.ts`, `lib/costs-revenues-server.ts`, `docs/design/CMS-LEDGER-THEME.md`

### Verify
- `npx tsc --noEmit` apps/web pass

---

## 2026-09-15 — Party address (VN merger) + VietQR bank picker

### User
Trong module đối tác: địa chỉ theo sáp nhập VN (mới/cũ); TKNH dropdown logo + tên viết tắt + tên đầy đủ; chi nhánh select nếu được.

### Done
- Địa chỉ: toggle **Địa chỉ mới** (Phường/Xã → Tỉnh/TP) / **Địa chỉ cũ** (+ Quận/Huyện). Bỏ tách “Thành phố” riêng; `city` đồng bộ `province` khi lưu.
- Ngân hàng: catalog VietQR v2 (65) snapshot — autocomplete Logo | shortName | full name.
- Chi nhánh: datalist gợi ý Hội sở + Chi nhánh theo 34 tỉnh/TP + nhập tự do (không có API chi nhánh công khai đầy đủ).

### Files
- `VnAddressFields`, `VnBankAutocomplete`, `VnBankBranchField`
- `lib/vn-admin.ts`, `lib/vn-banks.ts`, `lib/data/vietqr-banks.json`
- `EditBusinessPartyForm`, `PartyBankAccountsPanel`, `globals.css`

### Verify
- `npm run build` apps/web pass; push `8407b6e`

---

## 2026-09-15 — Dashboard: biểu đồ kiểm soát tài chính B2B

### User
thêm vài biểu đồ chuẩn hệ thống tài chính B2B vào dashboard cho chuyên nghiệp nào

### Done
- Cụm **Biểu đồ kiểm soát** trên `/dashboard` (SVG nhẹ, không thêm chart lib):
  1. P&L Best Available (CP / DT / LN) — grouped bar
  2. Độ chín dòng CP & DT — stacked composition
  3. Cơ cấu hàng đợi việc — horizontal bars
  4. Phễu chứng từ (nhận → chấp nhận → khớp nháp)
  5. Tuổi nợ AP/AR theo bucket — từ `GET /api/aging/summary`
- Tôn trọng quyền View Cost ≠ Revenue ≠ Margin; empty/error states trung thực.

### Files
- `apps/web/components/charts/FinanceCharts.tsx`
- `apps/web/app/dashboard/page.tsx`, `apps/web/app/globals.css`

### Verify
- `npx tsc --noEmit` apps/web pass

---

## 2026-09-15 — Theme Harbor Dawn (creative)

### User
tạo thêm 1 bộ theme mới, tự do sáng tạo không ràng buộc với những gì đã có.

### Done
- Theme **`harbor-dawn` / Cảng Bình Minh**: brass CTA `#c47a2a` + sương cảng, chrome sáng.
- Settings picker; layout dọc gợi ý. Ledger vẫn mặc định sản phẩm.
- `docs/design/THEME-HARBOR-DAWN.md`

### Files
- `apps/web/app/globals.css`, `lib/ui-preferences.ts`, `SettingsForm.tsx`

---

## 2026-09-15 — Full Business Partner master (Đối tác kinh doanh)

### User
Tài liệu BA chỉ là tham khảo, hãy làm full module đối tác kinh doanh đầy đủ nhất theo chuẩn hệ thống quản trị tài chính.

### Done
- Canonical `BusinessParty` + roles (không tách Customer/Vendor entity).
- Hồ sơ đầy đủ: MST (unique/tenant), tên pháp lý, địa chỉ VN, liên hệ, tiền tệ mặc định, điều khoản TT (ngày), hạn mức công nợ (tham chiếu — chưa chặn chứng từ), ghi chú.
- Child: `party_bank_accounts`, `party_contacts`; API nest dưới `/api/business-parties/{id}/…`.
- Quyền `master.party.manage` trên create/update/delete + bank/contact.
- UI: list lọc (q/role/status) + tạo; detail `/admin/parties/[id]` sửa hồ sơ, vai trò, TKNH, liên hệ.
- Migration `P26_BusinessPartyMasterFull`; demo seed MST/bank/contact.
- Tests: `BusinessPartyMasterFullTests` (2).

### Files
- Domain/API: `BusinessParty`, `PartyBankAccount`, `PartyContact`, commands/queries, `MasterDataEndpoints`
- Web: `admin/parties`, `admin/parties/[id]`, BFF nested, forms/panels
- `docs/handoff.md`

### Follow-up
- Hard credit-limit block trên AP/AR → cần ADR.
- Enforce role vendor/customer khi ghi Cost/Revenue.
- Audit trail UI cho thay đổi hồ sơ đối tác.

### Verify
- `dotnet test --filter BusinessPartyMasterFull` pass
- `npm run build` apps/web pass

---

## 2026-09-15 — Lighten CMS Ledger chrome

### User
tông màu có vẻ tối nhỉ?

### Done
- Ledger: bỏ nav slate tối; chrome trắng + jade CTA `#0f6b58`.
- Vertical sidebar cũng sáng (viền nhẹ), active vẫn jade.

### Files
- `apps/web/app/globals.css`, `lib/ui-preferences.ts`, `docs/design/CMS-LEDGER-THEME.md`

---

## 2026-09-15 — Fix Ui0AuthLoginTests (CI deploy skip)

### User
sao deploy hay bị lỗi skip vậy nhỉ

### Answer
- Deploy **không lỗi** — bị **skip** vì `deploy.needs: test` và job `test` đỏ.
- Root cause: `Ui0AuthLoginTests` login OK rồi gọi `/api/bills` / `/api/users` → **403** (user fixture không có Admin role sau khi authz siết).
- Không liên quan commit theme Ledger.

### Fix
- Seed Admin + `UserRole` trong `Ui0AuthLoginTests`.

### Verify
- `dotnet test --filter Ui0AuthLoginTests` pass

---

## 2026-09-15 — CMS Ledger theme (product default)

### User
Thiết kế bộ theme phù hợp CMS + nêu tiêu chí (chuyên gia thiết kế).

### Done
- Theme **CMS Ledger** (`ledger`): jade `#0d5c4d` + slate nav `#1a2b3c` + giấy `#eef2f5`.
- Mặc định: `ledger` + layout dọc. Invoika/Purple/Classic = preset phụ.
- Brief: `docs/design/CMS-LEDGER-THEME.md`.

### Files
- `apps/web/app/globals.css`, `lib/ui-preferences.ts`, `SettingsForm.tsx`
- `docs/design/CMS-LEDGER-THEME.md`

### Verify
- `npm run build` apps/web

---

## 2026-09-15 — Compact multi-col + fieldset layout

### User
bố cục lại các giao diện… 2–3 cột, fieldset nhóm liên quan, rà hết giao diện

### Answer
- CSS: `layout-cols-2/3`, `card-grid`, `fieldset.group-box`, `perm-group-grid`, denser `form-grid`, dashboard 2-col clusters.
- Phân quyền: ma trận nhóm Bill/Cost/Revenue/AP·AR/Danh mục/Hệ thống; thành viên dạng card.
- Admin danh mục: list + form cạnh nhau; Settings + create forms: fieldset 2 cột.

### Files
- `apps/web/app/globals.css`, `admin/**`, `login/page.tsx`
- `RolePermissionToggleMatrix`, `UserRoleAssignPanel`, `SettingsForm`, Create*/Receive* forms

---

## 2026-09-15 — VPS self-host hardening rules (from note.txt)

### User
Doc note.txt (Ollama/Next.js CVE/bot scan lessons) ? b? sung v�o Cursor rules.

### Done
- New always-apply rule `.cursor/rules/08-vps-selfhost-hardening.mdc` (public ports, container limits, edge rate-limit, CVE hygiene).
- Extended `security.mdc`, `deployment.mdc`, `supply-chain-deps.mdc`, `06-ship-after-task.mdc`.
- Host compose: stop publishing Postgres `5432`; add mem/cpu/pids limits; nginx rate-limit + block `x-middleware-subrequest`; `/metrics` allowlist-only.

### Files
- `.cursor/rules/08-vps-selfhost-hardening.mdc`
- `infra/docker-compose.host.yml`, `infra/nginx.conf`

---
## 2026-09-14 — CI: soft bootstrap permission (first operator)

### User
build bị lỗi hoài nè (CI #143 / header commits)

### Cause
`PermissionService` bootstrap theo «tenant chưa có Roles» — sau `CreateTenant` đã seed role hệ thống → JWT first-operator `POST /api/users` = **403**.

### Fix
Bootstrap khi **chưa có UserRoles** trên tenant **và** actor **chưa có** row trong `Users` (JWT chicken-egg). User đã tồn tại không role → vẫn 403.

### Verify
`Production_WithValidJwt_*` + `CreateBill_WithoutPermission_*` + Sprint0/1 identity (16) PASS.

### Files
- `src/LCMS.Infrastructure/Identity/PermissionService.cs`
- `src/LCMS.Application/Abstractions/IPermissionService.cs`

---

## 2026-09-14 — Horizontal header: logo | nav | logout top-right

### User
Chỉnh đầu trang: Đăng xuất góc trên phải; Logo, menu, Đăng xuất cùng 1 hàng.

### Done
- Header ngang nowrap: Brand | Nav (wrap trong cột giữa) | Đăng xuất top-right (`align-self: flex-start`).

### Files
- `apps/web/app/globals.css` (commit `6c2354e`)

---

## 2026-09-14 — Seed 7 role demo logins

### User
tạo các account tương ứng cho các role, mật khẩu mặc định là: abc123

### Answer
- Seeder `RoleDemoUserSeeder` (startup): 7 email → 7 system role, tenant `ops`.
- Bật: `Auth__RoleDemoUsers__Enabled=true` + `Auth__RoleDemoUsers__Password` (Dev appsettings; prod qua `infra/.env`).
- Emails: `admin@cms.local`, `controller@cms.local`, `cost@cms.local`, `revenue@cms.local`, `ops.user@cms.local`, `master@cms.local`, `viewer@cms.local`.
- VPS: env đã bật; fallback tar+compose (Actions chậm) — verify login `cost@cms.local` 200.

### Files
- `src/LCMS.Api/Auth/RoleDemoUserSeeder.cs`, `AuthOptions.cs`, `Program.cs`
- `apps` N/A · `infra/docker-compose.host.yml`, `infra/.env.example`
- `tests/LCMS.Api.Tests/RoleDemoUserSeederTests.cs`

---

## 2026-09-14 — Phân quyền: 7 role hệ thống + Admin on/off

### User
phân tích và triển khai chức năng phân quyền… Administrator on/off quyền cho thành viên

### Answer
- **7 role:** Admin, FinancialController, CostAccountant, RevenueAccountant, Ops, MasterData, Viewer (ADR-0016).
- Model: User → Role → Permission(Action) × DataScope; Cost ≠ Revenue; Permission ≠ Approval.
- Admin UI: `/admin/access` — gán/gỡ role + bật/tắt ma trận quyền.
- API: `PUT /api/roles/{id}/permissions`, `DELETE /api/users/{u}/roles/{r}`, `GET …/permission-matrix`, enforce `user.manage` / `role.manage`.
- Seed 7 role khi tạo tenant / migrate / bootstrap.

### Files
- `src/LCMS.Domain/Identity/SystemRoleCatalog.cs`, `PermissionCodes.cs` (catalog)
- `src/LCMS.Application/Identity/TenantAccessSeeder.cs`, `Roles/*`, `Users/*`
- `src/LCMS.Api/Endpoints/IdentityEndpoints.cs`, `Program.cs`
- `apps/web/app/admin/access/`, `components/RolePermissionToggleMatrix.tsx`, `UserRoleAssignPanel.tsx`
- `docs/adr/ADR-0016-system-role-catalog.md`
- `tests/LCMS.Api.Tests/SystemRoleCatalogAccessTests.cs`

---

## 2026-09-13 — Restore desktop nav on top

### User
sao bây giờ menu rớt xuống dưới hết rồi

### Cause
Đổi DOM `main` trước `sidebar` để vá mobile → desktop horizontal hiện nav dưới nội dung.

### Fix
- DOM lại: mobile-bar → sidebar → main → backdrop.
- Horizontal: `grid-template-areas` nav/main khóa vị trí.
- Mobile: `order` + sidebar/backdrop `flex:0` / fixed (sticky override giữ).

### Files
- `apps/web/components/ShellChrome.tsx`, `apps/web/app/globals.css`

---

## 2026-09-13 — Mobile gap: sticky-nav override

### User
giao diện mobile vẫn còn bị 1 khoảng trống lớn phía trên

### Cause
`html[data-sticky-nav=1][data-layout=horizontal] .sidebar { position: sticky }` (specificity cao hơn mobile `fixed`) → sidebar nav vẫn in-flow, tạo khoảng trống lớn.

### Fix
- Mobile: override sticky-nav → `position: fixed` + `visibility` khi đóng.
- `ShellChrome`: thứ tự DOM `mobile-bar` → `main` → backdrop/sidebar (drawer không chen trước nội dung).

### Files
- `apps/web/app/globals.css`, `apps/web/components/ShellChrome.tsx`

---

## 2026-09-13 — Mobile drawer z-index + empty gap

### User
- Click menu → lớp phủ mờ, không click menu con
- Khoảng trống phía trên nội dung mobile

### Cause
1. `html[data-layout=horizontal] .sidebar { z-index:20 }` thắng mobile z-50 → backdrop z-45 che menu.
2. Backdrop `display:block` luôn trong grid `auto 1fr` → chiếm hàng 1fr (khoảng trống).

### Fix
- Mobile: flex column; backdrop chỉ `display` khi `.nav-open`; sidebar/backdrop `fixed`; z-index bar 70 > drawer 60 > backdrop 55.
- Override horizontal sidebar z-index trên mobile.

### Files
- `apps/web/app/globals.css`

---

## 2026-09-13 — Fix deploy 502: stale nginx upstream

### User
(CI deploy fail / 502 Bad Gateway sau health check)

### Cause
`cms-api`/`cms-web` recreate đổi IP; `cms-proxy` giữ upstream IP cũ → connection refused → 502.

### Fix (now)
- Recreate proxy trên VPS; `/health` `/ready` OK.
- `infra/nginx.conf`: Docker DNS resolver + `proxy_pass` biến (re-resolve 10s).
- CI: force-recreate proxy sau `compose up`.

### Files
- `infra/nginx.conf`, `.github/workflows/ci.yml`

---

## 2026-09-13 — Mobile shell drawer + contrast

### User
giao diện mobile chưa ổn, fix lại cho chuẩn toàn bộ giúp mình nhé

### Done
- Mobile ≤800px: hamburger + drawer off-canvas (trái), backdrop, Escape/đóng khi đổi route; top bar sticky (logo + Đăng xuất).
- Nav căn trái; mục active chữ trắng trên accent (sửa contrast soft-purple).
- Nội dung: panel/table/toolbar/form/stat-grid co hẹp; viewport meta.
- Client `ShellChrome`; tách `severityLabel` khỏi server module (build client).

### Files
- `apps/web/components/ShellChrome.tsx`, `AppShell.tsx`
- `apps/web/app/globals.css`, `layout.tsx`
- `apps/web/lib/severity.ts`, `control-desk.ts`, `EscalateVarianceButton.tsx`

### Verify
- `npm run build` apps/web OK

---

## 2026-09-13 — Fix CI: Sprint12 Admin actor + Sprint7 AP adjust Created

### User
(CI đỏ sau commit Move logout… — screenshot Actions test fail)

### Cause
Không liên quan UI logout. 3 API tests lệch runtime:
- Sprint12 audit: `X-User-Id` random → 403 `cost.confirm` (tenant seed Admin role).
- Sprint7: AP `/adjust` trả `Created` (P10) nhưng test còn expect `NoContent`.

### Fix
- Sprint12(+FULL): tạo user + gán Admin trước mutation có actor.
- Sprint7: assert `Created`.

### Verify
- 3 test fail trước → pass; full suite chạy trước push.

---

## 2026-09-13 — Shell logout + AP/AR toolbar

### User
- Bỏ chữ Đã đăng nhập
- Đưa nút đăng xuất lên góc trên bên phải
- sắp xếp lại các nút: Tóm tắt tuổi nợ, Tạo Exposure... thẳng hàng.

### Done
- Xóa «Đã đăng nhập»; Đăng xuất vào `.shell-header-actions` (horizontal: góc phải header; vertical: đáy sidebar).
- Topbar chỉ hiện khi có `topbarRight`.
- AP/AR: hàng `.toolbar-row` nowrap — Tóm tắt tuổi nợ + 2 nút tạo exposure cùng size `btn-sm`.

### Files
- `apps/web/components/AppShell.tsx`
- `apps/web/app/globals.css`
- `apps/web/app/ap-ar/page.tsx`

---

## 2026-09-13 — UAT go-live S1–S14 = **PASS** (redeploy host)

### User
làm lần lượt từ S1 đến S14

### Done
- **S1** giữ PASS (Bill `UAT-GL-20260913-1515`, close locked, P&L 1.4M) — verify lại UI.
- **S2** UI: shared `UAT-S2-SHARED` → chốt phân bổ 250k+250k DEMO-02-SHARE-A/B.
- **S3–S14** PASS (S9: aging OK; dual-user Cost≠Revenue SKIP một Admin).
- **Gap deploy:** VPS API cũ thiếu `reverse-recognize` / `tenant-settings` / `import-csv` → fallback tar sync + `docker compose up -d --build api` + restart proxy; migrate P10/P14–P25 áp dụng.
- Sau vá: S6 reverse 204; S11 import-csv imported=1; S13 tenant-settings GET/PUT OK.
- RESULT: `docs/sprint/UAT-GO-LIVE-FINANCIAL-RESULT.md` + `UAT-GO-LIVE-S2-S14-RESULT.json`.

### Next
- Theo dõi CI deploy luôn sync đủ source (tránh host chậm lại).
- Optional: UAT người thật Cost-only vs Revenue-only (S9). Residual ADR: OIDC/OTLP/soak/broker.

### End-user
- VPS đã có đủ API P10–P25; go-live tài chính đủ theo RESULT PASS.

---

## 2026-09-13 — UAT S2–S14 mid (trước redeploy — superseded)

### User
Finish UAT S2–S14 with correct API shapes; S2 parent UI on DEMO-02-SHARE-A/B.

### Done
- S2 PASS UI; khi đó S6/S13 FAIL vì host thiếu endpoint — **đã vá ở mục trên**.

---

## 2026-09-13 — UAT go-live S1 UI-only VPS = **PASS**

### User
Cần agent chạy hộ S1 UI-only trên VPS.

### Done
- Browser + BFF cookie trên `194.233.89.26` (không Postman/JWT).
- Bill `UAT-GL-20260913-1515` (`01a099d4-18da-7706-a93a-1a685c8f7983`): Cost confirm 1.1M → Revenue 2.5M → doc accept+match → AP/AR recognize → payment/collection finalize → close **locked** P&L **1.4M** (AP/AR dư 0).
- RESULT: `docs/sprint/UAT-GO-LIVE-FINANCIAL-RESULT.md` — S1 PASS; S2–S14 SKIP (phiên sau).
- Gap minor: nút Chấp nhận chứng từ bị sidebar đè trên viewport hẹp.

### Next
- Phiên người nghiệp vụ thật: S2–S14 (shared, write-off, Strict, aging/quyền, bank feed…).
- Optional UX: fix overlap sidebar/accept CTA.

### End-user
- Bill UAT-GL trên VPS đã khóa chốt; đọc P&L trên `/financial-closes/01a099da-3e2d-7720-9407-5f5630bc7a79`.

---

## 2026-09-13 — Mở wave UAT go-live tài chính đủ (người nghiệp vụ)

### User
Tiếp theo: UAT go-live tài chính đủ với người nghiệp vụ trên VPS — không còn checklist P-series.

### Done
- Gói UAT nghiệp vụ: `docs/sprint/UAT-GO-LIVE-FINANCIAL.md` (S1 bắt buộc Bill→Close + S2–S14 kiểm soát).
- Phiếu kết quả: `UAT-GO-LIVE-FINANCIAL-RESULT.md`.
- Kickoff chat: `PROMPT-UAT-GO-LIVE.md`.
- Board: `PO-GAP-CHECKLIST.md` + `README-AGENTS.md` — P01–P25 đóng; wave hiện tại = UAT go-live.
- Preflight VPS: `/health` `/ready` `/` = 200; `/login` UI OK (VI). Host: bootstrap `ops@cms.local`, `Demo__SeedOnStartup=true` (password chỉ `infra/.env`).

### Files
- `docs/sprint/UAT-GO-LIVE-FINANCIAL.md`
- `docs/sprint/UAT-GO-LIVE-FINANCIAL-RESULT.md`
- `docs/sprint/PROMPT-UAT-GO-LIVE.md`
- `docs/sprint/PO-GAP-CHECKLIST.md`, `docs/sprint/README-AGENTS.md`

### Next
- Chạy phiên với người nghiệp vụ: paste `PROMPT-UAT-GO-LIVE.md` hoặc điền RESULT theo S1→S14.
- Agent có thể chạy hộ S1 UI-only nếu PO nhờ (không Postman).

### End-user
- URL: `http://194.233.89.26` — đăng nhập tài khoản vận hành đã cấp; làm theo kịch bản UAT-GO-LIVE (không dùng mã Pxx).

---

## 2026-09-13 — PO gap checklist P01–P25 **COMPLETE**

### User
đã làm xong P0-P25

### Done
- Board: Pass 1 · Pass 2 · Pass UI · **P01–P25** đóng (`PO-GAP-CHECKLIST.md`, README).
- Residual ngoài checklist (không bắt buộc): OIDC IdP thật, OTLP exporter, soak CI gate, broker ngoài process — cần ADR nếu làm.

### Next
- Vận hành go-live tài chính đủ trên VPS (người nghiệp vụ) · hoặc PO mở scope mới qua ADR.

---

## 2026-09-13 — P21→P25 data scope, refresh tokens, outbox/Redis, fine perms, soak

### User
Làm P21 đến P25.

### Done
- **P21:** Data scope on Revenue / Documents / AP / AR (own + Bill org).
- **P22:** Refresh token store + rotate/revoke; BFF `lcms_rt`; ADR-0015.
- **P23:** Outbox hosted worker; Redis optional rate-limit; Prometheus metrics (OTLP deferred).
- **P24:** Fine-grained `cost|revenue.confirm|actualize`, `ap|ar.write_off`.
- **P25:** Audit From/To PG pushdown; soak script.

### Files
- Queries: Revenue/FinancialDocument/ApAr + `DataScopeFilter`
- Auth: `AuthTokenService`, `refresh_tokens` migration `P21_P25_RefreshTokensAndScope`
- `OutboxProcessorHostedService`, Redis in `docker-compose.host.yml`
- PermissionCodes + maturity/write-off EnsureAsync
- `ListAuditEventsQuery`, `scripts/soak/money-path-soak.ps1`
- DoD P21–P25

### Verify
- `dotnet test` filter SprintP21P25 — 2 passed

### Next
- Checklist complete; optional OIDC IdP / OTLP / soak CI

### End-user
- Logout revoke refresh; settings/admin unchanged. Role catalog có thêm quyền xác nhận/xóa nợ.

---

## 2026-09-13 — P14→P20 integration recovery, bank CSV, tenant settings

### User
Làm P14 đến P20.

### Done
- **P14:** `/integration-errors` + mark-retried / dead-letter UI + BFF.
- **P15:** `POST /api/bank-feed/lines/import-csv`; `ImportBankFeedCsvForm` trên `/bank-feed`.
- **P16:** Recognition policy `require_document_link` gate trên recognize AP/AR.
- **P17:** Bill financial profile `asOf` dùng AP/AR adjustment ledger dated.
- **P18:** `/admin` hub — parties, orgs, currencies (create + list).
- **P19:** `tenant_settings` table + `GET/PUT /api/tenant-settings`; `settings.manage`.
- **P20:** Tenant override `maxWriteOffAmount`, `confirmApprovalThresholdBase`, recognition policy.

### Files
- Backend: `ImportBankFeedCsvCommand`, `TenantSetting`, `TenantSettingsService`, `TenantFinancialOptionsResolver`, `TenantSettingsEndpoints`, `CostApprovalGate` (async tenant threshold), migration `P14_P20_TenantSettingsBankFeedCsv`
- Web: `integration-errors/*`, `admin/*`, `TenantFinancialSettingsForm`, `ImportBankFeedCsvForm`, BFF routes
- Tests: `SprintP14P20SliceTests.cs`
- DoD: `P14-DOD.md` … `P20-DOD.md`

### Verify
- `dotnet test` filter `SprintP14P20|SprintP10|SprintP08` — 6 passed

### Next
- **P21** data scope Revenue / Documents / AP·AR

### End-user
- **Lỗi tích hợp:** menu → lọc trạng thái → thử lại / dead-letter.
- **Bank feed:** tab import CSV trên `/bank-feed`.
- **Cài đặt:** `/settings` — ngưỡng xóa nợ, ngưỡng phê duyệt chi phí, chính sách ghi nhận.
- **Quản trị:** `/admin` — đối tác, tổ chức, tiền tệ.

---

## 2026-09-13 — P10 reverse recognize + P12 matrix backend (complete P10–P13)

### User
Làm tiếp P10, P11, P12, P13.

### Done
- **P10:** AP/AR adjustment ledger; reverse-recognize API/UI; restore exposure; tests.
- **P11–P13 UI:** already on `a730ffe` (variance inbox, write-off wizard, audit panel).
- **P12 backend:** `ApprovalMatrix` config → write-off RequiredLevel.

### Files
- Entities + migration `P10_P12_ApArAdjustmentsAndMatrix`
- ReverseRecognize* commands; ApArAdjustmentQueries; WriteOff matrix
- `ReverseRecognizeButton`, BFF reverse-recognize
- DoD P10–P13

### Verify
- SprintP10 — 2 passed

### Next
- P14 UI recovery tích hợp

### End-user
`/ap-ar`: **Đảo ghi nhận** (khi chưa tất toán). Xóa nợ lớn → phê duyệt cấp theo ma trận.

---

## 2026-09-13 — P11 Variance inbox + P12 write-off wizard + P13 audit panel

### User
Implement P11, P12 UI polish, and P13. Backend APIs largely exist.

### Done
- **P11:** `/queues/variances` list (`status` filter, default open); `listVariances`; `EscalateVarianceButton` → POST `/bff/exceptions` (`variance.manual_escalate`); nav + ui-prefs + dashboard `openVarianceCount` → variances.
- **P12:** `WriteOffButton` multi-step (preview + estimated cấp 1/2; reason confirm); show `requiredLevel` from 202 if present; API 202 includes `requiredLevel`.
- **P13:** `AuditTrailPanel` + BFF GET `/bff/audit-events`; mounted on cost detail + financial close (latest snapshot).

### Files
- `apps/web/app/queues/variances/{page,loading}.tsx`
- `EscalateVarianceButton.tsx`, `AuditTrailPanel.tsx`, `WriteOffButton.tsx`
- `bff/exceptions/route.ts`, `bff/audit-events/route.ts`
- `control-desk.ts`, `AppShell.tsx`, `ui-preferences.ts`, `dashboard/page.tsx`
- `costs/[id]/page.tsx`, `financial-closes/[id]/page.tsx`
- `ExposureApArEndpoints.cs` (requiredLevel on 202)

### Verify
- Manual: open `/queues/variances`, escalate → exceptions queue; write-off wizard steps; audit on cost/close.

### Next
- P14 UI recovery tích hợp (retry / dead-letter)

### End-user
Hàng đợi **Chênh lệch** tách ngoại lệ; CTA **Mở ngoại lệ**. Xóa nợ 2 bước (ước tính cấp phê duyệt). Nhật ký kiểm toán trên Chi phí / Bản chốt.

---

## 2026-09-13 — P09 Confirm match session + auto-suggest

### User
Next: P09 Confirm match session + auto-suggest.

### Done
- Confirm phiên khớp: draft → confirmed (≥1 active detail); khóa thêm chi tiết; audit.
- Suggestions trong dung sai (read-only); review tay qua Add detail.
- UI: Xác nhận phiên + Xem đề xuất; reverse/cancel sau confirm.
- ADR-0005 amended (confirm + suggest).

### Files
- `ConfirmDocumentMatchCommand.cs`, `SuggestMatchCandidatesQuery.cs`
- Endpoints confirm + suggestions; BFF; `ConfirmDocumentMatchButton`, `MatchSuggestionsPanel`
- `SprintP09ConfirmMatchSuggestTests.cs`, `P09-DOD.md`

### Verify
- `dotnet test` filter SprintP09 — 3 passed

### Next
- P10 Reverse recognize AP/AR + sổ điều chỉnh

### End-user
Phiên khớp nháp: thêm chi tiết → **Xác nhận phiên khớp**. **Xem đề xuất** trong dung sai rồi thêm thủ công. Không auto-apply.

---

## 2026-09-13 — P08 Auto Exposure từ chứng từ đã khớp

### User
Next: P08 Auto Exposure từ chứng từ đã khớp.

### Done
- Propose + create exposure từ match detail active; gắn Cost/Revenue sẵn có (C-003/C-004).
- Idempotent: `SourceType=document_match_detail` + `SourceId=detail.Id`.
- Skip `line_to_line` / hướng chứng từ sai / đã tạo.
- UI CTA trên trang phiên khớp + BFF.

### Files
- `CreateExposuresFromMatchCommand.cs` (propose + create)
- `FinancialDocumentEndpoints` `/exposure-proposals`, `/create-exposures`
- `CreateExposuresFromMatchButton.tsx`, match page, BFF routes
- `SprintP08AutoExposureFromMatchTests.cs`, `docs/sprint/P08-DOD.md`

### Verify
- `dotnet test` filter SprintP08 — 2 passed

### Next
- P09 Confirm match session + auto-suggest

### End-user
Trên phiên khớp chứng từ: **Xem đề xuất** → **Tạo exposure**. Không tạo chi phí/doanh thu mới. Received ≠ Accepted ≠ Matched ≠ Recognized.

---

## 2026-09-12 — P07 Aging summary + dashboard financial visibility

### User
Next: P07 Aging summary + dashboard tách quyền tài chính.

### Done
- `revenue.read` catalog; HasPermissionAsync; dashboard omits Cost/Revenue/Margin without permission.
- `GET /api/aging/summary` + CSV export; AP=`cost.read`, AR=`revenue.read`.
- UI `/ap-ar/aging` + links; ADR-0006 note; tests SprintP07.

### Files
- PermissionCodes, PermissionService, GetDashboardSummaryQuery
- AgingSummaryQueries, DashboardReportingEndpoints
- `apps/web/app/ap-ar/aging`, bff/aging/export, dashboard/ap-ar pages
- `docs/sprint/P07-DOD.md`

### Verify
- SprintP07 tests passed · `npm run build` OK

### Next
- P08 Auto Exposure từ chứng từ đã khớp

### End-user
**Tóm tắt tuổi nợ** (`/ap-ar/aging`): bucket + Xuất CSV. Dashboard chỉ hiện số CP/DT/biên đúng quyền.

---

## 2026-09-12 — Next wave sau P01–P06: P07→P11

### User
đã xong từ P01 đến P05, lên checklist tiếp theo làm gì

### Done
- Checklist cập nhật: P01–P06 Done (P06 đã ship trước đó); wave tiếp **P07→P11**.
- Next kickoff: **P07** Aging summary + dashboard tách quyền.

### Next
- P07 → P08 → P09 → P10 → P11 → rồi P12+

---

## 2026-09-12 — P06 Dated fx_rates (Cost/Revenue/Settlement)

### User
Next: P06 — bảng fx_rates theo ngày.

### Done
- Entity + migration `fx_rates` (from/to/date/rate/source/version).
- Lookup dated → set `BaseAmount` + `FxRateId`; stub config fallback nếu thiếu dòng.
- API upsert/list/resolve/delete; dashboard roll-up dùng asOf + rates.
- ADR-0004 amended; ADR-0011 note; tests `SprintP06FxRatesTests`.

### Files
- `FxRate.cs`, `FxRateLookup`, Commands/Queries, `Cost|Revenue|SettlementFxStub` (scoped + async Apply)
- Migration `20260912164916_P06_FxRates`
- `MasterDataEndpoints` `/api/fx-rates*`
- `docs/sprint/P06-DOD.md`

### Verify
- `dotnet test` filter SprintP06FxRatesTests — 2 passed

### Next
- P07 Aging summary + dashboard tách quyền tài chính

### End-user / ops
`POST /api/fx-rates` (USD→VND theo ngày) trước khi ghi Cost/Revenue/Settlement ngoại tệ — hệ thống gắn `fx_rate_id`. Chưa có dòng thì vẫn dùng stub config (nếu khai báo).

---

## 2026-09-12 — P05 Rate card / Rating / seed Expected (UI)

### User
Next: P05 Rate card / Rating / seed Expected (UI).

### Done
- UI `/rate-cards` list + create; `/rate-cards/[id]` versions → rules → publish.
- Bill panel: rate (published version) + seed Expected; lịch sử + seed lại (idempotent).
- BFF proxies rate-cards / versions / publish / rules / ratings / seed-expected-costs.
- Nav **Bảng giá** + dashboard shortcut.
- DoD `docs/sprint/P05-DOD.md`; checklist P05 Done; next P06.

### Files
- `apps/web/lib/rate-cards.ts`
- `apps/web/app/rate-cards/**`, `app/bff/rate-cards|rate-versions|ratings/**`
- Components: CreateRateCard/Version, AddPricingRule, Publish, RateBillForm, SeedExpected, BillRatingPanel
- AppShell, dashboard, bills/[id]

### Verify
- `npm run build` apps/web

### Next
- P06 Bảng `fx_rates` theo ngày

### End-user
**Bảng giá** → tạo card → phiên bản nháp → quy tắc → phát hành → mở Bill → **Tính giá** (tick seed Dự kiến).

---

## 2026-09-12 — P04 Strict close + period lock UAT (VPS PASS)

### User
Next: P04 Strict close + period lock UAT trên VPS.

### Done
- Script `scripts/uat-vps-p04-strict-period-lock.ps1`.
- Chạy VPS `194.233.89.26` → **PASS** (strict lock → 409 confirm/allocate → reopen → mutate → reclose v2; snapshot v1 immutable).
- Kết quả: `docs/sprint/P04-UAT-VPS-RESULT.json` · DoD `P04-DOD.md`.
- UI note Strict trên form mở chốt.

### Verify
- VPS UAT verdict PASS (no secrets in result JSON).

### Next
- P05 Rate card / Rating / seed Expected (UI)

### End-user
Chọn chính sách **Strict** khi mở chốt → sau snapshot: không xác nhận CP/DT / không phân bổ TT khi Locked; mở lại mới thao tác tiếp; bản chốt cũ không đổi.

---

## 2026-09-12 — P03 Approval gate write-off > trần

### User
Next: P03 Approval gate write-off > trần.

### Done
- Write-off ≤ `MaxWriteOffAmount` (1000): áp ngay (204).
- Vượt trần: tạo Approval + 202 `{ requiresApproval, approvalId }`; outstanding giữ nguyên đến khi duyệt.
- `DecideApproval` final approve → áp xóa nợ từ payload `Notes`; reject → không ghi.
- UI: cho phép số lớn; thông báo + link `/queues/approvals`.
- ADR-0008 cập nhật; objectType `accounts_payable` / `accounts_receivable`.

### Files
- `WriteOffApplier.cs`, `WriteOffAccountsPayable|ReceivableCommand.cs`, `DecideApprovalCommand.cs`
- `Approval.cs` object types; `ExposureApArEndpoints.cs`
- `WriteOffButton.tsx`, `control-desk.ts`
- `docs/sprint/P03-DOD.md`, ADR-0008, tests Sprint8

### Verify
- Sprint8FullSettlementTests 4 passed · `npm run build` OK

### Next
- P04 Strict close + period lock UAT trên VPS

### End-user
Xóa nợ nhỏ → ghi ngay. Xóa nợ lớn → chờ **Hàng đợi phê duyệt**; duyệt xong mới giảm outstanding.

---

## 2026-09-12 — P02 Adjust Cost/Revenue + số Confirm/Actual

### User
P02 Adjust Cost/Revenue + nhập số Confirm/Actual

### Done
- Confirm/Actual dialog: nhập số lớp đích (Bill + detail pages).
- Adjust dialog: adjustment|reversal + delta + lý do bắt buộc → API adjustments.
- Lịch sử trên `/costs/[id]`, `/revenues/[id]`, shared cost detail; link từ Bill panel.
- BFF `POST /bff/costs|revenues/{id}/adjustments`.

### Files
- `AdjustCostRevenueButton`, `MaturityTransitionButton`, `AdjustmentHistoryTable`
- `apps/web/app/costs/[id]`, `revenues/[id]`, `costs/shared/[id]` (adjust+history)
- `BillCostRevenuePanel`, BFF adjustments routes, `costs-revenues*.ts`
- `docs/sprint/P02-DOD.md`

### Verify
- `npm run build` apps/web OK

### Next
- P03 Approval gate write-off > trần

### End-user
Trên Bill: Xác nhận/Ghi nhận Thực tế → nhập số; **Điều chỉnh** → delta + lý do; **Lịch sử** xem sổ điều chỉnh.

---

## 2026-09-12 — P01 Shared allocation UI

### User
P01 Shared allocation UI

### Done
- UI phân bổ chi phí chung: list `/costs/shared`, tạo `/costs/shared/new`, chi tiết + draft/finalize `/costs/shared/[id]`.
- Basis equal | quantity | manual_ratio; ≥2 Bill; chốt với dialog VI.
- BFF: `POST /bff/costs/{id}/allocations`, `POST /bff/cost-allocations/{id}/finalize`.
- API: `GET /api/costs?attributionType=shared`; create/finalize allocation reject nếu dưới 2 Bill.
- Nav sidebar + dashboard shortcut + link từ Bill cost panel.

### Files
- `apps/web/app/costs/shared/**`, components `CreateSharedCostForm`, `AllocateSharedCostForm`, `FinalizeCostAllocationButton`
- `apps/web/lib/costs-revenues.ts`, `costs-revenues-server.ts`, BFF routes
- `src/LCMS.Application/Costs/Commands/AllocationCommands.cs`, `CostQueries.cs`, `CostEndpoints.cs`
- `docs/sprint/P01-DOD.md`, checklist/README cập nhật

### Verify
- `dotnet test --filter Sprint4Full`
- `npm run build` (apps/web)

### Next
- P02 Adjust Cost/Revenue + nhập số Confirm/Actual

### End-user
Finance mở **Chi phí chung** → tạo shared → chọn ≥2 Bill + cơ sở → tạo nháp → **Chốt phân bổ**. Số phân bổ đã chốt hiện trên hồ sơ Bill / lợi nhuận.

---

## 2026-09-12 — PO gap checklist (post UAT G1–G6)

### User
Lên checklist tuần tự các chức năng còn thiếu so với documents PO để làm tuần tự.

### Done
- `docs/sprint/PO-GAP-CHECKLIST.md` — P01→P25 (go-live → control → NFR).
- README trỏ backlog; next đề xuất **P01 Shared allocation UI**.

### Next
- P01 → P02 → P03 → P04 → P05 …

---

## 2026-09-12 — Settings: options + admin utilities

### User
thêm các chức năng, tiện ích, tùy chọn phù hợp cho phần cài đặt trong admin

### Done
- Cơ bản: layout, density, trang vào sau đăng nhập, ngôn ngữ VI khóa.
- Hiển thị: sọc bảng, ghim nav, giảm chuyển động, nhãn nhóm menu ngang, ẩn/hiện hàng đợi.
- Theme picker; lối tắt vận hành; tiện ích kiểm tra health/ready/session (BFF), xuất/nhập JSON UI, khôi phục mặc định.
- `/` redirect theo `homePath` cookie `lcms_ui`. Không bỏ xác nhận thao tác tiền.

### Files
- `apps/web/lib/ui-preferences.ts`, `SettingsForm.tsx`, `AppShell.tsx`, `globals.css`
- `app/settings`, `app/page.tsx`, `app/layout.tsx`
- `app/bff/system/health|ready`

### Verify
- `npm run build` apps/web

### Follow-ups
- Tenant-scoped settings API · `settings.manage`

---

## 2026-09-12 — Dashboard clusters (control + finance ops)

### User
thiết kế Dashboard thêm các cụm thông tin, chức năng, báo cáo thống kê đầy đủ phù hợp với chức năng đặc trưng của hệ thống này

### Done
- Mở rộng `GET /api/dashboard/summary`: số phiên đối soát mở, bank-feed chưa khớp, cụm chứng từ (chờ chấp nhận / chấp nhận chưa khớp / match nháp), AP/AR mở + exposure, payment/collection mở, pipeline độ chín (số dòng Expected/Confirmed/Actual — không phải tiền).
- UI `/dashboard` chia cụm: Việc cần xử lý → Best Available P&L → Độ chín → Chứng từ → AP/AR → Thanh toán/Thu tiền → Lối tắt chức năng (link thật).
- Giữ honesty: Best Available = projection; Received ≠ Accepted ≠ Matched; không fake chart.

### Files / API
- `src/LCMS.Application/Dashboard/Queries/GetDashboardSummaryQuery.cs`
- `apps/web/app/dashboard/page.tsx`, `loading.tsx`
- `apps/web/lib/control-desk.ts`
- `apps/web/app/globals.css` (`.dash-layout`, `.dash-cluster`, `.dash-shortcuts`)

### Verify
- `dotnet test` filter Sprint11: 7 passed
- `npm run build` apps/web: OK

### Follow-ups
- Aging buckets on dashboard (reuse `/api/accounts-payable|receivable/aging`) khi có quyền View Cost/Revenue tách
- Owner drill-down Company→Bill (implementation-plan)
- Chart xu hướng chỉ khi có chuỗi thời gian thật (không stub)

---

## 2026-09-12 — Full-width content panels

### User
phần nội dung hiển thị tràn ngang full page

### Done
- Bỏ `max-width` trên `.panel` / `.panel-wide` / `.main` — nội dung full ngang viewport (giữ padding cạnh).

### Files
- `apps/web/app/globals.css`

---

## 2026-09-12 — Invoika-inspired themes + Settings

### User
Clone theme Invoika horizontal; menu Setting admin; cơ bản + chọn theme mặc định.

### Done
- **Không** copy mã/assets Themesbrand — theme cảm hứng + ADR-0014.
- 3 theme: `invoika` (mặc định) · `soft-purple` · `classic`; layout `horizontal`/`vertical`; density.
- Cookie/localStorage `lcms_ui` + boot script chống FOUC.
- `/settings`: bố cục, mật độ, ngôn ngữ VI khóa, picker theme mặc định.
- Nav **Hệ thống → Cài đặt**; middleware auth `/settings`.

### Files
- `apps/web/lib/ui-preferences.ts`, `components/SettingsForm.tsx`, `app/settings/page.tsx`
- `globals.css`, `AppShell.tsx`, `layout.tsx`, `middleware.ts`
- `VietnameseUiTerms` SETTINGS*; `docs/adr/ADR-0014-ui-theme-preferences.md`

### Verify
- `npm run build` apps/web

### Follow-ups
- Tenant-scoped default theme API + `settings.manage` · icon nav · permission gate Settings

---

## 2026-09-12 — Soft-UI purple theme (Kubayar-inspired)

### User
Full restyle toàn web; khóa tím như mockup; layout soft-UI.

### Done
- Token CSS: accent `#5D5FEF`, bg `#F8F9FB`, radius ~14px / pill, shadow mềm.
- Sidebar trắng + active pill tím; brand mark; nhóm nav Chính / Hàng đợi.
- Topbar card; panel/btn/table/status/form/login đồng bộ soft-UI.
- Giữ Be Vietnam Pro + nhãn CP6.5; không copy widget thẻ tín dụng trang trí.

### Files
- `apps/web/app/globals.css`
- `apps/web/components/AppShell.tsx`
- `apps/web/app/login/page.tsx`

### Verify
- `npm run build` apps/web

### Follow-ups
- Icon line-style cho nav (tùy chọn) · dark mode không làm

---

## 2026-09-12 — Reconciliation UI + bank feed thin

### User
Tiếp theo: reconciliation UI (API sẵn) · bank feed (cần backend).

### Done
- UI đối soát: `/queues/reconciliations`, `/reconciliations`, `/new`, `/{id}` — mở phiên, thêm dòng, hoàn tất.
- `POST /api/reconciliations/{id}/complete`.
- Bank feed (ADR-0013): `bank_feed_lines` + create/list/ignore; UI `/bank-feed`.
- `sourceType=bank_line` khi khớp đủ → dòng sao kê `matched`.
- DoD `UI-RECON-BANKFEED-DOD.md`; test `BankFeedAndCompleteReconciliationTests`.

### Files
- Domain/App/Api: BankFeed*, CompleteReconciliation, ReconciliationDetailWriter, migration `BankFeedLines`
- `apps/web`: reconciliations + bank-feed pages/BFF/components; AppShell/middleware/dashboard
- `docs/adr/ADR-0013-bank-feed-lines.md`, `docs/sprint/UI-RECON-BANKFEED-DOD.md`

### Verify
- `dotnet test --filter BankFeedAndComplete` (2 pass)
- `npm run build` apps/web

### Follow-ups
- Write-off approval gate · CSV/open-banking sync · `billId` filter document list · period close Strict stress

---

## 2026-09-12 — Queue decide: approve/reject + exception resolve

### User
làm tiếp

### Done
- BFF approvals approve/reject; exceptions resolve/close/escalate.
- UI `/queues/approvals`: Phê duyệt / Từ chối (lý do bắt buộc khi reject; multi-step stub).
- UI `/queues/exceptions`: Xử lý · Leo thang · Đóng.
- Deep-link payment/collection từ approval; DoD `UI-QUEUE-DECIDE-DOD.md`.

### Files
- `apps/web/app/bff/approvals/[id]/approve|reject`
- `apps/web/app/bff/exceptions/[id]/resolve|close|escalate`
- `DecideApprovalButton.tsx`, `ExceptionActionButtons.tsx`
- `queues/approvals/page.tsx`, `queues/exceptions/page.tsx`, `lib/control-desk.ts`

### Verify
- `npm run build` apps/web

### Follow-ups
- Manual reconciliation UI · bank feed · write-off approval

---

## 2026-09-12 — Write-off UI + đảo phân bổ

### User
Write-off UI + đảo phân bổ (follow-up ngoài G1–G6).

### Done
- BFF reverse: `payment-allocations|collection-allocations/{id}/reverse` `{ reason }`.
- BFF write-off: `accounts-payable|accounts-receivable/{id}/write-off` `{ amount, reason }`.
- UI: Đảo phân bổ trên chi tiết payment/collection (draft + finalized).
- UI: Xóa nợ trên `/ap-ar` khi outstanding > 0 (trần stub 1000, ADR-0008).
- DoD `docs/sprint/UI-WRITEOFF-REVERSE-DOD.md`.

### Files
- `apps/web/app/bff/payment-allocations/[id]/reverse`, `collection-allocations/.../reverse`
- `apps/web/app/bff/accounts-payable/[id]/write-off`, `accounts-receivable/.../write-off`
- `ReverseAllocationButton.tsx`, `WriteOffButton.tsx`
- `settlements/payments|collections/[id]/page.tsx`, `ap-ar/page.tsx`, `lib/settlements.ts`

### Verify
- `npm run build` apps/web

### Follow-ups
- ~~Queue decide~~ → Done (`UI-QUEUE-DECIDE-DOD.md`) · bank feed · write-off approval · period close Strict stress

---

## 2026-09-12 — UAT UI-only VPS re-run Bill → Close = PASS

### User
Chạy lại UAT UI-only trên VPS — một vòng không Postman (Bill → Close) để xác nhận PASS thật, không chỉ DoD.

### Done
- Browser UAT trên `194.233.89.26` (BFF cookie, **không** Postman/JWT script).
- Bill `UAT-UI-20260912-161500` → Cost confirm 1.1M → Revenue confirm 2.5M → doc accept+match → AP/AR recognize → payment/collection finalize → close **locked** + P&L 1.4M.
- G1–G6 **PASS thật** (settlements hiện `billNo`; AP/AR Đã tất toán; close list Đã khóa).

### Files
- `docs/sprint/UAT-VPS-ONE-ROUND.md`, `UAT-VPS-ONE-ROUND-RESULT.json`

### Verify
- `/health` `/ready` OK; close `01a094e9-a71a-7408-b58b-11a68c461644` locked

### Follow-ups
- ~~Write-off UI · reverse allocation~~ → Done (`UI-WRITEOFF-REVERSE-DOD.md`) · bank feed

---

## 2026-09-12 — G6 FULL: Settlement `billNo`

### User
làm full G6

### Done
- API: `PaymentDto` / `CollectionDto` + list/get join `Bill.BillNo`.
- UI: `/settlements` + chi tiết payment/collection hiện số Bill (không còn «Mở Bill»).
- Test `SettlementBillNoTests`; DoD `UI-G6-DOD.md`; UAT G1–G6 Done.

### Files
- `SettlementQueries.cs`
- `apps/web/lib/settlements.ts`, `settlements/page.tsx`, payments/collections detail
- `SettlementBillNoTests.cs`, `UI-G6-DOD.md`, UAT md/json

### Verify
- `dotnet test --filter SettlementBillNo`
- `npm run build` apps/web

### Follow-ups
- ~~Write-off UI · reverse allocation~~ → Done · bank feed (ngoài UAT gap)

---

## 2026-09-12 — G5 FULL: AP/AR tab Đã tất toán

### User
làm full g5

### Done
- `/ap-ar?status=settled|all` — filter Còn dư / Đã tất toán / Tất cả (giữ theo tab AP|AR).
- Empty còn dư → link sổ đã tất toán khi có data; cột `finalizedSettledAmount` khi settled/all.
- Bill panel: hiện toàn bộ AP/AR Bill (không ẩn settled) + CTA sổ đã tất toán.
- Fix label `partially_settled` → «Tất toán một phần».
- DoD `UI-G5-DOD.md`; UAT G5 → Done; còn G6.

### Files
- `apps/web/lib/ap-ar.ts`, `apps/web/app/ap-ar/page.tsx`
- `BillDocumentsApArPanel.tsx`
- `UI-G5-DOD.md`, UAT md/json

### Verify
- `npm run build` apps/web

### Follow-ups
- G6 settlement `billNo`

---

## 2026-09-12 — G4 FULL: lọc chứng từ theo Bill

### User
triển khai G4 full

### Done
- API: `GET /api/financial-documents?billId=` — khớp header **hoặc** dòng có BillId; list DTO thêm `billId`.
- UI: `/documents?billId=` (chip Bill + giữ filter quick links); Bill panel bảng chứng từ + CTA «Danh sách theo Bill».
- DoD `UI-G4-DOD.md`; UAT G4 → Done; còn G5–G6.

### Files
- `FinancialDocumentQueries.cs`, `FinancialDocumentEndpoints.cs`
- `documents.ts`, `documents/page.tsx`, `BillDocumentsApArPanel.tsx`, `bills/[id]/page.tsx`
- `DocumentBillIdFilterTests.cs`, `UI-G4-DOD.md`, UAT md/json

### Verify
- `dotnet test --filter DocumentBillIdFilter`
- `npm run build` apps/web

### Follow-ups
- G5 AP/AR settled tab · G6 settlement `billNo`

---

## 2026-09-12 — Fix CI #91–#96: Demo seed vs ThresholdApiFactory

### User
Sao lỗi nhiều vậy? (6 run đỏ liên tiếp trên Actions)

### Answer
- Không phải 6 bug khác nhau — **cùng 1 lỗi test** lặp mỗi push. VPS `/health` vẫn OK; deploy không chạy vì job `test` fail trước.
- Root: `appsettings.Development.json` → `Demo:SeedOnStartup=true`. Factory S4/S5 `ThresholdApiFactory` tắt migrate nhưng **không** tắt demo seed → host start seed trước `EnsureCreated` → SQLite `no such table: tenants`.
- Fix: `Demo:SeedOnStartup=false` trên ThresholdApiFactory (S4+S5), giống `LcmsApiFactory` / S6.

### Files
- `tests/LCMS.Api.Tests/Sprint4FullCostTests.cs`
- `tests/LCMS.Api.Tests/Sprint5FullRevenueProfitabilityTests.cs`

### Verify
- `dotnet test` — 2 test threshold xanh; suite full xanh.

---

## 2026-09-12 — UAT G2: gạch Done (stale doc)

### User
G2 cũng đã ship (UI-MATCH-DOD) — bảng UAT chưa gạch Done (chỉ stale doc). Làm luôn G2.

### Done
- `UAT-VPS-ONE-ROUND.md`: G2 → **Done** (`UI-MATCH-DOD.md`); tóm tắt UI = PASS G1–G3.
- `UAT-VPS-ONE-ROUND-RESULT.json`: G1/G2/G3 notes → Done (parity handoff).
- Code Match UI đã ship trước — không đổi `apps/web`.

### Follow-ups
- G4–G6 minor

---

## 2026-09-12 — G3 API+UI: sửa/xóa dòng, party picker, ép sum=header

### User
Cố ý ngoài G3 (API cũng chưa có thì làm luôn API): sửa/xóa dòng · party picker · ép sum = header

### Done
- **ADR-0012** document line integrity.
- API: `PUT/DELETE /api/financial-documents/{id}/lines/{lineId}`; Accept yêu cầu Σ=Total; draft Σ≤Total; sau Accept khóa add/delete/amount; Receive validate CounterpartyId active; audit line add/update/delete.
- UI: Sửa/Xóa dòng; party select lúc nhận; Accept blocked khi lệch tổng; copy ADR-0012.
- Tests: `DocumentLineIntegrityTests`; reorder Accept-after-lines trong Sprint6/6Full/10.

### Files
- `DocumentLineIntegrity.cs`, Update/Delete commands, endpoints, ADR-0012
- `EditDocumentLineForm`, BFF lines/[lineId], `parties.ts`, Receive/Detail/Accept UI
- `UI-G3-DOD.md`

### Verify
- `dotnet test --filter DocumentLineIntegrity|Sprint6`
- `npm run build` apps/web

---

## 2026-09-12 — G3 FULL (prefill + coverage + match CTA)

### User
kiểm tra G3 còn gì nữa không, làm full luôn nhé

### Done
- Prefill số tiền = còn theo header (`total − Σ lines`); soft warn khi vượt tổng.
- Metric đối chiếu header↔dòng; bảng hiện loại + Bill; success sau thêm.
- CTA khi đã accept nhưng chưa có dòng mở (detail + match start + match detail empty).
- DoD `UI-G3-DOD.md` → FULL; non-goals giữ edit/delete / ép sum / G4–G6.

### Verify
- `npm run build` apps/web

---

## 2026-09-12 — G3 UI thêm dòng chứng từ

### User
làm G3

### Done
- Form **Thêm dòng chứng từ** trên `/documents/{id}` (đã nhận + hiệu lực).
- BFF `POST /bff/financial-documents/{id}/lines` → API `AddFinancialDocumentLine`.
- Empty state bỏ “Thêm dòng qua API”; copy: dòng để khớp, không tạo Cost/Revenue.
- DoD: `docs/sprint/UI-G3-DOD.md`; cập nhật gap G3 trong UAT + follow-up Match.

### Files
- `AddDocumentLineForm.tsx`, `bff/financial-documents/[id]/lines`, `documents/[id]/page`, `lib/documents.ts`

### Follow-ups
- G4–G6 minor (`billId` filter documents; AP/AR settled tab; settlement `billNo`)

### Verify
- `npm run build` apps/web
- VPS: chứng từ đã nhận → thêm dòng → khớp

---

## 2026-09-12 — G1 FULL (confirm override + exposure link)

### User
G1 lát mỏng? Còn gì thì làm full.

### Done
- Confirm/Actual dialog: chỉnh `confirmedAmount` / `actualAmount` (parity UAT Expected≠Confirmed).
- Exposure: gắn cost/revenue (select khi có Bill), prefill amount/currency từ query; CTA **Exposure** trên dòng Cost/Revenue.
- Recognize: prefill hạn từ exposure.
- DoD `UI-G1-DOD.md` cập nhật FULL; non-goals giữ shared allocate / party / G3.

### Verify
- `npm run build` apps/web

---

## 2026-09-12 — G1 UI tạo Bill / Cost / Revenue / Exposure→Recognize


### User
G1 — UI tạo Bill / Cost / Revenue / Exposure→Recognize; làm hết full G1.

### Done
- Form tạo: Bill (`/bills/new`), Cost/Revenue trên Bill, Exposure payable/receivable, Recognize → AP/AR.
- BFF POST → API Pass 2 sẵn có; copy VI: Exposure ≠ AP/AR; Cost ≠ Payment; partial recognize; không nhập outstanding.
- CTA trên `/bills`, Bill panel, `/ap-ar` (+ nút Ghi nhận trên dòng exposure).
- DoD: `docs/sprint/UI-G1-DOD.md`; cập nhật gap G1 trong `UAT-VPS-ONE-ROUND.md`.

### Files
- `apps/web/app/bff/bills|costs|revenues|payable-exposures|receivable-exposures/**`
- Forms + pages `bills/new`, `bills/[id]/costs|revenues/new`, `ap-ar/exposures/**`
- `BillCostRevenuePanel`, `BillDocumentsApArPanel`, `ap-ar/page`, `bills/page`, `lib/ap-ar.ts` (get by id)

### Follow-ups
- Shared cost create/allocate UI
- ~~UAT G3 add document line~~ → Done; G4–G6 minor

### Verify
- `npm run build` apps/web
- VPS: UI tạo Bill→Cost→Revenue→Exposure→Recognize

---

## 2026-09-12 — Match UI (defer U4 / UAT G2)

### User
Match UI (còn defer từ U4).

### Done
- Document detail: CTA **Khớp chứng từ** khi đã nhận + đã chấp nhận + còn số mở.
- `/documents/{id}/match` — mở phiên nháp (`line_to_cost` / `line_to_revenue` / `line_to_line`).
- `/documents/{id}/matches/{matchId}` — thêm chi tiết, đảo chi tiết, hủy phiên (BFF → `/api/document-matches`).
- Copy: khớp = liên kết; không tạo Cost/Revenue. Triad MatchingStatus cập nhật sau add/reverse.
- DoD: `docs/sprint/UI-MATCH-DOD.md`; cập nhật follow-up U4/U5.

### Files
- `apps/web/app/documents/[id]/match/**`, `…/matches/[matchId]/**`
- `apps/web/app/bff/document-matches/**`
- `apps/web/lib/document-matches.ts`, `document-matches-server.ts`
- Components Start/Add/Reverse/Cancel match; document detail CTA

### Follow-ups
- List matches by `primaryDocumentId` (API).
- ~~Add document line UI (UAT G3)~~ → Done (`UI-G3-DOD.md`).
- Confirm-match workflow nếu cần.

### Verify
- `npm run build` apps/web xanh
- VPS (sau deploy): chứng từ accepted → khớp → matchingStatus đổi

---

## 2026-09-12 — UAT VPS một vòng Bill → Close + gap thật

### User
UAT trên VPS — một vòng Bill → Cost/Revenue → chứng từ/AP-AR → Settlement → Close; ghi gap thật. Giải thích ngắn + triển khai.

### Done
- Chạy vòng **API** trên `194.233.89.26` với JWT bootstrap `ops@cms.local` (password chỉ trên host `infra/.env`): Bill `UAT-20260912-111445` → Cost/Revenue confirm → document accept+match → AP/AR recognize → payment/collection finalize → financial close **locked** + P&L. **API = PASS.**
- Smoke **UI** browser: login → dashboard/bills/documents/ap-ar/settlements/financial-closes. Profile số khớp API; close hiện Đã khóa. **UI = PARTIAL** (không tạo đủ entity từ UI).
- Ghi gap: `docs/sprint/UAT-VPS-ONE-ROUND.md` + `UAT-VPS-ONE-ROUND-RESULT.json` (token redact). Script tái chạy: `scripts/uat-vps-one-round.ps1`.

### Gaps ưu tiên
- **G1 blocker:** ~~UI thiếu tạo Bill / Cost / Revenue / Exposure→Recognize~~ → Done (`UI-G1-DOD.md`).
- **G2 major:** ~~thiếu match chứng từ~~ → Done (`UI-MATCH-DOD.md`); **G3:** ~~add document line~~ → Done (`UI-G3-DOD.md`).
- **G4–G6 minor:** `billId` filter documents; AP/AR ẩn settled; settlement list thiếu `billNo`.

### Verify
- `/health` `/ready` 200; UI `/login` 200
- Bill UAT trên `/bills`; close locked trên `/financial-closes`

---

## 2026-09-12 — Demo seed: đủ case bấm test UI

### User
Seed demo dữ liệu thật tất cả các trường hợp có thể xảy ra để bấm test.

### Done
- `DemoDataSeeder` idempotent (marker bill `DEMO-SEED-MARKER`) vào tenant `ops`.
- Cover: Bill hub / empty; cost+revenue maturity; shared cost draft+finalized; chứng từ Received≠Accepted≠Matched (+ rejected/cancelled); exposure/AP/AR open→partial→settled + write-off; payment/collection draft/finalized/reversed/cancelled; close open/locked(+snapshot)/reopened + period; exceptions/approvals/recon/variance; parties + rate card draft/published.
- Startup: `Demo:SeedOnStartup` (dev default true). Endpoint: `POST /api/dev/seed-demo` (Dev hoặc `Demo:AllowEndpoint=true`).
- Compose/env: `Demo__SeedOnStartup`, `Demo__AllowEndpoint`, `Demo__TenantCode`.

### Files
- `src/LCMS.Application/Demo/DemoDataSeeder.cs`, `DemoOptions.cs`
- `DependencyInjection.cs`, `Program.cs`, `DevAuthEndpoints.cs`
- `appsettings.json`, `appsettings.Development.json`
- `infra/docker-compose.host.yml`, `infra/.env.example`

### How to seed VPS
1. Trong `/opt/cms/infra/.env`: `Demo__SeedOnStartup=true` (hoặc `Demo__AllowEndpoint=true` rồi `POST /api/dev/seed-demo`).
2. `docker compose … up -d api` (recreate). Re-run an toàn — đã có marker thì skip.

### Click map (BillNo)
| Bill | Mục đích |
|------|----------|
| DEMO-01-HUB | Cost/Revenue expected→confirmed→actual |
| DEMO-02-SHARE-A/B | Shared cost + allocation |
| DEMO-03-AP | AP + thanh toán nháp/chốt/đảo |
| DEMO-04-AR | AR + thu tiền |
| DEMO-05-DOCS | Chứng từ đủ triad |
| DEMO-06/07/08-CLOSE-* | Chốt mở / khóa / mở lại |
| DEMO-09-CONTROL | Queue ngoại lệ / phê duyệt |
| DEMO-10-EMPTY | Empty state |

### Verify
- `dotnet build src/LCMS.Api` xanh
- Local Dev: restart API → bills `DEMO-*`; hoặc `POST /api/dev/seed-demo`

---

## 2026-09-12 — Pass UI / Sprint U5 Settlement + Close

### User
UI Settlement + Close (sau U4).

### Done
- `/settlements` — tab Thanh toán / Thu tiền; tạo; detail phân bổ nháp + **chốt phân bổ** (BFF → S8 APIs).
- `/financial-closes` — list/filter; mở chốt (kỳ/Bill); **tạo snapshot** (primary); mở lại; P&L đọc từ snapshot.
- Bill panel: CTA tạo payment/collection + chốt theo Bill; AP/AR copy trỏ `/settlements`.
- Nav + middleware; docs `PROMPT-UI-5.md`, `UI-5-DOD.md`; README/PLAN/orchestration U5.

### Files / API
- UI: `apps/web/app/settlements/**`, `apps/web/app/financial-closes/**`, `lib/settlements.ts`, `lib/financial-closes.ts`, BFF payments/collections/financial-closes, settlement/close components
- API (unchanged): `/api/payments`, `/api/collections`, `/api/financial-closes` (+ snapshot/reopen/pnl)

### Verify
- `npm run build` trong `apps/web`
- Sau deploy: `/settlements`, `/financial-closes`, `/health`

### Next / Follow-ups
- Reverse allocation UI; write-off/recognize; match UI; documents `billId` filter API.

### Residual
- Không có reverse từ UI; list AP/AR trên allocate lọc currency (+ bill soft preference).

---

## 2026-09-12 — U4 local takeover: BFF 201 fix + VPS verify

### User
Implement U4 alone; prior cloud id unreachable. Finish existing branch if present; ship; verify VPS.

### Done
- Confirmed U4 already on `main` (`0953cce` / PR #27): `/documents`, `/ap-ar`, Bill AP/AR panel, receive/accept BFF.
- Fixed BFF `forwardApiMutation`: **201** now forwards JSON body (`{ id }`) so nhận chứng từ redirects to detail (was empty body).
- Docs already mark **Pass UI COMPLETE (U0–U4)**; follow-ups stay match UI + settlement UI.

### Files
- `apps/web/lib/bff-api.ts`

### Verify
- `npm run build` apps/web xanh (local).
- VPS stale web (404 `/documents`) after Actions race — fallback sync `apps/web` + `docker compose build --no-cache web` (never touched `infra/.env` / alogex).
- Smoke: BFF login **200** → `/documents` `/ap-ar` `/documents/receive` **200** + VI markers (Chứng từ / phải trả / Số dư); `/health` `/ready` **200**.

### Residual
- List documents still no `billId` filter on API list DTO.
- Match UI deferred (settlement → U5).

---

## 2026-09-12 — Coordinator: U4 merged — **Pass UI COMPLETE**

### User
Follow-up after [UI-4](bc-9c035d0b-b5a7-44ca-9511-2927f0d3c03d) PR #27.

### Done
- Rebased UI-4 onto main (handoff/orchestration conflicts); CI #80 green on prior tip.
- Merged to `main` → `0953cce` (**Pass UI COMPLETE** U0–U4). Closed PR #27 (history already on main).

### Next
- VPS smoke `/documents` `/ap-ar` after Actions deploy; follow-ups in `UI-4-DOD.md`.

---

## 2026-09-12 — Pass UI / Sprint U4 Documents & AP/AR (thin)

### User
Ship U4: chứng từ Received ≠ Accepted ≠ Matched nhìn thấy; outstanding AP/AR đọc được (thin). Copy CP6.5; không gộp Cost = Payment. Chỉ `apps/web/**` + docs. PR vào main. Pass UI track complete nếu U4 là sprint cuối.

### Done
- `/documents` list + filters (chờ chấp nhận / đã chấp nhận chưa khớp); triad trạng thái VI.
- `/documents/receive` — Nhận chứng từ (BFF → `POST /api/financial-documents`); chỉ đặt Received.
- `/documents/[id]` — detail + Chấp nhận (BFF accept); lines open/matched; không match UI.
- `/ap-ar` — AP / AR outstanding + tab Exposure (read); copy AP ≠ Cost, AR ≠ Revenue.
- Bill detail: panel AP/AR outstanding theo `billId` + CTA nhận chứng từ gắn Bill.
- Nav AppShell + middleware; approval queue deep-link chứng từ khi `objectType` document.
- Docs: `UI-4-DOD.md`; README/orchestration **U4 Done** → **Pass UI COMPLETE**.
- Rebased onto main (incl. U3 VPS verify); PR #27.

### Files / API
- UI: `apps/web/app/documents/**`, `apps/web/app/ap-ar/**`, `lib/documents.ts`, `lib/ap-ar.ts`, components Document*/BillDocuments*, BFF financial-documents
- API (unchanged): financial-documents, accounts-payable/receivable, payable/receivable-exposures, terminology

### Verify
- `npm run build` trong `apps/web`
- Sau merge/Actions: `/documents`, `/ap-ar`, Bill AP/AR block, `/health`

### Next / Follow-ups
- API: list financial-documents thiếu `billId` filter/DTO — không lọc chứng từ theo Bill trên UI.
- Match UI + Settlement/Payment UI (non-goals U4).
- Approve/reject từ queue.

### Residual risks
- List chứng từ trên Bill không filter được (thiếu field API) — CTA + danh sách chung trung thực.
- Recognize/write-off/settlement chưa có UI — chỉ đọc outstanding.

---

## 2026-09-12 — U3 VPS verify + fallback redeploy (local takeover)

### User
Implement U3 Control desk alone; prior cloud id unreachable — pull latest `main`; verify VPS.

### Done
- Confirmed U3 already on `main` (PR #26 → `657be51` / `e5bb257`): `/dashboard`, `/queues/exceptions`, `/queues/approvals`, Dashboard nav live, `UI-3-DOD.md`.
- VPS web was still U2 (dashboard/queues **404**) after Actions race with U4 kickoff.
- Fallback operator redeploy (tar+scp → `compose up -d --build` web/api/proxy); **never** touched `infra/.env` / alogex.
- Smoke: BFF login **200** → dashboard/exceptions/approvals **200** + VI markers; `/health` `/ready` OK.
- `dotnet test Cms.sln -c Release` → **97 passed**.

### Next
- **U4 can start / continue** (cloud [UI-4](bc-9c035d0b-b5a7-44ca-9511-2927f0d3c03d) already kicked off).

---

## 2026-09-12 — Coordinator: U3 merged → U4 kickoff

### User
Follow-up after [UI-3](bc-e514db5a-484e-4333-a372-f8d08e0cdb32) PR #26.

### Done
- CI #76 green; undraft + merge PR #26 → `657be51`.
- Kicked off [UI-4](bc-9c035d0b-b5a7-44ca-9511-2927f0d3c03d) Documents & AP/AR thin.

### Next
- Merge U4 PR → Pass UI COMPLETE.

---

## 2026-09-12 — Pass UI / Sprint U3 Control desk

### User
Ship U3: Dashboard tóm tắt + hàng đợi Exception / Approval (CP6.5 VI). DoD mỏng: `/` hoặc `/dashboard` từ `GET /api/dashboard/summary`; queues từ API Pass 2; link Bill khi có id; empty/loading/error thật. Chỉ `apps/web/**` + docs. PR vào main.

### Done
- `/` → redirect `/dashboard`; summary cards (exception/approval/overdue/bill/variance/close counts) + totals by currency + FX stub roll-up note.
- `/queues/exceptions` (+ `?overdueOnly=1`), `/queues/approvals` — table VI, link `Mở Bill` khi `billId` / `objectType=bill`.
- AppShell nav: Dashboard + Bill + hai hàng đợi (bỏ placeholder “sắp có”).
- Docs: `UI-3-DOD.md`; README Pass UI U3 Done; orchestration U3 Done / U4 Ready.

### Files / API
- UI: `apps/web/app/dashboard/**`, `apps/web/app/queues/**`, `apps/web/lib/control-desk.ts`, `AppShell`, `middleware`, `globals.css`
- API (unchanged): `GET /api/dashboard/summary`, `/api/queues/exceptions`, `/api/queues/approvals`, `/api/terminology`

### Verify
- `npm run build` trong `apps/web` (pre-merge)
- Sau merge/Actions: `http://194.233.89.26/dashboard` + `/queues/*` + `/health`

### Next
- Merge U3 PR → kickoff U4 Documents & AP/AR thin.
- Follow-up: approve/reject UI; deep-link cost/revenue từ approval object.

### Residual risks
- Approval `objectType` ≠ bill: không có màn chi tiết — hiện “—” (trung thực).
- Counts/totals là projection read-only (ADR-0011); không SoT.

---

## 2026-09-12 — Coordinator: U2 landed → U3 kickoff

### User
Follow-up after [UI-2](bc-5377dafd-4e8b-4761-801f-ae76de6079b7) completed (PR #24).

### Done
- Rebased UI-2 onto main (conflict handoff/orchestration); force-pushed branch; CI green then commits landed on `main` (`01d8a50` / `c6210b7`). PR #24 closed (not squash-merged — history already on main).
- Pass 2 already COMPLETE on main; kicked off [UI-3](bc-e514db5a-484e-4333-a372-f8d08e0cdb32) Control desk.

### Next
- Merge U3 PR when green; then U4.

---

## 2026-09-12 — Pass 2 Sprint 12 FULL Hardening & UAT (**Pass 2 COMPLETE**)

### User
Follow-up after Pass2 Sprint12 Hardening Full — merge final Pass 2 sprint.

### Done
- Merged `cursor/sprint-12-full-hardening-uat-cb6e` onto `main` (S11 already on main).
- Audit richer before/after JSON; cover finalize, document accept/match, AP/AR recognize, write-off; audit filters `from`/`to`.
- Integration recovery: mark-retried / dead-letter; duplicate 409 C-002; outbox enqueue + process-once.
- Rate-limit money paths; AC-007/008/009 smoke; VI gaps; `SPRINT-12-FULL-DOD.md` → **Pass 2 COMPLETE**.
- README Pass 2 board = all Done.

### Files / API
- `/api/integration-errors*`, `/api/outbox*`; migration `Sprint12Full_HardeningRecovery`
- DoD: `docs/sprint/SPRINT-12-FULL-DOD.md`

### Verify
- `dotnet test Cms.sln -c Release` → **97 passed** (S12 FULL +4 on main after S11)

### Next
- Pass UI continues (U3+). Residual backlog in SPRINT-12-FULL-DOD only.

---

## 2026-09-12 — Pass UI / Sprint U2 Cost & Revenue confirm

### User
Ship U2: trên Bill detail xác nhận Chi phí / Doanh thu (CTA rõ; maturity không silent overwrite). List + confirm/actualize dialog VI; refresh profile; 403/409 honest. Ship per 06. Chỉ `apps/web` + docs.

### Done
- Bill detail: list `GET /api/costs?billId=` + `GET /api/revenues?billId=`.
- Actions: Xác nhận (Expected→Confirmed) primary; Ghi nhận Thực tế (Confirmed→Actual) secondary; dialog VI giải thích không ghi đè lớp trước.
- BFF cookie→Bearer: `/bff/costs|revenues/[id]/confirm|actualize`; lỗi API VI; 403/409 khóa nút + refresh; `router.refresh` sau mutate.
- Docs: `UI-2-DOD.md`; README/orchestration U2 Done.
- Rebased onto main (after CI smoke redirect fix).

### Files / API
- UI: `BillCostRevenuePanel`, `bills/[id]/page.tsx`, `lib/costs-revenues*.ts`, `lib/bff-api.ts`, `lib/terminology.ts`, `app/bff/costs/**`, `app/bff/revenues/**`
- API unchanged (confirm/actualize đã có trên main)

### Verify
- `npm run build` (apps/web) xanh; `dotnet test` **93 passed** (pre-S12 push; S12 later → 97)
- VPS: login BFF 200 → `/bills` → Bill detail shows `maturity-actions` / Xác nhận / Ghi nhận; `POST /bff/costs/{id}/confirm` → **204**; `/health` OK
- Actions: U2 on `main` (`c6210b7`); run #72 cancelled by S12 concurrency — later deploy (post-S12 / UI-3 kickoff) rebuilt `cms-web` with U2

### Next
- U3 Control desk (already kicked off on Actions)
- Optional amount override trong dialog

### Follow-ups
- Permission codes riêng cho confirm (hiện 403 runtime)
- Shared allocation UI (non-goal U2)
- 409 cũng khóa nút + refresh (U2 polish)

---

## 2026-09-12 — Fix CI deploy smoke: follow `/` → `/login` redirect

### User
Những lỗi CI trên Actions (run đỏ liên tiếp) — tự fix luôn chứ?

### Answer
- VPS `/health` `/ready` OK; app chạy. Fail ở **deploy Health check**, không phải `dotnet test`.
- Root cause (từ UI-0): unauthenticated `/` trả **307 → `/login`**. Smoke dùng `curl -fsS` **không** follow redirect → body chỉ `/login` (6 bytes) → grep HTML fail → đỏ từ #61 trở đi (sau JWT fix #58–#60).
- Fix: `curl -fsSL` + kiểm tra marker `Đăng nhập|CMS` trên HTML sau redirect. JWT SigningKey trên host đã đủ từ trước.

### Files
- `.github/workflows/ci.yml` (Health check)
- `docs/ops/github-actions.md`

### Verify
- Local repro: `curl -fsS /` → no HTML; `curl -fsSL /` → login HTML.
- Actions: push main → CI `deploy` smoke xanh; http://194.233.89.26/health

### Next
- Re-run cũ không bắt buộc; run mới sau push là đủ.

---

## 2026-09-12 — Coordinator: S11 FULL merged → S12 FULL kickoff

### User
Follow-up after Pass2 Sprint11 Profile Full cloud agent completed.

### Done
- Merged Sprint 11 FULL (`cursor/sprint-11-full-financial-profile-reporting-31b9`); suite target **93**.
- README: S11 Done; S12 In progress (last Pass 2 FULL).
- Added `docs/sprint/PROMPT-SPRINT-12-FULL.md`.
- Launched cloud agent Pass2 Sprint12 Hardening Full.

### Next
- Merge Sprint 12 FULL PR → declare **Pass 2 COMPLETE**.

---

## 2026-09-12 — Sprint 11 FULL Financial Profile & Reporting (Pass 2)

### User
Pass 2 Sprint 11 FULL: asOf maturity reconstruct; dashboard FX roll-up + variance/overdue counts; queue filters; close snapshot P&L; tests + DoD + ADR + PR. Never secrets/alogex.

### Done
- Financial profile: asOf reconstructs Confirmed/Actual via `ConfirmedAt`/`ActualizedAt`; settlement outstanding from finalized allocations at asOf; residual limits on DTO note + DoD.
- Dashboard: `OpenVarianceCount`, `OverdueExceptionCount`; optional `BaseCurrencyRollUp` via Cost/Revenue FX stub (ADR-0011).
- Queues: exceptions (status/severity/overdue/objectType), approvals (status/objectType/requiredLevel), `GET /api/queues/reconciliations` stub.
- Close P&L: `GET /api/financial-closes/{id}/pnl` derived from immutable snapshot metrics only.
- Derived read-only — no totals written onto Bill. VI labels/errors.
- Tests: `Sprint11FullFinancialProfileReportingTests` (4); suite **93 passed**.

### Files / API
- Profile: `GetBillFinancialProfileQuery`
- Dashboard/queues: `GetDashboardSummaryQuery`, `ControlQueueQueries`, `DashboardReportingEndpoints`
- P&L: `GetFinancialClosePnlQuery` + `FinancialCloseEndpoints`
- ADR: `docs/adr/ADR-0011-dashboard-fx-rollup-close-pnl.md`
- DoD: `docs/sprint/SPRINT-11-FULL-DOD.md`

### Verify
- `dotnet test Cms.sln -c Release` → **93 passed**

### Deferred / Next
- Pass 2 Sprint 12 Hardening FULL
- Real FX table; Next.js dashboard UI

---

## 2026-09-12 — CI concurrency + Pass2∥UI ship rules

### User
Làm những gì tốt nhất cho agent Sprint và Agent U chạy song song.

### Done
- `.github/workflows/ci.yml`: `concurrency` per ref + `cancel-in-progress` — hết dual deploy race trên VPS.
- `README-AGENTS.md` + `PASS-UI-ORCHESTRATION.md`: quy tắc serialize merge/deploy; PR ưu tiên khi Pass 2 ∥ Pass UI.

### Verify
- Actions: run mới hủy run cũ cùng `main`; `/health` sau deploy.

### Next
- U2 cloud in progress: [UI-2](bc-5377dafd-4e8b-4761-801f-ae76de6079b7). S11 merge tuần tự khi PR sẵn (không đua push `main`).

---

## 2026-09-12 — Pass UI / Sprint U1 Bill hub

### User
Ship Bill hub: `/bills` list+search → `/bills/[id]` financial profile (Expected/Confirmed/Actual) + profitability. Enable Bill nav. DoD + handoff + ship. No Cost mutate (U2).

### Done
- `/bills` list + `?q=` search via existing `GET /api/bills`; auth Bearer from `lcms_at` cookie.
- `/bills/[id]` loads `financial-profile` + `profitability?view=best`; maturity table scannable; money `vi-VN`.
- Terminology labels; empty/error/loading; AppShell Bill link live; Dashboard still “sắp có”.
- No new backend — APIs already on main. Docs: `UI-1-DOD.md`, README/orchestration U1 Done.

### Files / API
- UI: `apps/web/app/bills/**`, `lib/bills.ts`, `lib/money.ts`, `components/AppShell.tsx`
- API (unchanged): `GET /api/bills`, `/api/bills/{id}`, `.../financial-profile`, `.../profitability`, `/api/terminology`

### Verify
- Local: `dotnet test` **89 passed**; `npm run build` (web) OK
- VPS after deploy (`4aeab7a`): login OK; `/bills` 307→login then 200; search `?q=`; detail maturity Dự kiến/Đã xác nhận/Thực tế; `/health` OK
- Actions: https://github.com/thanhquyen129/CMS/actions

### Next
- U2 Cost & Revenue confirm actions on Bill

---

## 2026-09-12 — Coordinator: S10 FULL merged → S11 FULL kickoff

### User
Follow-up after Pass2 Sprint10 Close Full cloud agent completed.

### Done
- Merged Sprint 10 FULL (`cursor/sprint-10-full-financial-close-4de8`); suite target **86**.
- README: S10 Done; S11 In progress.
- Added `docs/sprint/PROMPT-SPRINT-11-FULL.md`.
- Launched cloud agent Pass2 Sprint11 Profile Full.

### Next
- Merge Sprint 11 FULL PR → kickoff Sprint 12 Hardening FULL.

---

## 2026-09-12 — Sprint 10 FULL Financial Close (Pass 2)

### User
Pass 2 Sprint 10 FULL: eligibility checklist (critical exceptions / unmatched accepted docs / unsettled AP-AR threshold); period lock on confirm + allocate/finalize; Controlled vs Strict policy; reopen/reclose append-only; tests + SPRINT-10-FULL-DOD + ADR + PR. Never secrets/alogex. Vietnamese errors.

### Done
- Eligibility checklist (`FinancialClose:Eligibility`, default on): three gates with distinct VI Conflict reasons; Strict forces all gates.
- Period lock (`FinancialClose:EnforcePeriodLock` default true): Locked close blocks Cost/Revenue confirm and Payment/Collection allocate/finalize in scope; Strict always enforces.
- Policy `controlled` | `strict` stored on close + copied to snapshot; reopen/reclose never mutates old snapshots (C-010 / AC-008).
- ADR-0010; VI terms; `SPRINT-10-FULL-DOD.md`; Pass 1 DoD deferred marked done for eligibility/period lock.
- Tests: `Sprint10FullFinancialCloseTests` (3); suite **86 passed**.

### Files / API / Config
- Application: `FinancialCloseOptions`, `CloseEligibilityChecker`, `PeriodLockGate`; snapshot command uses checker; confirm/allocate/finalize call period lock.
- Config: `FinancialClose` section in `appsettings.json`
- ADR: `docs/adr/ADR-0010-financial-close-eligibility-period-lock.md`
- DoD: `docs/sprint/SPRINT-10-FULL-DOD.md`

### Verify
- `dotnet test Cms.sln -c Release` → **86 passed**

### Deferred / Next
- Pass 2 Sprint 11 Profile/Reporting FULL
- Per-tenant eligibility tables; Next.js close UI

---

## 2026-09-12 — CI #58–#60 deploy fail: missing JWT signing key on VPS

### User
Những lỗi CI trên main giải quyết thế nào? Fix ngay hay đợi build sau?

### Answer
- Fail ở job **deploy** / step **Compose up on host**, không phải job `test` (PR xanh vì không chạy deploy).
- Root cause: `/opt/cms/infra/.env` thiếu `Auth__Jwt__SigningKey` (compose bắt buộc kể từ Sprint 0 JWT). Đợi push sau **không** hết — cùng lỗi.
- Fixed ngay trên `cms-sg-01`: thêm SigningKey + khôi phục `LCMS_DB_PASSWORD`; `compose up -d --build`. Verify `/health` `/ready` `/api/terminology` OK; `/` → 307 (web).
- Secret chỉ trên host — không commit. Re-run Actions (optional) sẽ xanh sau khi `.env` đủ.

### Next
- Optional: Re-run failed workflow trên Actions tab để ghi nhận xanh.
- Operator: set `Auth__Bootstrap__*` trên host nếu cần login UI-0.

---

## 2026-09-12 — Pass UI / Sprint U0 Scaffold + login + proxy

### User
Ship Next.js to VPS: `/` = Vietnamese UI shell + login (not API JSON). Proxy `/api`→API. JWT per ADR-0002. BFF httpOnly cookie (ADR-0007).

### Done
- `apps/web` Next.js 15 App Router: `/login`, `/` shell; Bill/Dashboard “sắp có”; terminology VI labels.
- API: `POST /api/auth/login`; `users.password_hash`; env bootstrap `Auth:Bootstrap:*`; `JwtTokenIssuer`.
- Infra: `Dockerfile.web`, nginx proxy (`/`→web, `/api|/health|/ready|/metrics`→api); API no longer on host `:80`.
- ADR-0007 BFF cookie; CI post-deploy smoke includes HTML `/` + terminology.
- Tests: `Ui0AuthLoginTests` (3).

### Files / API / Config
- UI: `apps/web/**`; BFF `/bff/auth/*`
- API: `/api/auth/login`; migration `UI0_UserPasswordHash`
- Compose: `web` + `proxy` services; bootstrap env on host
- ADR: `docs/adr/ADR-0007-bff-httponly-jwt-cookie.md`
- DoD: `docs/sprint/UI-0-DOD.md`

### Host note
Set `Auth__Bootstrap__Email` / `Auth__Bootstrap__Password` in `infra/.env` on VPS so login works (never commit).
After U0 deploy: seeded `ops@cms.local` on host (password only in `infra/.env` — rotate as needed).

### Verify (prod)
- `/` → 307 → `/login` HTML (nginx → web)
- `/health` + `/ready` OK
- `/api/terminology` OK
- `POST /api/auth/login` + `/bff/auth/login` OK with bootstrap user

### Next
- U1 Bill hub (`PROMPT-UI-1.md`)
- Operator: confirm/rotate bootstrap password on host

---

## 2026-09-12 — Coordinator: S9 FULL merged → S10 FULL kickoff

### User
Follow-up after Pass2 Sprint9 Control Full cloud agent completed.

### Done
- Merged Sprint 9 FULL (`cursor/sprint-9-full-financial-control-6616`); suite target **83**.
- README: S9 Done; S10 In progress.
- Added `docs/sprint/PROMPT-SPRINT-10-FULL.md`.
- Launched cloud agent Pass2 Sprint10 Close Full.

### Next
- Merge Sprint 10 FULL PR → kickoff Sprint 11 Profile/Reporting FULL.

---

## 2026-09-12 — Sprint 9 FULL Financial Control (Pass 2)

### User
Pass 2 Sprint 9 FULL: reconciliation batch + auto variance severity thresholds; exception SLA/inbox + multi-step approval stub (approval ≠ permission); optional block Cost/Revenue confirm on critical open exception; tests + SPRINT-9-FULL-DOD + PR. Never secrets/alogex. Vietnamese errors.

### Done
- Batch recon: `POST /api/reconciliations/{id}/details/batch`; shared writer auto-creates Variance (not Exception) with severity from thresholds.
- Exception: object link, default SLA hours, escalate stub, inbox filters (`status`/`severity`/`objectType`/`overdueOnly`).
- Approval: `requiredLevel` 1|2 multi-step; reject requires reason; never touches Permission.
- Confirm block: `FinancialControl:BlockConfirmOnCriticalException` default true for Cost/Revenue.
- Migration `Sprint9Full_FinancialControl`; ADR-0009; VI terms.
- Tests: `Sprint9FullFinancialControlTests` (3); suite **83 passed**.

### Files / API / Config
- APIs: details/batch; exceptions escalate + filters; approvals requiredLevel
- Application: FinancialControlOptions, VarianceSeverityCalculator, CriticalExceptionConfirmGate, ReconciliationDetailWriter
- Config: FinancialControl section in appsettings.json
- Migration: 20260912023712_Sprint9Full_FinancialControl
- ADR: docs/adr/ADR-0009-financial-control-severity-sla-approval.md
- DoD: docs/sprint/SPRINT-9-FULL-DOD.md

### Verify
- `dotnet test Cms.sln -c Release` → **83 passed**

### Deferred / Next
- Pass 2 Sprint 10 FULL Financial Close
- Auto variance→exception; per-tenant SLA tables; Next.js inbox UI

---

## 2026-09-12 — Coordinator: S8 FULL merged → S9 FULL kickoff

### User
Follow-up after Pass2 Sprint8 Settle Full cloud agent completed.

### Done
- Merged Sprint 8 FULL onto `main`; verified `dotnet test` → **80 passed**.
- README Pass 2: S8 Done; S9 In progress.
- Added `docs/sprint/PROMPT-SPRINT-9-FULL.md`; pushed `325d80d`.
- Launched cloud agent Pass2 Sprint9 Control Full.

### Next
- Merge Sprint 9 FULL PR when ready → kickoff Sprint 10 Close FULL.

---

## 2026-09-12 — Sprint 8 FULL Settlement (Pass 2)

### User
Pass 2 Sprint 8 FULL: unapplied cash + multi-allocation; C-008 over-allocate reject; FX stub base_amount on settlement; write-off stub with reason; idempotent finalize; tests + SPRINT-8-FULL-DOD + PR. Never secrets/alogex. Vietnamese errors.

### Done
- Unapplied / AvailableToAllocate: multi-AP/AR allocate from one payment/collection; subsequent allocate until fully applied; over → 409 C-008 VI.
- FX stub: `Settlement:BaseCurrency` + `StubFxRatesToBase` fills `base_amount` on create/allocate; `fx_rate_id` null (ADR-0004/0008); missing rate → VI validation.
- Write-off: `POST /api/accounts-payable|accounts-receivable/{id}/write-off` — negative AdjustmentAmount + `[xóa nợ]` note; capped by `MaxWriteOffAmount`; never silent wipe / never invents Cost/Revenue.
- Idempotent finalize: re-finalize finalized allocation → 204 no-op; reversed → 409 VI.
- Migration `Sprint8Full_Settlement`; ADR-0008; VI terms WRITE_OFF, SETTLEMENT_BASE_AMOUNT, IDEMPOTENT_FINALIZE.
- Tests: `Sprint8FullSettlementTests` (3); suite **80 passed**.

### Files / API / Config
- APIs: write-off endpoints; GET payment/collection + baseAmount/fxRateId; finalize idempotent
- Application: Settlements/SettlementOptions, SettlementFxStub; create/allocate/finalize; WriteOff* commands
- Config: Settlement section in appsettings.json
- Migration: 20260912022427_Sprint8Full_Settlement
- ADR: docs/adr/ADR-0008-settlement-fx-writeoff.md; ADR-0004 extended

### Verify
- `dotnet test Cms.sln -c Release` → **80 passed**

### Deferred / Next
- Pass 2 Sprint 9 FULL Financial Control / reconciliation
- Bank feed; real fx_rates; write-off approval

---

## 2026-09-12 — Pass UI plan (parallel track)

### User
Lên kế hoạch chạy UI ngay — http://194.233.89.26/ hiện chỉ JSON API.

### Done
- Plan: docs/sprint/PLAN-UI.md — U0→U4 song song Pass 2 FULL; nginx proxy /→Next.js, /api→API; login JWT mỏng (ADR-0007 trong U0).
- Prompts: PROMPT-UI-0.md … PROMPT-UI-4.md.
- docs/sprint/README-AGENTS.md cập nhật Status Pass UI = Ready (start U0).
- Resolved merge conflict with Sprint 7 FULL (aging ADR-0006).

### Next
- Kickoff **U0**: paste PROMPT-UI-0.md vào New Chat / cloud agent riêng.
- Pass 2: paste PROMPT-SPRINT-8-FULL.md cho Settlement FULL.
- Sau U1: Bill list + financial profile trên VPS.

---

## 2026-09-12 — Sprint 7 FULL Exposure + AP/AR (Pass 2)

### User
Pass 2 Sprint 7 FULL: partial/multi recognition + recognized_amount; aging buckets on AP/AR; optional document→exposure link (no Cost/Revenue); outstanding respects settlements; tests + SPRINT-7-FULL-DOD + PR. Never secrets/alogex. Vietnamese errors.

### Done
- Multi-recognize: 
ecognized_amount recomputed from sum of AP/AR slices before each recognize; over-recognize / already-full → 409 VI; GET exposure returns 
ecognitions[].
- Aging (ADR-0006): daysPastDue + gingBucket on AP/AR DTOs; GET /api/accounts-payable/aging and …/accounts-receivable/aging with bucket summary.
- Document→exposure: create with inancialDocumentId + POST /api/{payable|receivable}-exposures/{id}/link-document; direction mismatch rejected; never invents Cost/Revenue.
- Outstanding: draft payment allocation unchanged; finalize reduces outstanding (AC-007 / C-015) — covered in FULL aging/settlement test.
- VI terms: AGING, AGING_BUCKET, DAYS_PAST_DUE, LINK_DOCUMENT, RECOGNIZED_AMOUNT.
- Tests: Sprint7FullExposureApArTests (3); suite **74 passed**.
- DoD: docs/sprint/SPRINT-7-FULL-DOD.md; prompt: PROMPT-SPRINT-7-FULL.md; ADR-0006.

### Files / API
- APIs: link-document; /api/accounts-payable/aging; /api/accounts-receivable/aging; exposure GET + recognitions; AP/AR aging fields
- Application: Exposures/AgingBuckets; link commands; recognize recompute; aging queries
- ADR: docs/adr/ADR-0006-ap-ar-aging-buckets.md
- No new EF migration (derived fields + existing inancial_document_id)

### Verify
- dotnet test Cms.sln -c Release → **74 passed**

### Deferred / Next
- Pass 2 Sprint 8 FULL Settlement
- Next.js aging UI (Pass UI U4+)

---

## 2026-09-12 — Sprint 6 FULL Financial Documents (Pass 2)

### User
Pass 2 Sprint 6 FULL: match methods line_to_line/line_to_cost/line_to_revenue (link-only C-003/C-004); tolerance policy; accept-before-match; duplicate control; reverse/cancel match detail; tests + SPRINT-6-FULL-DOD + PR. Never secrets/alogex. Vietnamese errors.

### Done
- Match methods enforce target shape; Cost/Revenue links never invent economic rows.
- Tolerance: Documents:DefaultToleranceAbsolute / DefaultTolerancePercent (+ per-match override); C-007 beyond effective tolerance → 409 VI.
- Accept-before-match gate (RequireAcceptBeforeMatch default true); soft cancel/void document + cancel match session; reverse match detail restores open amounts (RV-003).
- Duplicate control on active (type, document_no, counterparty) — IDX-006 / BR-FIN-023.
- GET /api/financial-documents/open-amounts; reverse/cancel APIs.
- Migration Sprint6Full_FinancialDocuments; ADR-0005.
- Tests: Sprint6FullFinancialDocumentTests (3); Pass 1 Sprint6 updated for methods/accept; suite **74 passed**.
- DoD: docs/sprint/SPRINT-6-FULL-DOD.md.

### Files / API / Config
- APIs: /api/document-matches (methods+tolerance), .../details/{id}/reverse, .../cancel; /api/financial-documents/open-amounts, .../{id}/cancel
- Application: DocumentOptions, match/receive/cancel/reverse commands; queries open amounts
- Config: Documents section in ppsettings.json
- Migration: 20260912020613_Sprint6Full_FinancialDocuments
- ADR: docs/adr/ADR-0005-document-match-tolerance-accept.md

### Verify
- dotnet test Cms.sln -c Release → **74 passed**

### Deferred / Next
- Pass 2 Sprint 7 FULL Exposure/AP/AR
- Auto-match engine; e-invoice; Next.js Documents UI

---

## 2026-09-11 — Sprint 5 FULL Revenue & Profitability (Pass 2)


### User
Pass 2 Sprint 5 FULL: Revenue FX stub + optional confirm approval threshold (parity Cost FULL); profitability view API + profile enhancements; C-004 hardened; tests green; SPRINT-5-FULL-DOD + PR. Never secrets/alogex. Vietnamese errors.

### Done
- FX stub: `Revenue:BaseCurrency` + `StubFxRatesToBase` fills `base_amount` on create/confirm/actualize/adjust (ADR-0004); `fx_rate_id` null until real FX table; missing rate → VI validation.
- Optional confirm gate: `Revenue:ConfirmApprovalThresholdBase` → pending + VI 409 until Sprint 9 `/api/approvals` approve (`objectType=revenue`).
- Profile: Expected vs Actual variance fields; allocated cost included in CostBestAvailable; multi-currency never summed raw.
- `GET /api/bills/{id}/profitability?view=expected|confirmed|actual|best` (default best); confirmed/actual layers missing → 0 for honesty.
- C-004 harden: reject `document` / `accounts_receivable` / aliases (`ar`, `doc`, `financial_document`); defense in depth in handler.
- GET revenue exposes `baseAmount`, `fxRateId`.
- VI terms: REVENUE_CONFIRM_APPROVAL_THRESHOLD, BILL_PROFITABILITY, PROFITABILITY_VIEW, VARIANCE_EXPECTED_VS_ACTUAL.
- Tests: `Sprint5FullRevenueProfitabilityTests` (3); suite **71 passed**.
- DoD: `docs/sprint/SPRINT-5-FULL-DOD.md`; prompt: `PROMPT-SPRINT-5-FULL.md`.

### Files / API / Config
- APIs: `/api/revenues`, `/api/revenues/{id}/confirm`, `/api/bills/{id}/financial-profile`, `/api/bills/{id}/profitability`
- Application: `Revenues/RevenueOptions`, `RevenueFxStub`, `RevenueApprovalGate`; maturity/create/adjust; `GetBillProfitabilityQuery`
- Config: `Revenue` section in `appsettings.json`
- ADR: `docs/adr/ADR-0004-cost-fx-stub-approval-threshold.md` (Cost+Revenue)
- No new EF migration (schema already had base_amount / fx_rate_id)

### Verify
- `dotnet test Cms.sln -c Release` → **71 passed**
### Deferred / Next
- Pass 2 Sprint 6 FULL Documents
- Real fx_rates table; per-tenant threshold; Next.js Revenue UI

---

## 2026-09-11 — Sprint 4 FULL Cost (Pass 2)

### User
Pass 2 Sprint 4 FULL: allocation bases equal/quantity/manual_ratio; C-005/C-006; reallocation supersedes history; Shared vs Direct rules; FX stub base_amount; optional approval threshold before confirm; tests + SPRINT-4-FULL-DOD + PR. Never secrets/alogex. Vietnamese errors.

### Done
- Allocation bases locked: `equal` | `quantity` | `manual_ratio` (C-006); conservation on finalize (C-005); reallocation marks prior finalized as `superseded` + `supersedes_allocation_id`.
- Shared vs Direct harden: Direct requires Bill; Shared Bill null; allocate Shared only.
- FX stub: `Cost:BaseCurrency` + `StubFxRatesToBase` fills `base_amount` on create/confirm/actualize/adjust/seed (ADR-0004); `fx_rate_id` null until real FX table.
- Optional confirm gate: `Cost:ConfirmApprovalThresholdBase` → pending + VI 409 until Sprint 9 approve.
- GET cost exposes `baseAmount`, `fxRateId`, allocation `supersedesAllocationId`.
- VI terms: ALLOCATION_BASIS_*, BASE_AMOUNT, FX_STUB_RATE, COST_CONFIRM_APPROVAL_THRESHOLD.
- Tests: `Sprint4FullCostTests` (3); suite **63 passed**.
- DoD: `docs/sprint/SPRINT-4-FULL-DOD.md`; prompt: `PROMPT-SPRINT-4-FULL.md`.

### Files / API / Config
- APIs: `/api/costs`, `/api/costs/{id}/confirm`, `/api/costs/{id}/allocations`, `/api/cost-allocations/{id}/finalize`
- Application: `Costs/CostOptions`, `CostFxStub`, `CostApprovalGate`; allocation/maturity/create/adjust/seed commands
- Config: `Cost` section in `appsettings.json`
- ADR: `docs/adr/ADR-0004-cost-fx-stub-approval-threshold.md`
- No new EF migration (schema already had base_amount / fx_rate_id / supersedes)

### Verify
- `dotnet test Cms.sln -c Release` → **63 passed**

### Deferred / Next
- Pass 2 Sprint 3 FULL (Rate) if not yet merged; Sprint 5 FULL Revenue
- Real fx_rates table; per-tenant threshold; Next.js Cost UI

---

## 2026-09-11 — Sprint 2 FULL Operational Reference (Pass 2)

### User
Pass 2 Sprint 2 FULL: transport_legs / transport_movements + bill_leg_links / leg_movement_links / bill_movement_links + migration; upsert APIs; expand bill graph; operational search within tenant; C-002 idempotency; JWT/data scope on lists; tests + SPRINT-2-FULL-DOD + handoff + PR. Never secrets/alogex. Vietnamese errors.

### Done
- Domain D03 depth: `TransportLeg` (Shipment 1:N), `TransportMovement`, bridges `BillLegLink`, `LegMovementLink`, `BillMovementLink`.
- Migration `Sprint2Full_TransportLegsMovements` (C-002 unique on leg/movement external identity).
- CQRS/API: upsert leg/movement; link bill↔leg, leg↔movement, bill↔movement (idempotent).
- `GET /api/bills/{id}/graph` now returns Legs + Movements (direct bridges ∪ shipment legs ∪ movements via legs).
- Search: `GET /api/search/operational?q=` + `GET /api/bills?q=` (bill_no / bill external_id / order external_id); respects `bill.read` Data Scope.
- VI validation/errors; terminology keys for operational graph.
- Tests: `Sprint2FullOperationalReferenceTests` (idempotency+graph; search isolation; JWT data-scope; cross-tenant 404).
- DoD: `docs/sprint/SPRINT-2-FULL-DOD.md`.

### Files / API
- APIs: `/api/transport-legs`, `/api/transport-movements`, `/api/search/operational`, expanded `/api/bills/{id}/graph`, `/api/bills?q=`
- Endpoints: `OperationalReferenceEndpoints`, `TenantBillEndpoints`
- Migration: `20260911203703_Sprint2Full_TransportLegsMovements`

### Verify
- `dotnet test Cms.sln -c Release` → **60 passed** (4 new Sprint 2 FULL; Sprint 1 FULL was 56)

### Deferred / Next
- Pass 2 Sprint 3 FULL (Rate & Pricing depth)
- Full-text search index; Order/Shipment list Data Scope; Next.js UI

---

## 2026-09-11 — Sprint 1 FULL Identity + Master Data (Pass 2)

### User
Pass 2 Sprint 1 FULL: Permission × Data Scope independent (`all`/`organization`/`own`) on Bill+Cost list/get; JWT `sub` actor; inactive users denied; party_roles + APIs; org tree/children; currency harden; tests + SPRINT-1-FULL-DOD + handoff + PR. Base after Sprint 0 FULL JWT. Never secrets/alogex. Vietnamese errors.

### Done
- Data Scope matrix on `role_permissions` (`all` > `organization` > `own`); enforced on Bill/Cost list+get (`bill.read` / `cost.read`).
- `organization_id` on users/bills/costs; org subtree for organization scope; `CreatedBy` stamped from JWT `sub` / Dev header.
- Inactive registered users → VI 403 «Tài khoản không còn hiệu lực.»
- `party_roles` + assign/list/revoke APIs; org `tree` + `children`; currency seed VND/USD/EUR + get-by-code + inactive reject + baseline decimals.
- `POST /api/roles/{id}/permissions` with dataScope; `PUT /api/users/{id}`.
- Migration `Sprint1Full_IdentityMasterData`; ADR-0003; `PROMPT-SPRINT-1-FULL.md` + `SPRINT-1-FULL-DOD.md`.
- Tests: `Sprint1FullIdentityMasterTests` (6) — suite **56 passed**.

### Files / API
- APIs: `GET /api/bills`; role permissions; user update; org tree/children; party roles; `GET /api/currencies/{code}`
- Identity: `PermissionService`, `OrganizationHierarchyService`, `DataScopeAccess`
- Migration: `20260911202508_Sprint1Full_IdentityMasterData`
- ADR: `docs/adr/ADR-0003-permission-data-scope.md`

### Verify
- `dotnet test Cms.sln -c Release` → **56 passed**

### Deferred / Next
- Data Scope on Revenue/Documents/AP-AR; OIDC; multi-org membership; Pass 2 Sprint 2 FULL

---

## 2026-09-11 — Sprint 0 FULL Foundation (Pass 2)

### User
Pass 2 Sprint 0 FULL: JWT Bearer (`tenant_id` + `sub`); Production disables header bootstrap; Dev `POST /api/dev/token` + optional `X-Tenant-Id`; observability enrichment + Prometheus `/metrics`; tests JWT + Production 401; SPRINT-0-FULL-DOD + handoff + PR. Never secrets/alogex. Vietnamese errors remain.

### Done
- JWT Bearer auth (`Auth` options + ADR-0002). Claims `tenant_id` + `sub` drive `ITenantContext` / `ICurrentUserContext`.
- Production defaults: `RequireJwt=true`, `AllowHeaderBootstrap=false`. Dev: headers allowed; `POST /api/dev/token`.
- Serilog LogContext + request log enrich TenantId/UserId/CorrelationId.
- `/metrics` = Prometheus text (`prometheus-net` HTTP metrics); anonymous.
- Vietnamese JWT 401 challenge JSON (`unauthorized`).
- Rate-limit remains in-process fixed window (Sprint 12) — single-node only; Redis deferred (documented).
- Tests: `Sprint0FullJwtAuthTests` (Dev JWT path, Production 401, Production JWT + header ignored).
- DoD: `docs/sprint/SPRINT-0-FULL-DOD.md`.

### Ops before Production deploy
- Set `Auth__Jwt__SigningKey` (≥32 chars) in host `infra/.env` (compose requires it). Never commit the real value.
- Dev token helper: `docs/ops/dev-jwt-token.md`.

### Files / API
- Auth: `src/LCMS.Api/Auth/*`, `TenantResolutionMiddleware`, `Program.cs`
- Dev: `POST /api/dev/token`
- Config: `appsettings*.json`, `infra/.env.example`, `infra/docker-compose.host.yml`
- ADR: `docs/adr/ADR-0002-jwt-bearer-tenant-claims.md`

### Verify
- `dotnet test Cms.sln -c Release` → **50 passed** (4 new Sprint 0 FULL; Sprint 12 was 46)

### Deferred / Next
- OIDC IdP; refresh/revocation; OTel exporter; Redis rate-limit; Pass 2 Sprint 1+ domain depth

---

## 2026-09-11 — Sprint 12 Hardening & UAT (E14/E15/E16) — **Pass 1 COMPLETE**

### User
Implement Sprint 12 Pass 1 final slice only: audit_events + writes on key money mutations + GET /api/audit-events; integration_records C-002 upsert stub; rate-limit + security headers smoke; Vietnamese terminology coverage + DoD declaring Pass 1 COMPLETE; tests green; handoff + PR to main. Non-goals: full outbox, load tests, JWT, Next.js UAT. Never secrets/alogex.


### Done
- Domain D12: `AuditEvent`, `IntegrationRecord`, `IntegrationError` (UUIDv7, soft-delete, tenant_id).
- Migration `Sprint12_HardeningAudit` (IDX-012/013).
- Audit writer on: cost create/confirm, revenue create, payment_allocation finalize, financial_close_snapshot create (actor/action/object/correlation).
- APIs: `GET /api/audit-events`; `POST|GET /api/integration-records` (duplicate → 409 C-002).
- Middleware: `SecurityHeadersMiddleware`, `RateLimitingMiddleware` (fixed window `/api/*`, config-driven); `/health` `/ready` + correlation still OK.
- VI terms extended (AUDIT_EVENT, INTEGRATION_RECORD, CORRELATION_ID, RATE_LIMIT, …); coverage test vs Sprints 1–11 keys.
- Tests: **46 passed** (3 new Sprint 12).
- DoD: `docs/sprint/SPRINT-12-DOD.md` declares **Pass 1 COMPLETE** + Pass 2 backlog pointers.
- Board: `docs/sprint/README-AGENTS.md` Sprint 0–12 all Done.

### Files / API
- APIs: `/api/audit-events`, `/api/integration-records`
- Endpoints: `AuditIntegrationEndpoints`
- Middleware: `SecurityHeadersMiddleware`, `RateLimitingMiddleware`
- Migration: `Sprint12_HardeningAudit`
- Config: `RateLimiting` in `appsettings.json`

### Verify
- `dotnet test Cms.sln -c Release` → 46 passed

### Deferred / Next (Pass 2)
- Full outbox/retry; load/soak; JWT/OIDC; Next.js UAT; AC-001… matrix hardening
- See Pass 2 pointers in `SPRINT-12-DOD.md`


---

## 2026-09-11 — Sprint 11 Financial Profile & Reporting (E13)

### User
Implement Sprint 11 Pass 1 only: enhance bill financial-profile read model (derived); GET /api/dashboard/summary + control queues exceptions/approvals; tenant isolation tests; SPRINT-11-DOD + handoff + PR to main. All tests green. No SoT derived totals on Bill. Non-goals: Next.js UI, Sprint 12 NFR, JWT. Never secrets/alogex.

### Done
- Enhanced `GET /api/bills/{id}/financial-profile`: maturity breakdown (Expected/Confirmed/Actual), allocated cost, AP/AR settlement outstanding by currency, `asOfTimestamp`, optional `?asOf=` filter with limitation note.
- `GET /api/dashboard/summary`: bill / open-exception / pending-approval / open-close counts + Best Available cost/revenue/profit by currency.
- Control queues: `GET /api/queues/exceptions` (open), `GET /api/queues/approvals` (pending).
- Derived read-only — Bill entity unchanged (no SoT totals).
- VI terminology keys for dashboard/queues; notes use `VietnameseUiTerms`.
- Tests: 43 passed (3 new Sprint 11 — profile maturity/allocated/settlement/asOf; dashboard isolation+counts; queues status filter).
- DoD: `docs/sprint/SPRINT-11-DOD.md`.

### Files / API
- APIs: `/api/bills/{id}/financial-profile`, `/api/dashboard/summary`, `/api/queues/exceptions`, `/api/queues/approvals`
- Endpoints: `DashboardReportingEndpoints`; profile query enhanced in `GetBillFinancialProfileQuery`
- Application: `Dashboard/Queries`, `Queues/Queries`

### Verify
- `dotnet test Cms.sln -c Release` → 43 passed

### Deferred / Next
- Full asOf maturity-history reconstruction (Pass 2)
- Sprint 12 hardening / NFR
- FX base roll-up; Next.js UI; JWT/OIDC; snapshot-based P&L

---

## 2026-09-11 — Sprint 10 Financial Close (E12)

### User
Implement Sprint 10 Pass 1 only: D11 financial_closes / financial_close_snapshots / financial_close_snapshot_details; C-010/AC-008 immutable snapshots; reopen/reclose new versions; tenant APIs start/snapshot/reopen/list + VI; isolation + immutability tests; DoD + PR to main. Non-goals: reporting UI (Sprint 11), hardening (Sprint 12), JWT, Next.js.

### Done
- Domain: `FinancialClose`, `FinancialCloseSnapshot`, `FinancialCloseSnapshotDetail`.
- Migration `Sprint10_FinancialClose` (three tables).
- CQRS + API: start close; snapshot+lock (metrics hash); reopen; list/get snapshots; optional supersede reclose.
- C-010/AC-008: snapshots insert-only (DbContext rejects Modified/Deleted); reopen keeps history; re-snapshot creates new `SnapshotVersion`; reclose creates new `VersionNo`.
- Eligibility stub: block snapshot when open/in_progress critical exception in scope.
- VI validation/errors; tenant filter + `X-Tenant-Id` / `X-User-Id`.
- Tests: 40 passed (3 new Sprint 10 — immutable+reopen history; eligibility; cross-tenant+reclose).
- DoD: `docs/sprint/SPRINT-10-DOD.md`.

### Files / API
- APIs: `/api/financial-closes`, `/api/financial-close-snapshots`
- Endpoints: `FinancialCloseEndpoints`
- Migration: `20260911193741_Sprint10_FinancialClose`

### Verify
- `dotnet test Cms.sln -c Release` → 40 passed

### Deferred / Next
- Reporting dashboard (Sprint 11)
- Hardening / UAT (Sprint 12)
- Full eligibility matrix; period lock on live ledger; JWT/OIDC; Next.js UI

---

## 2026-09-11 — Sprint 9 Financial Control (E11)

### User
Implement Sprint 9 Pass 1 only: D10 reconciliations/reconciliation_details/variances/exceptions/approvals; Variance ≠ Exception; Approval independent of Permission; tenant APIs reconcile/exception/approval + VI; isolation tests; DoD + PR to main. Non-goals: financial close (Sprint 10), JWT, Next.js.

### Done
- Domain: `Reconciliation`, `ReconciliationDetail`, `Variance`, `FinancialException` (table `exceptions`), `Approval`.
- Migration `Sprint9_FinancialControl` (five tables; IDX-011 on exceptions).
- CQRS + API: start reconciliation; add details (auto Variance when delta ≠ 0, never auto Exception); open/resolve/close exception; request/approve/reject approval on financial object ref.
- Variance ≠ Exception: control fact vs severity/owner/SLA work item; optional link only when escalated.
- Permission ≠ Approval: approval never calls `IPermissionService`; does not mutate permissions; works with role that has zero RolePermissions.
- Cost/Revenue `ApprovalStatus` updated by approval workflow (pending/approved/rejected) without RBAC.
- VI validation/errors; tenant filter + `X-Tenant-Id` / `X-User-Id`.
- Tests: 37 passed (3 new Sprint 9 — variance≠exception; approval≠permission; cross-tenant).
- DoD: `docs/sprint/SPRINT-9-DOD.md`.

### Files / API
- APIs: `/api/reconciliations`, `/api/variances`, `/api/exceptions`, `/api/approvals`
- Endpoints: `FinancialControlEndpoints`
- Migration: `20260911192820_Sprint9_FinancialControl`

### Verify
- `dotnet test Cms.sln -c Release` → 37 passed

### Deferred / Next
- Financial close snapshots (Sprint 10)
- Auto-escalate variance→exception; multi-step approval; bank feed; JWT/OIDC; Next.js UI

---

## 2026-09-11 — Sprint 8 Settlement (E10)

### User
Implement Sprint 8 Pass 1 only: D09 payments/collections/payment_allocations/collection_allocations; outstanding changes only via finalized allocation (AC-007/C-008); partial settle; reject over-allocation; reversal without hard delete; tenant APIs + VI; no Cost/Revenue from settlement (C-003/C-004); tests; DoD + PR to main. Non-goals: reconciliation (Sprint 9), close (Sprint 10), JWT, Next.js.

### Done
- Domain: `Payment`, `Collection`, `PaymentAllocation`, `CollectionAllocation`.
- Migration `Sprint8_Settlement` (four tables; IDX-009/010).
- CQRS + API: create/list/get payment & collection; draft allocate; finalize; reverse.
- AC-007: draft allocation does not change AP/AR outstanding; finalize updates `FinalizedSettledAmount` and derived Outstanding/SettlementStatus.
- C-008: over-allocation rejected (policy stub = 0) vs transaction amount and AP/AR ceiling.
- Partial settlement + unapplied/available-to-allocate on GET.
- Reversal: status → `reversed`; restores outstanding; no hard delete / silent overwrite (C-013).
- C-003/C-004: settlement never invents Cost/Revenue.
- VI validation/errors; tenant filter + `X-Tenant-Id`.
- Tests: 34 passed (3 new Sprint 8 — partial+finalize AC-007; over-allocate+reversal; collection+cross-tenant).
- DoD: `docs/sprint/SPRINT-8-DOD.md`.

### Files / API
- APIs: `/api/payments`, `/api/payment-allocations`, `/api/collections`, `/api/collection-allocations`
- Endpoints: `SettlementEndpoints`
- Migration: `20260911191645_Sprint8_Settlement`

### Verify
- `dotnet test Cms.sln -c Release` → 34 passed

### Deferred / Next
- Reconciliation / exceptions (Sprint 9)
- Financial close (Sprint 10)
- Bank feed; over-settlement policy; JWT/OIDC; Next.js UI

---

## 2026-09-11 — Sprint 7 Exposure + AP/AR (E09)

### User
Implement Sprint 7 Pass 1 only: D08 payable/receivable exposures + accounts_payable/receivable; Exposure ≠ Recognized AP/AR; outstanding derived (C-015); partial recognition; tenant APIs + VI; no Cost/Revenue on recognize (C-003/C-004); tests; DoD + PR to main. Non-goals: settlement (Sprint 8), JWT, Next.js.

### Done
- Domain: `PayableExposure`, `ReceivableExposure`, `AccountsPayable`, `AccountsReceivable`.
- Migration `Sprint7_ExposureApAr` (four tables; IDX-007/008).
- CQRS + API: create/list/get exposures; recognize → AP/AR; list/get AP/AR with derived Outstanding; adjust AP/AR.
- CP3/TD4: recognition creates separate AP/AR row — never merges into exposure status.
- C-015: Outstanding = recognized + adjustment − finalized_settled (settled=0 Pass 1); no user-entered SoT.
- C-003/C-004: recognize does not invent Cost/Revenue.
- VI validation/errors; tenant filter + `X-Tenant-Id`.
- Tests: 31 passed (3 new Sprint 7 — exposure≠AP + no invent; outstanding derived + partial; AR + cross-tenant).
- DoD: `docs/sprint/SPRINT-7-DOD.md`.

### Files / API
- APIs: `/api/payable-exposures`, `/api/receivable-exposures`, `/api/accounts-payable`, `/api/accounts-receivable`
- Endpoints: `ExposureApArEndpoints`
- Migration: `20260911190832_Sprint7_ExposureApAr`

### Verify
- `dotnet test Cms.sln -c Release` → 31 passed

### Deferred / Next
- Settlement payments/collections (Sprint 8)
- Dedicated AP/AR adjustment ledger; aging UI; JWT/OIDC; Next.js UI

---

## 2026-09-11 — Sprint 6 Financial Documents (E08)

### User
Implement Sprint 6 Pass 1 only: D07 financial_documents / lines / document_matches / match_details; Received ≠ Accepted ≠ Matched; tenant APIs receive/accept/lines/match; C-007 no over-match; C-003/C-004 no Cost/Revenue from documents; tests; DoD + PR to main. Non-goals: AP/AR, settlement, JWT, Next.js.

### Done
- Domain: `FinancialDocument` (three independent status dims), `FinancialDocumentLine`, `DocumentMatch`, `DocumentMatchDetail`.
- Migration `Sprint6_FinancialDocuments` (four tables; IDX-006).
- CQRS + API: receive, list/get, accept, add lines, start match, add match details (line↔line or line↔cost/revenue stub).
- AC-005: ReceiptStatus / AcceptanceStatus / MatchingStatus never collapsed to one enum.
- C-007: over-match rejected (tolerance stub = 0). C-003/C-004: receive/match do not invent Cost/Revenue.
- VI validation/errors; tenant filter + `X-Tenant-Id`.
- Tests: 28 passed (3 new Sprint 6 — state separation+no invent, over-match C-007, cross-tenant).
- DoD: `docs/sprint/SPRINT-6-DOD.md`.

### Files / API
- APIs: `/api/financial-documents`, `/api/document-matches`
- Endpoints: `FinancialDocumentEndpoints`
- Migration: `20260911185745_Sprint6_FinancialDocuments`

### Verify
- `dotnet test Cms.sln -c Release` → 28 passed

### Deferred / Next
- AP/AR recognition (Sprint 7)
- Settlement; auto-match; JWT/OIDC; Next.js UI

---

## 2026-09-12 — Sprint 5 Revenue & Profitability (E07)

### User
Implement Sprint 5 only: full revenues TD1; revenue_adjustments; maturity no-overwrite (C-009); Single Economic Revenue (C-004); APIs create/list/get/confirm/actualize/adjust; `GET /api/bills/{id}/financial-profile` derived read model; tests; DoD + PR to main. Non-goals: Documents/AP/AR, settlement, JWT, Next.js.

### Done
- Domain: expanded `Revenue` (layer amounts + customer/source/audit); `RevenueAdjustment`.
- Migration `Sprint5_Revenue` (alters `revenues`; creates `revenue_adjustments`).
- CQRS + API: create/list/get revenue; confirm/actualize; adjust; Bill financial profile.
- C-004/C-009: Single Economic Revenue; no silent maturity overwrite; document/AR source rejected.
- Financial profile: Best Available (Actual→Confirmed→Expected); profit = rev − cost per currency; no SoT totals on Bill (TD1-DB-003/004).
- VI validation/errors; tenant filter + `X-Tenant-Id`.
- Tests: 25 passed (3 new — maturity+adjust, profile/currency, isolation+idempotent source).
- DoD: `docs/sprint/SPRINT-5-DOD.md`.

### Files / API
- APIs: `/api/revenues`, `/api/bills/{id}/financial-profile`
- Endpoints: `RevenueEndpoints`
- Migration: `20260911185027_Sprint5_Revenue`

### Verify
- `dotnet test Cms.sln -c Release` → 25 passed

### Deferred / Next
- Documents / AP / AR (Sprint 6–7)
- Settlement; full approval; JWT/OIDC; Next.js UI

---

## 2026-09-12 — Sprint 4 Cost (E05/E06)


### User
Implement Sprint 4 only: full costs TD1 fields; cost_adjustments; allocations+finalize conservation; maturity no-overwrite; seed Expected from ratings; tenant APIs; tests; DoD + PR to main.

### Done
- Domain: expanded `Cost` (layer amounts + attribution/maturity/source/audit); `CostAdjustment`; `CostAllocation`; `CostAllocationDetail`.
- Migration `Sprint4_Cost` (alters `costs`; creates adjustment/allocation tables).
- CQRS + API: create/list/get cost; confirm/actualize; adjust; allocate/finalize; seed from rating.
- C-003/C-005/C-006/C-009: Single Economic Cost; conservation; basis gate; no silent maturity overwrite.
- VI validation/errors; tenant filter + `X-Tenant-Id`.
- Tests: 22 passed (3 new — maturity, allocation conservation, isolation+idempotent seed).
- DoD: `docs/sprint/SPRINT-4-DOD.md`.

### Files / API
- APIs: `/api/costs`, `/api/cost-allocations/{id}/finalize`, `/api/ratings/{id}/seed-expected-costs`
- Endpoints: `CostEndpoints`
- Migration: `20260911184233_Sprint4_Cost`

### Verify
- `dotnet test Cms.sln -c Release` → 22 passed

### Deferred / Next
- Revenue lifecycle (Sprint 5)
- Documents/AP/AR; full approval; JWT/OIDC; Next.js UI

---

## 2026-09-12 — Sprint 3 Rate & Pricing (E04)

### User
Implement Sprint 3 only: D04 rate cards/versions/rules/components + ratings snapshot; published version immutable (C-011); tenant APIs; tests; DoD + PR to main.

### Done
- Domain: `RateCard`, `RateVersion`, `PricingRule`, `PricingRuleComponent`, `Rating`, `RatingDetail` (UUIDv7, tenant_id, soft-delete, row_version).
- Migration `Sprint3_RatePricing` (does not alter Sprint 0–2).
- CQRS + API: rate card CRUD; draft→publish versions; rules/components on draft only; `POST /api/ratings` Expected seed snapshot (`fixed` / `unit_rate × qty`).
- C-011: published version rejects rule/component mutations; new version instead of overwrite; re-rating appends history.
- VI validation/errors; tenant filter + `X-Tenant-Id`.
- Tests: 19 passed (3 new — immutable publish, tenant isolation, rating snapshot).
- DoD: `docs/sprint/SPRINT-3-DOD.md`.

### Files / API
- APIs: `/api/rate-cards`, `/api/rate-versions`, `/api/pricing-rules/{id}/components`, `/api/ratings`
- Endpoints: `RatePricingEndpoints`
- Migration: `20260911183223_Sprint3_RatePricing`

### Verify
- `dotnet test Cms.sln -c Release` → 19 passed

### Deferred / Next
- Cost Expected lifecycle from rating (Sprint 4)
- Full formula engine (deferred)
- JWT/OIDC; Next.js UI

---

## 2026-09-12 — Wire CMS_DEPLOY_SSH_KEY + green deploy

### User
Làm luôn: gắn secret `CMS_DEPLOY_SSH_KEY` từ private key operator.

### Done
- GitHub Actions secret `CMS_DEPLOY_SSH_KEY` = operator `~/.ssh/id_ed25519_a1` (không commit key).
- Re-run failed `deploy` trên run `34632386398` → **success**.

### Verify
- Actions: https://github.com/thanhquyen129/CMS/actions/runs/34632386398 (`test` + `deploy` green)
- http://194.233.89.26/health · `/ready` OK

### Next
- Cost Expected on Bill (Sprint 4); JWT/OIDC thay header bootstrap

---

## 2026-09-12 — Sprint 2 Operational Reference (E03/E14)

### User
Implement Sprint 2: orders/shipments + N:N links to Bill; idempotent order upsert; bill graph; isolation/idempotency/graph tests; DoD + ship via PR.

### Done
- Domain: `Order`, `Shipment`, `OrderBillLink`, `BillShipmentLink` (UUIDv7, tenant_id, soft-delete, row_version).
- Migration `Sprint2_OperationalReference` (`orders`, `shipments`, `order_bill_links`, `bill_shipment_links`).
- CQRS + API: order upsert/list/get; shipment upsert; link Order↔Bill / Bill↔Shipment; `GET /api/bills/{id}/graph`.
- Idempotent upsert by `(tenant_id, source_system, external_id)`; VI validation; tenant filter + `X-Tenant-Id`.
- Tests: 16 passed (4 new Sprint 2 — isolation, idempotency, graph, graph cross-tenant).
- DoD: `docs/sprint/SPRINT-2-DOD.md`.

### Files / API
- APIs: `/api/orders`, `/api/shipments`, `/api/orders/{orderId}/bills/{billId}`, `/api/bills/{billId}/shipments/{shipmentId}`, `/api/bills/{id}/graph`
- Migration: `20260911182340_Sprint2_OperationalReference`
- Endpoints: `OperationalReferenceEndpoints`

### Verify
- `dotnet test Cms.sln -c Release` → 16 passed

### Deferred / Next
- transport_legs / movements (deferred)
- Cost Expected on Bill (Phase 2)
- JWT/OIDC replace header bootstrap

---

## 2026-09-12 — Sprint 1 Identity + Master Data (E01/E02)


### User
Implement Sprint 1: Tenant/user/access; Organization/BusinessParty/Currency; permission skeleton; isolation tests; ship.

### Done
- Domain: `Role`, `Permission`, `RolePermission`, `UserRole`, `Currency`; User polished.
- Migration `Sprint1_IdentityMaster` (roles, permissions, role_permissions, user_roles, currencies).
- CQRS + API: users, roles (+ assign), organizations CRUD, business-parties CRUD, currencies list/upsert.
- `CreateTenant` seeds Admin + core Action permissions; `IPermissionService` gates Bill create.
- Bootstrap: `X-Tenant-Id` + optional `X-User-Id` (JWT deferred). Soft allow when no user header.
- Tests: 12 passed (cross-tenant User/Org/Party; permission 403 VI + correlation id).
- DoD: `docs/sprint/SPRINT-1-DOD.md`.

### Files / API
- APIs: `/api/users`, `/api/roles`, `/api/users/{id}/roles/{roleId}`, `/api/organizations`, `/api/business-parties`, `/api/currencies`
- Identity: `PermissionService`, `TenantAccessSeeder`, `PermissionCodes`
- Migration: `20260911181636_Sprint1_IdentityMaster`

### Verify
- `dotnet test Cms.sln -c Release` → 12 passed
- Actions: https://github.com/thanhquyen129/CMS/actions
- Health: http://194.233.89.26/health (post-deploy)

### Next
- JWT/OIDC replace header bootstrap
- Cost Expected on Bill
- Full Data Scope matrix / Approval (deferred)

---

## 2026-09-12 — Fix CI Health smoke race (exit 7)

### User
Screenshot: Actions CI failed on `test` (exit 7); `deploy` skipped.

### Cause
`curl` hit `/health` ~1s before `dotnet run` finished migrate+listen (`curl: (7) Failed to connect`).

### Done
- CI Health smoke: poll `/health` up to 60s, `--no-build`, dump smoke log on failure; `ASPNETCORE_ENVIRONMENT=Production`.

### Files
- `.github/workflows/ci.yml`

### Verify
- Re-run after push: https://github.com/thanhquyen129/CMS/actions

### Next
- Confirm `CMS_DEPLOY_SSH_KEY` so `deploy` can run after green `test`

---

## 2026-09-12 — Close Sprint 0 (TD6 Foundation)

### User
Close Sprint 0: observability skeleton (E14), terminology contract (E16), real Actions deploy (E15), env example, DoD checklist; ship.

### Done
- Serilog JSON console + `CorrelationIdMiddleware` + `RequestLoggingMiddleware`; `/metrics` process placeholder.
- `VietnameseUiTerms` (CP6.5) + `GET /api/terminology`.
- CI `deploy` job: SSH rsync → `/opt/cms`, compose up (excludes `infra/.env`); docs for `CMS_DEPLOY_SSH_KEY`.
- `infra/.env.example`, `docs/sprint/SPRINT-0-DOD.md`.
- Tests: 6 passed (terminology + correlation ID coverage).

### Files / API
- Middleware: `CorrelationIdMiddleware`, `RequestLoggingMiddleware`
- Domain: `src/LCMS.Domain/Terminology/VietnameseUiTerms.cs`
- Endpoints: `/api/terminology`, `/metrics`
- Ops: `.github/workflows/ci.yml`, `docs/ops/github-actions.md`, `docs/ops/vps-bootstrap.md`

### Verify
- `dotnet test Cms.sln -c Release` → 6 passed
- Operator fallback deploy (tar/scp + compose) OK: `/health`, `/ready`, `/api/terminology`, `/metrics`, correlation header
- Actions: https://github.com/thanhquyen129/CMS/actions — require secret `CMS_DEPLOY_SSH_KEY` for automated deploy job

### Next
- Confirm GitHub secret `CMS_DEPLOY_SSH_KEY` so Actions `deploy` succeeds without operator fallback
- Sprint 1: User/Permission/JWT (replace `X-Tenant-Id`)
- Cost Expected on Bill

---

## 2026-09-12 — Bill thật trên DB (migration + API Tenant/Bill)

### User
Làm bước 1–2: EF migration InitialTd1 + API Tenant/Bill thật + test cô lập tenant.

### Done
- Migration `InitialTd1` (`src/LCMS.Infrastructure/Persistence/Migrations/`).
- Compose: `infra/docker-compose.dev.yml` (Postgres local), host compose thêm `db` + migrate on startup.
- CQRS: `CreateTenant` / `GetTenantById` / `CreateBill` / `GetBillById` → `ILcmsDbContext`.
- API: `POST/GET /api/tenants`, `POST/GET /api/bills` (Bill cần `X-Tenant-Id`).
- `/ready` kiểm tra kết nối DB; `Database:MigrateOnStartup`.
- Tests: `tests/LCMS.Api.Tests` — 4 passed (cross-tenant 404, thiếu tenant 401, duplicate 409).
- CI: Postgres service + `dotnet test` + health smoke với migrate.

### Verify
- `dotnet test Cms.sln -c Release` → 4 passed
- Local DB: `docker compose -f infra/docker-compose.dev.yml up -d` rồi chạy API
- Actions: https://github.com/thanhquyen129/CMS/actions

### Next
- Wire deploy job → `/opt/cms` (compose có Postgres)
- JWT claims thay `X-Tenant-Id`
- Cost Expected trên Bill (Phase 2)

---

## 2026-09-12 — TD1 Clean Architecture scaffold (Tenant + Bill)

### User
Khởi tạo Solution .NET Clean Architecture 4 project theo TD1; mẫu Entity Tenant/Bill + DbContext PostgreSQL; CQRS/MediatR; FluentValidation tiếng Việt; EF global tenant filter; exception middleware + Correlation ID.

### Done
- Solution `Cms.sln`: `LCMS.Domain`, `LCMS.Application`, `LCMS.Infrastructure`, `LCMS.Api` (.NET 8). Removed bootstrap `Cms.Api`.
- Domain: `Tenant`, `Bill` (TD1 baseline fields), stubs `Organization`/`User`/`BusinessParty`/`Cost`/`Revenue`; `UuidV7`, `row_version`, soft-delete (C-013), no hard delete.
- Infrastructure: `LcmsDbContext` + Fluent configs + snake_case + PostgreSQL; global query filter C-001 + soft-delete.
- Application: MediatR + FluentValidation (VI messages) + sample `CreateBillCommand`.
- API: DI, `ExceptionHandlingMiddleware` (JSON VI + correlationId), `TenantResolutionMiddleware` (`X-Tenant-Id`), `/health` `/ready`.
- ADR: `docs/adr/ADR-0001-td1-identity-tenancy-postgres.md`
- Docker/CI updated to `LCMS.Api`.

### Files / schema
- Tables mapped: `tenants`, `bills`, `organizations`, `users`, `business_parties`, `costs`, `revenues`
- Conn string: `ConnectionStrings:LcmsDb`
- Migration chưa tạo — follow-up: `dotnet ef migrations add InitialTd1`

### Verify
- `dotnet build Cms.sln -c Release` (local OK)
- After deploy: http://194.233.89.26/health , `/ready`
- Actions: https://github.com/thanhquyen129/CMS/actions

### Next
- EF migration + Postgres on host
- Wire CreateBill handler to DbContext
- Identity/JWT claims → replace header tenant bootstrap

---

## 2026-09-12 — Wipe VPS + deploy CMS bootstrap

### User
Wipe `194.233.89.26`; update all IPs in rules to `194.233.89.26`.

### Done
- Rules: removed other IPv4 (`217.216…`); CMS production IP consistently `194.233.89.26` (IPv6 NIC kept).
- VPS wipe: stopped/removed `a1-api`/`a1-postgres`, volume `infra_a1_pgdata`, deleted `/opt/sanlogistics`, created `/opt/cms`.
- Deployed CMS API bootstrap via Docker Compose on host; verified `/health` and `/ready`.

### Verify
- http://194.233.89.26/health
- http://194.233.89.26/ready
- Actions: https://github.com/thanhquyen129/CMS/actions

### Next
- Phase 1: Identity/Tenant/Master + Bill anchor
- Wire self-hosted runner / Actions deploy for `/opt/cms`

---

## 2026-09-12 — Kickoff CMS

### User
Kickoff: đặt tên Cost Management System (CMS); update rules từ alogex → CMS; đọc BA docs; lập kế hoạch; tạo repo GitHub CMS; clear + deploy lên VPS theo hailybato.

### Done
- Rules: renamed product to CMS; host `194.233.89.26` → `/opt/cms`; repo `thanhquyen129/CMS`; expert stance → financial control (Bill-centric).
- BA indexed; implementation plan written; GitHub private repo created.
- VPS wipe + deploy completed in follow-up turn after PO confirm.
