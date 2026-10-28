# LCMS Master Data Standardization — Fast-Track Handover

Ngày kiểm kê: 2026-10-28. Nhánh: `main`. Remote: `origin` (`https://github.com/thanhquyen129/CMS.git`).

## Commit

| Vai trò | SHA | Đã có trên `origin/main` |
|---|---|---|
| Code Backend, Frontend, test | `d2c7c1ce658c5cb0f8c6fd0c3cda4ccfc301a510` | Có, từ `09c44a7..af9a192` |
| Dòng SHA trong hồ sơ vòng 1 | `af9a1927ebbd6199e5a863582c1fe128512d129e` | Có |
| Hồ sơ bổ sung số liệu DB, ma trận 6 chiều, SQL rollback | Ghi ở cuối file này sau khi commit tài liệu | Tài liệu không đổi binary đang chạy |

`git show --name-status d2c7c1ce658c5cb0f8c6fd0c3cda4ccfc301a510`

### Backend

- `src/LCMS.Application/Catalog/TransportModeCatalog.cs` (thêm)
- `src/LCMS.Domain/Entities/ShipmentModePolicy.cs`
- `src/LCMS.Domain/Pricing/RateFieldPolicy.cs`
- `src/LCMS.Application/Bills/Commands/UpdateBillContextCommand.cs`
- `src/LCMS.Application/Bills/Queries/PrefillBillFromOrdersQuery.cs`
- `src/LCMS.Application/Orders/Commands/UpsertOrderCommand.cs`
- `src/LCMS.Application/Shipments/Commands/UpsertShipmentCommand.cs`
- `src/LCMS.Application/Shipments/Queries/ShipmentQueries.cs`
- `src/LCMS.Application/Shipments/Queries/PreviewShipmentRollupQuery.cs`
- `src/LCMS.Application/TransportLegs/Commands/UpsertTransportLegCommand.cs`
- `src/LCMS.Application/TransportLegs/Queries/TransportLegQueries.cs`
- `src/LCMS.Application/TransportMovements/Commands/UpsertTransportMovementCommand.cs`
- `src/LCMS.Application/OperationalReferences/Edit/EditOperationalReferenceCommand.cs`
- `src/LCMS.Application/OperationalReferences/Edit/GetOperationalReferenceEditQuery.cs`
- `src/LCMS.Application/OperationalReferences/Edit/ChargeableWeightPolicy.cs`
- `src/LCMS.Application/OperationalReferences/ImportOperationalBatchCommand.cs`
- `src/LCMS.Application/RateCards/Commands/RateCardCommands.cs`
- `src/LCMS.Application/RateCards/Commands/SaveRateCardDeclarationCommand.cs`
- `src/LCMS.Application/RateCards/Commands/ComposeTariffCommand.cs`
- `src/LCMS.Application/RateCards/Queries/RateCardQueries.cs`
- `src/LCMS.Application/PricingRules/Commands/PricingRuleCommands.cs`
- `src/LCMS.Application/Surcharges/SurchargeCommands.cs`
- `src/LCMS.Application/Surcharges/SurchargeRating.cs`
- `src/LCMS.Application/Ratings/Commands/CreateRatingCommand.cs`
- `src/LCMS.Application/Ratings/RatingEngine.cs`
- `src/LCMS.Application/Ratings/RatingReadiness.cs`
- `src/LCMS.Application/Ratings/RatingStalenessService.cs`
- `src/LCMS.Application/Ratings/BillRateSuggestion.cs`
- `src/LCMS.Application/ReferenceMasters/RouteCommands.cs`
- `src/LCMS.Application/Costs/Queries/GetSharedCostAllocationContextQuery.cs`
- `src/LCMS.Application/Demo/DemoDataSeeder.cs`

### Frontend

- `apps/web/app/rate-cards/page.tsx`
- `apps/web/app/rate-cards/rate/page.tsx`
- `apps/web/app/rate-cards/compare/page.tsx`
- `apps/web/app/rate-cards/surcharges/page.tsx`
- `apps/web/app/rate-cards/surcharges/new/page.tsx`
- `apps/web/app/rate-cards/surcharges/[id]/page.tsx`
- `apps/web/components/RateCalculatorForm.tsx`
- `apps/web/components/CompareRatesForm.tsx`
- `apps/web/components/SurchargeForm.tsx`
- `apps/web/components/OperationalReferenceEditor.tsx`
- `apps/web/lib/create-workspace.ts`
- `apps/web/lib/opref-edit.ts`
- `apps/web/lib/rate-field-policy.ts`
- `apps/web/lib/rate-field-policy.test.ts`

### Test

- `tests/LCMS.Api.Tests/MasterDataStandardizationTests.cs` (thêm)
- `tests/LCMS.Api.Tests/BillCreateTotalsTests.cs`
- `tests/LCMS.Api.Tests/BuyRatingLegTests.cs`
- `tests/LCMS.Api.Tests/ChangeBatch20261013Tests.cs`
- `tests/LCMS.Api.Tests/InputOptimizationInheritanceTests.cs`
- `tests/LCMS.Api.Tests/RateRatingBaselineTests.cs`
- `tests/LCMS.Api.Tests/RatingModeTests.cs`
- `tests/LCMS.Api.Tests/RevenuePackageTests.cs`
- `tests/LCMS.Api.Tests/Ui02OperationalCreateContextTests.cs`

## Kết quả build và test (không chạy lại suite sau khi chỉ sửa hồ sơ)

| Việc | Kết quả | Ghi chú |
|---|---|---|
| `dotnet build tests/LCMS.Api.Tests/LCMS.Api.Tests.csproj` | 0 error | |
| `dotnet test` lọc bỏ `RateScopeMigrationPostgresTests` | 579/579 PASS | SQLite tạm của `LcmsApiFactory` |
| `npm test` trong `apps/web` (`vitest run`) | 47/47 PASS | |
| `RateScopeMigrationPostgresTests` | Không chạy | TCP `127.0.0.1:5432` và `127.0.0.1:5433` timeout 1,5 giây. Không có process Postgres. Hai test này `DROP DATABASE` / `CREATE DATABASE` tên `lcms_rate_ac30` và `lcms_rate_baseline_audit` trên cổng 5433. Không dùng chúng để đọc UAT. |

## Kết nối DB dùng cho số liệu trong file 03

| Đích | Kết quả 2026-10-28 |
|---|---|
| Postgres local `127.0.0.1:5432` (`infra/docker-compose.dev.yml`, user dev `lcms`, db `lcms_dev`) | Timeout, container không lắng nghe |
| Postgres `127.0.0.1:5433` (cổng của `RateScopeMigrationPostgresTests`) | Timeout |
| Credential UAT/PO trong `infra/.env` | Không mở file này |
| SQLite `src/LCMS.Api/lcms_dev.db` | Mở read-only (`mode=ro`), 5.324.800 byte, 100 bảng, 1 tenant `ops` |
| SQLite `lcms_dev.db` ở thư mục gốc repo | 0 byte, không có bảng |

Script `SELECT` để PO chạy trên PostgreSQL UAT nằm trong file `03`. Script đó chỉ đọc.

## Triển khai

Code đã nằm trên `origin/main`. GitHub Actions trên `main` triển khai ưu tiên 1 `https://cms.lsslogistics.vn` (`cms-lss-01`) trước, rồi máy phụ `cms-sg-01`. Không `git pull` trên host. Không tar/scp. Không sửa `infra/.env`.

Commit chỉ chứa markdown/xlsx không đổi binary API. Không đẩy commit hồ sơ này lên `main` để tránh một lần deploy không đổi ứng dụng. Muốn PO thấy file trên Git: đẩy riêng khi chấp nhận một lần chạy Actions không đổi hành vi.

Sau khi bản `d2c7c1c` đã lên UAT, kiểm tra bằng tay:

1. Mở Bill đang lưu `air` (ví dụ `TESTLSS002` trên DB local).
2. Chọn cùng phương thức `AIR` và lưu.
3. Không có stale mới. `ratings.context_json` và số tiền Cost không đổi.
4. Đổi sang `SEA` trên một Bill thử thì rating hiện tại được đánh dấu stale, snapshot cũ vẫn nguyên.

## Năm giới hạn

| Giới hạn | Kết quả của commit code |
|---|---|
| Migration / bulk update lịch sử | 0 lần chạy. SQL trong file 03 là đề xuất |
| Sửa snapshot, published rate card, Cost, Revenue, AP/AR | 0 |
| Xóa `stale_at` cũ | 0 |
| Bật/tắt hàng loạt `is_active` | 0 |
| Đổi WATER / EXPRESS / MULTIMODAL hoặc trộn Service Type với Transport Mode | 0 |
