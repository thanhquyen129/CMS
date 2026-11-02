# Ma trận kiểm thử DEV — 25 Delta + 32 FU + 37 CR + CI Logs (Audit remaster)

`PO UAT` = **NOT RUN** trên mọi dòng. `DEV Test` = **PASS**.

Excel:
- `LCMS_DEV_UAT_Followup_Matrix_v1.0_20261026.xlsx` — `04_UAT_32` (32 dòng)
- `LCMS_DEV_UAT_Correction_Matrix_v1.0_20261027.xlsx` — `04_UAT` (37 dòng) + `02_QuyTac` / `05_XacMinh` / `06_HoiQuy` / `08_BanGiao`

## Bảng 25 kịch bản Delta (TC-01 → TC-25)

| TC | DEV Test | PO UAT | Bằng chứng thực thi |
|---|---|---|---|
| TC-01 | PASS | NOT RUN | Bill context PATCH + cargo store · BillCreateTotalsTests |
| TC-02 | PASS | NOT RUN | Bỏ fallback billType/mode vào Loại DV · RatingReadiness |
| TC-03 | PASS | NOT RUN | catalogModeOptions + API reject mode ngoài catalog |
| TC-04 | PASS | NOT RUN | CompareRatesQuery dùng BillRateSuggestion + serviceTypeCode |
| TC-05 | PASS | NOT RUN | `Sell_compare_does_not_price_the_reverse_route` |
| TC-06 | PASS | NOT RUN | BUY/SELL seed guard · suite rating · R-01 số không đổi |
| TC-07 | PASS | NOT RUN | `Cost_rating_trace_reads_freight_base_rate_and_donggoi...` |
| TC-08 | PASS | NOT RUN | UI FIXED / khoản; đ/kg = tham khảo |
| TC-09 | PASS | NOT RUN | DateInputVi DD/MM/YYYY |
| TC-10 | PASS | NOT RUN | CreateRevenueForm roleCode=customer |
| TC-11 | PASS | NOT RUN | Audit revenue.confirm / actualize |
| TC-12 | PASS | NOT RUN | KPI "Số dư … trạng thái hiện hành" |
| TC-13 | PASS | NOT RUN | Shared 240k bất biến · PC-21 disable |
| TC-14 | PASS | NOT RUN | finalizedAllocations chung hồ sơ |
| TC-15 | PASS | NOT RUN | Tab approvals pending/approved/rejected/all |
| TC-16 | PASS | NOT RUN | 3 chiều chứng từ độc lập |
| TC-17 | PASS | NOT RUN | `Exception_exposure_requires_notes_and_valid_ar_role` |
| TC-18 | PASS | NOT RUN | Recognize manual; Accept ≠ AR |
| TC-19 | PASS | NOT RUN | Trần AR theo revenue · idempotency key |
| TC-20 | PASS | NOT RUN | revenueMaturityModel two/three |
| TC-21 | PASS | NOT RUN | DEC-02/03 nhãn + 2 bảng |
| TC-22 | PASS | NOT RUN | `Tc22_partial_collection_keeps_bill_profit_unchanged` — Settled=200k Outstanding=300k Profit=380k |
| TC-23 | PASS | NOT RUN | `Tc23_remaining_collection_settles_ar_and_retry_is_idempotent` — Settled=500k Outstanding=0 Aging + retry |
| TC-24 | PASS | NOT RUN | `Tc24_revenue_split_conserves_500k_across_two_bills_without_doubling` — 300k+200k; replace ≠ 1.000.000 |
| TC-25 | PASS | NOT RUN | ProfitabilityBoardQueries route_code / O-D |

## Bảng 32 Follow-up 26/10 (TC-01 → TC-32)

Toàn bộ sheet `04_UAT_32`: `DEV Test=PASS`, `PO UAT=NOT RUN`, cột `DEV Evidence` + `Ghi chú` đã điền (không NaN). Nguồn evidence: ConsolidatedUat + suite FU26.

## Bảng 37 Correction 27/10 (FU27-TC-001 → FU27-TC-037)

Toàn bộ sheet `04_UAT`: `DEV Test=PASS`, `PO UAT=NOT RUN`, `Bằng chứng thực thi` + `Ghi chú` đã điền. Mapping trọng tâm:
- FU27-TC-021/022 ↔ Cost FREIGHT/DONGGOI trace
- FU27-TC-025 ↔ missing_link
- Chiều tuyến ↔ RatingEngine IsBidirectional

## REG-01 → REG-10

Sheet `06_HoiQuy`: `DEV Regression=PASS` + chú thích evidence cho cả 10 dòng (R-01…R-10 bảo vệ).

## LCMS-FT-01 … FT-08

| ID | DEV |
|---|---|
| FT-01 | PASS — service/mode tách |
| FT-02 | PASS — SameMode AIR/air |
| FT-03 | PASS — trace không bịa mã |
| FT-04 | PASS — FIXED ≠ unit rate tiền |
| FT-05 | PASS — catalogModeOptions |
| FT-06 | PASS trên CI với Postgres service; local portable `:5433` khi bật |
| FT-07 | PASS — 37 TC sheet |
| FT-08 | No Change — không gộp master trùng |

## Log thực thi (final polish — sau remaster)

### Full suite `Cms.sln` Release + Postgres `:5433`

```
LCMS_TEST_POSTGRES="Host=127.0.0.1;Port=5433;Database=postgres;Username=lcms;Password=lcms"
dotnet test Cms.sln -c Release
Passed!  - Failed: 0, Passed: 598, Skipped: 0, Total: 598, Duration: 2 m 39 s - LCMS.Api.Tests.dll (net8.0)
exit 0
```

Gồm `ConsolidatedUat20261102*` (10) + `RateScopeMigrationPostgresTests` (2, Skipped: 0) + toàn bộ suite API. Helper Sprint10 dùng `ExposureAnchor` (notes + role) khớp basis exposure sau remaster.

### ConsolidatedUat20261102

```
dotnet test tests/LCMS.Api.Tests/LCMS.Api.Tests.csproj -c Release --filter "FullyQualifiedName~ConsolidatedUat20261102"
Passed!  - Failed: 0, Passed: 10, Skipped: 0, Total: 10
```

### RateScopeMigrationPostgresTests

```
LCMS_TEST_POSTGRES=Host=127.0.0.1;Port=5433;Database=postgres;Username=lcms;Password=lcms
dotnet test ... --filter FullyQualifiedName~RateScopeMigrationPostgresTests
Passed!  - Failed: 0, Passed: 2, Skipped: 0, Total: 2
```

Postgres portable 16.4 local `:5433` (trust, user `lcms`). DB tạm `lcms_rate_ac30` / `lcms_rate_baseline_audit` — không đụng DB smoke `lcms`.

### CI GitHub Actions

`.github/workflows/ci.yml` job `test`:
- service `postgres:16-alpine` port `5432`, user/password/db `lcms`
- env `LCMS_TEST_POSTGRES=Host=127.0.0.1;Port=5432;Database=postgres;Username=lcms;Password=lcms`
- `dotnet test Cms.sln -c Release` — `RequiresIsolatedPostgres` không Skip khi mở socket

Migration SQL:
```sql
ALTER TABLE pricing_rules ADD COLUMN is_bidirectional boolean NOT NULL DEFAULT false;
```
File: `src/LCMS.Infrastructure/Persistence/Migrations/20261102120000_PricingRuleIsBidirectional.cs`

### Web (sau remaster / CreateExposureForm)

```
cd apps/web && npm test
 Test Files  7 passed (7)
      Tests  47 passed (47)
 Duration  14.83s

npx tsc --noEmit
exit 0
```

## CreditBlock

`CreditBlock_RoleGate_SoftDeleteOpenAr_And_Export` tạo exposure ngoại lệ với `counterpartyId` + `notes` → PASS (không còn amount-only).
