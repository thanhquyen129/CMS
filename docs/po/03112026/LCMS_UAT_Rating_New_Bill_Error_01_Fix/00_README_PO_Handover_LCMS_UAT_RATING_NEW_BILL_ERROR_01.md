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

## 4. BUY / SELL độc lập

Integration test `NewBillRatingBuySellE2ETests`:

1. Tạo Bill mới (không Rating/Cost/Revenue sẵn).  
2. Publish RateCard BUY + SELL.  
3. `POST /api/ratings` BUY `seed*=false` → 200/201, costs=0, revenues=0.  
4. `POST /api/ratings` SELL `seed*=false` → 200/201, costs=0, revenues=0.  
5. Apply BUY ×2 + Apply SELL ×2 → Expected sinh ra, **không** tăng count khi Apply lại cùng rating.

### Log test (verbatim)

```
Passed!  - Failed:     0, Passed:     2, Skipped:     0, Total:     2
Filter: FullyQualifiedName~NewBillRatingBuySellE2ETests
```

## 5. Hotfix môi trường UAT

Đã chạy trên `cms-db` (Priority 1) **ngay khi xác định RCA**:

```sql
ALTER TABLE service_selections ADD COLUMN IF NOT EXISTS deleted_by uuid NULL;
ALTER TABLE service_selection_audits ADD COLUMN IF NOT EXISTS deleted_by uuid NULL;
INSERT INTO __ef_migrations_history (migration_id, product_version)
VALUES ('20261103180000_ServiceSelectionDeletedByColumn', '8.0.0')
ON CONFLICT DO NOTHING;
```

→ **UAT đã mở khóa tính thử** mà không cần chờ image mới (schema-compatible với code đang chạy).  
Commit code + migration vẫn được ship để môi trường khác / rebuild đồng bộ.

## 6. Commit / File / Deploy

- **SHA sửa lỗi:** (stamp sau commit bàn giao)  
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

Screenshots: `screenshots/` (chụp sau khi PO/DEV verify trên UI UAT).
