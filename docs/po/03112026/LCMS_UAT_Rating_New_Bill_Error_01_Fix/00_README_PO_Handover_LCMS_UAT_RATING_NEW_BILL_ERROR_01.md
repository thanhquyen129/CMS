# LCMS-UAT-RATING-NEW-BILL-ERROR-01 — Root Cause & Fix

**Mức độ:** P0 BLOCKER  
**Môi trường:** `https://cms.lsslogistics.vn` (`cms-lss-01`)  
**Bill PO:** `LSSTEST03112026001` (CW = 120 kg)  
**Thời điểm lỗi PO:** `2026-11-03T10:31:43Z` (17:31:43 +07)

## 1. Bằng chứng Log & Root Cause

### Exception gốc (verbatim từ `cms-api`)

Tại **`2026-11-03T10:31:43.978Z`** (khớp mốc PO), `POST /api/ratings` ném:

```
Npgsql.PostgresException (0x80004005): 42703: column s.deleted_by does not exist
POSITION: 126
```

SQL EF sinh ra khi đọc `service_selections` (soft-delete filter):

```sql
SELECT s.id, s.confirmed_at, s.confirmed_by_user_id, s.context_id, s.context_type,
       s.created_at, s.created_by, s.deleted_at, s.deleted_by, s.is_selected, ...
FROM service_selections AS s
WHERE ...
```

**CorrelationId:** `242ec2fe0fbb4ed7a950fded3df810ff`  
Cùng lỗi cũng xuất hiện lúc `10:23:09Z` / `10:23:12Z` / `10:23:13Z` (retry BUY/SELL).

### Root cause (đã xác minh, không suy đoán)

1. Migration `20261102170000_SurchargeActivationPolicy` tạo bảng `service_selections` / `service_selection_audits` **có** `deleted_at` nhưng **thiếu** `deleted_by`.
2. Entity `ServiceSelection` : `TenantEntityBase` / `EntityBase` **luôn** map `DeletedBy` → cột `deleted_by`.
3. Global soft-delete filter SELECT đủ cột audit → Postgres **42703** → middleware trả `HTTP 500` / `internal_error` qua BFF `/bff/ratings`.
4. UI “tìm được bảng giá” vẫn OK vì bước resolve RateCard **chưa** query `service_selections`; lỗi chỉ khi **Tính thử** chạy `CreateRatingCommand` → Surcharge activation đọc selections.

**Không phải** lỗi Double-Count Dedup, **không phải** thiếu cột EconomicIdentity (các cột `economic_identity_key` đã có trên prod).

## 2. Đối chiếu Migration / Schema

| Thành phần | Code / Migration | Postgres UAT (trước hotfix) | Sau hotfix |
|------------|------------------|-----------------------------|------------|
| `service_selections.deleted_at` | Có trong CreateTable | Có | Có |
| `service_selections.deleted_by` | EntityBase — **thiếu trong CreateTable** | **Không** | **Có** |
| `service_selection_audits.deleted_by` | EntityBase — thiếu CreateTable | **Không** | **Có** |
| `costs/revenues` economic identity | `20261103120000` | Đã apply | Đã apply |
| `rating_details.application_bucket`… | `20261102170000` | Có | Có |
| `__ef_migrations_history` | — | Có `20261102170000`, `20261103120000` | + `20261103180000_ServiceSelectionDeletedByColumn` |

Migration chính thức mới: `20261103180000_ServiceSelectionDeletedByColumn` (idempotent `ADD COLUMN IF NOT EXISTS` trên Npgsql).

## 3. Rating Context / Surcharge / Rate Card / Save Result

Trên Bill mới: nạp Bill + RateVersion OK → tới bước query `ServiceSelections` thì 500.  
Sau khi có `deleted_by`: CreateRating với `seedExpectedCosts/Revenues = false` lưu `Rating` + `RatingDetail` (base + surcharge buckets) **không** seed Cost/Revenue.

## 4. BUY / SELL độc lập — Smoke UAT thực tế (`cms-api` / Bill `LSSTEST031126001`)

Bill trên `cms-db`: `LSSTEST031126001` id `01a1a0eb-eb40-7923-bca2-e0bb884f5093` (tương đương PO `LSSTEST03112026001`).  
`seedExpectedCosts=false`, `seedExpectedRevenues=false`, `quantity=120`.

### 4.1 EntityBase column audit (gate chống bẫy Postgres chỉ báo cột đầu)

Đối chiếu đủ thuộc tính `EntityBase`/`TenantEntityBase` (`id`, `tenant_id`, `created_at`, `created_by`, `updated_at`, `updated_by`, `deleted_at`, `deleted_by`, `row_version`) với `information_schema` trên:

- `service_selections` — **ok hết**
- `service_selection_audits` — **ok hết**
- `surcharge_activation_migration_audits` — **ok hết** (đã bổ sung `deleted_by` vào migration `20261103180000` + ALTER UAT)

**Không còn cột EntityBase nào MISSING.**

### 4.2 `POST /api/ratings` — CREATE (HTTP 201)

**BUY** (`cost@cms.local`, rateVersion `01a17170-ae29-77f7-844c-8fdc5b3bdb7d`):

```json
{"id":"01a1a131-d544-77d8-bdac-e824dd458a02"}
```
HTTP:201

**SELL** (`revenue@cms.local`, rateVersion `01a10a22-29b1-75ef-9607-0b058d95a97a`):

```json
{"id":"01a1a131-e260-7118-8421-29aac7306afc"}
```
HTTP:201

### 4.3 `GET /api/ratings/{id}` — snapshot (HTTP 200, rút gọn)

**BUY:**

```json
{
  "id": "01a1a131-d544-77d8-bdac-e824dd458a02",
  "billId": "01a1a0eb-eb40-7923-bca2-e0bb884f5093",
  "rateVersionId": "01a17170-ae29-77f7-844c-8fdc5b3bdb7d",
  "ratedAt": "2026-11-03T11:36:00.068963+00:00",
  "currencyCode": "VND",
  "totalAmount": 1440000.0000,
  "quantity": 120.0000,
  "serviceTypeCode": "FREIGHT",
  "partyTypeCode": "vendor",
  "routeCode": "SGN-HAN",
  "status": "completed",
  "details": [{
    "ruleCode": "QT0001",
    "componentCode": "FREIGHT",
    "financialNature": "cost",
    "financialMaturity": "expected",
    "amount": 1440000.0000,
    "netAmount": 1440000.0000,
    "vatRate": 8.0000,
    "vatAmount": 115200.0000,
    "grossAmount": 1555200.0000
  }]
}
```
HTTP:200

**SELL:**

```json
{
  "id": "01a1a131-e260-7118-8421-29aac7306afc",
  "billId": "01a1a0eb-eb40-7923-bca2-e0bb884f5093",
  "rateVersionId": "01a10a22-29b1-75ef-9607-0b058d95a97a",
  "ratedAt": "2026-11-03T11:36:03.424561+00:00",
  "currencyCode": "VND",
  "totalAmount": 2040000.0000,
  "quantity": 120.0000,
  "serviceTypeCode": "FREIGHT",
  "partyTypeCode": "customer",
  "routeCode": "SGN-HAN",
  "status": "completed",
  "details": [{
    "ruleCode": "BGEMSLOGS_AIR_SGN_HAN_DONGCO",
    "financialNature": "revenue",
    "financialMaturity": "expected",
    "amount": 2040000.0000,
    "netAmount": 2040000.0000,
    "vatRate": 8.0000,
    "vatAmount": 163200.0000,
    "grossAmount": 2203200.0000
  }]
}
```
HTTP:200

### 4.4 Apply-financials ×2 — không cộng trùng

**BUY apply 1:**

```json
{"ratingId":"01a1a131-d544-77d8-bdac-e824dd458a02","direction":"BUY","createdCount":1,"existingCount":0,"factIds":["01a1a131-f690-7578-b21f-0c5c1f3454fa"]}
```
HTTP:200

**BUY apply 2 (idempotent):**

```json
{"ratingId":"01a1a131-d544-77d8-bdac-e824dd458a02","direction":"BUY","createdCount":0,"existingCount":1,"factIds":["01a1a131-f690-7578-b21f-0c5c1f3454fa"]}
```
HTTP:200

**SELL apply 1:**

```json
{"ratingId":"01a1a131-e260-7118-8421-29aac7306afc","direction":"SELL","createdCount":1,"existingCount":0,"factIds":["01a1a131-fee9-723b-b42b-d01135dd9613"]}
```
HTTP:200

**SELL apply 2 (idempotent):**

```json
{"ratingId":"01a1a131-e260-7118-8421-29aac7306afc","direction":"SELL","createdCount":0,"existingCount":1,"factIds":["01a1a131-fee9-723b-b42b-d01135dd9613"]}
```
HTTP:200

Integration test `NewBillRatingBuySellE2ETests`: **2/2 PASS**.

## 5. Hotfix môi trường UAT

Đã chạy trên `cms-db` (Priority 1) **ngay khi xác định RCA** (+ gate audit EntityBase):

```sql
ALTER TABLE service_selections ADD COLUMN IF NOT EXISTS deleted_by uuid NULL;
ALTER TABLE service_selection_audits ADD COLUMN IF NOT EXISTS deleted_by uuid NULL;
ALTER TABLE surcharge_activation_migration_audits ADD COLUMN IF NOT EXISTS deleted_by uuid NULL;
INSERT INTO __ef_migrations_history (migration_id, product_version)
VALUES ('20261103180000_ServiceSelectionDeletedByColumn', '8.0.0')
ON CONFLICT DO NOTHING;
```

→ **UAT đã mở khóa tính thử** (smoke BUY/SELL HTTP 201 + UI TryCalculate thành công trên Bill trên).  
Commit code + migration ship để môi trường khác / rebuild đồng bộ.

## 6. Commit / File / Deploy

- **SHA sửa lỗi:** 9eb1096c7e55efeb8b2b8a10471cebe8c5aeb822  
- Files chính:
  - `src/LCMS.Infrastructure/Persistence/Migrations/20261103180000_ServiceSelectionDeletedByColumn.cs`
  - `tests/LCMS.Api.Tests/NewBillRatingBuySellE2ETests.cs`
  - `Docs/PO/03112026/LCMS_UAT_Rating_New_Bill_Error_01_Fix/`
- **G4 Production image rebuild:** theo GitHub Actions trên `main` (Priority 1 trước). Schema UAT đã hotfix.

## 7. Hướng dẫn PO retest

1. Mở Bill `LSSTEST03112026001` (hoặc Bill mới CW=120).  
2. **Tính thử giá MUA** → kỳ vọng 200, có Rating snapshot, **chưa** có Cost mới nếu chưa Apply.  
3. **Tính thử giá BÁN** → tương tự.  
4. **Áp dụng** BUY rồi SELL (lặp lại) → không cộng trùng Expected.

Screenshots DEV đã verify: `screenshots/01_BUY_TryCalculate_Success.png`, `02_SELL_TryCalculate_Success.png`, `03_Apply_NoDoubleCount.png`.
