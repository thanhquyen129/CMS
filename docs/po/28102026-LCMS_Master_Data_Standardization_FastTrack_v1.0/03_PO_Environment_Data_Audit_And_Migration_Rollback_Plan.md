# Kiểm kê dữ liệu và migration (chưa chạy)

## 1. Cổng DB đã thử

| Đích | Cách thử | Kết quả |
|---|---|---|
| `127.0.0.1:5432` | TCP timeout 1,5 giây | Không có Postgres. `infra/docker-compose.dev.yml` map cổng này cho container `cms-db-dev`, container không chạy |
| `127.0.0.1:5433` | TCP timeout 1,5 giây | Không chạy `RateScopeMigrationPostgresTests`. Hai test đó tạo và xóa database `lcms_rate_ac30`, `lcms_rate_baseline_audit`. Không phải lệnh đọc UAT |
| `infra/.env` | Không mở | Không lấy credential UAT từ file môi trường |
| `src/LCMS.Api/lcms_dev.db` | SQLite URI `mode=ro`, 5.324.800 byte, 100 bảng | Đã chạy `SELECT` ngày 2026-10-28. Một tenant: `ops` / `Vận hành (demo)` |
| `lcms_dev.db` ở gốc repo | Kích thước 0 | Bỏ qua |

Số liệu bên dưới là của file SQLite đó. PostgreSQL UAT chưa đọc được từ máy này. Mục 4 là script để DBA chạy trên UAT, chỉ `SELECT`.

## 2. Đếm theo giá trị mã

### Mode trên bảng giao dịch

| Bảng | Cột | Tổng dòng | Giá trị | Số dòng | Chữ thường khác `upper(trim)` |
|---|---|---:|---|---:|---:|
| `orders` | `transport_mode` | 120 | `air` | 30 | 120 |
| `orders` | `transport_mode` | | `sea` | 30 | |
| `orders` | `transport_mode` | | `road` | 30 | |
| `orders` | `transport_mode` | | `rail` | 30 | |
| `bills` | `transport_mode` | 132 | `air` | 32 | 122 |
| `bills` | `transport_mode` | | `sea` | 30 | |
| `bills` | `transport_mode` | | `road` | 30 | |
| `bills` | `transport_mode` | | `rail` | 30 | |
| `bills` | `transport_mode` | | NULL | 10 | |
| `shipments` | `transport_mode` | 120 | `air` / `sea` / `road` / `rail` | 30 mỗi mã | 120 |
| `transport_legs` | `transport_mode` | 120 | NULL | 120 | 0 |
| `transport_movements` | `transport_mode` | 120 | NULL | 120 | 0 |
| `rate_cards` | `transport_mode` | 124 | NULL | 121 | 3 |
| `rate_cards` | `transport_mode` | | `air` | 2 | published: `NSE-AIR-VN-MY`, `TDNT_SGN_HAN` (2 version published) |
| `rate_cards` | `transport_mode` | | `sea` | 1 | published: `NSE-SEA-VN-MY` |
| `pricing_rules` | `transport_mode` | 129 | NULL | 126 | |
| `pricing_rules` | `transport_mode` | | `air` | 3 | cả 3 thuộc `rate_versions.status = published`. Draft có mode chữ thường: 0 |
| `routes` | `transport_mode_code` | 1 | `air` | 1 | `SGN-HAN`, service `FREIGHT` |
| `surcharge_conditions` | `dimension`,`value_text` | 4 | `destination`/`SBH` | 2 | |
| `surcharge_conditions` | | | `destination`/`SWK` | 2 | |
| `surcharge_conditions` | `dimension = transport_mode` | 0 | | 0 | Draft mode condition: 0 |
| `ratings` | `context_json.transportMode` | 16 | `air` | 16 | Không UPDATE |
| `ratings` | `context_json.serviceType` | 16 | `FREIGHT` 14, `freight` 1, `master` 1 | | `freight` và `master` không thuộc TESTLSS002 |
| `ratings` | `party_type_code` | 16 | `vendor` | 16 | |
| `ratings` | `context_type` | 16 | `bill` | 16 | |
| `ratings` | `stale_at` | 16 | NULL | 16 | |
| `rate_versions` | `status` | 126 | `published` 125, `draft` 1 | | |
| `surcharge_versions` | `publish_status` | 6 | `published` | 6 | |

### `master_catalog_items` theo kind

| kind | Số dòng | Mã (mỗi mã đúng 1 dòng, active) |
|---|---:|---|
| `cost_type` | 3 | `AP-FREIGHT`, `FREIGHT`, `FUEL` |
| `document_type` | 2 | `debit_note`, `invoice` |
| `location` | 120 | `VOL-LOC-0001` … `VOL-LOC-0120` |
| `payment_term` | 2 | `NET15`, `NET30` |
| `pricing_component` | 1 | `FREIGHT` |
| `revenue_type` | 2 | `AR-FREIGHT`, `FREIGHT-REV` |
| `service_type` | 2 | `AIR` (di sản), `FREIGHT` |
| `transport_mode` | 1 | `air` (chữ thường, active) |
| `transport_route` | 120 | `VOL-RTE-0001` … `VOL-RTE-0120` |

Không có hai dòng cùng `(kind, code)`.

### Bảng master khác

| Bảng | Tổng | Phân bố |
|---|---:|---|
| `currencies` | 15 | AUD, CAD, CHF, CNY, EUR, GBP, HKD, JPY, KRW, MYR, SGD, THB, TWD, USD, VND. Mỗi mã 1, active. Global, không `tenant_id` |
| `commodity_types` | 5 | COSMETICS, COSMETICS_FOOD, DRY_FOOD, EXPRESS, GENERAL. Active, `is_dangerous_goods = 0` |
| `economic_charge_types` | 6 | DELIVERY, FREIGHT, FUEL, HANDLING, PICKUP, REMOTE. Active |
| `locations` | 2 | `SGN` airport VN, `HAN` airport VN. Khác 120 mã `VOL-LOC-*` trong catalog |
| `routes` | 1 | `SGN-HAN` / mode `air` / service `FREIGHT` |
| `party_roles` | 484 | `customer` 121, `payee` 121, `payer` 121, `vendor` 121. Active |
| `bills.service_type_code` | 132 | NULL 130, `FREIGHT` 1, `AIR` 1 |
| `bills.incoterm_code` | 132 | NULL 132 |
| `audit_events` | 51 | `rate_card.version_created` 24, `rate_card.declaration_changed` 24, `rate_card.published` 3. Không có `rating.mark_stale` |

### TESTLSS002

Bill `01A16CF7-30D8-731C-A50F-928FC7AA575B`, mode `air`, service `FREIGHT`, route `SGN-HAN`. 14 rating ở file `01`. Audit gắn Bill này: 0 dòng. Cost expected không sửa: 1.440.000 `FREIGHT` và 300.000 `DONGGOI_SGN_HAN_MUA`.

## 3. Script đọc cho PostgreSQL UAT

Chạy bằng role chỉ có `SELECT`. Đổi tên schema nếu không phải `public`. Không có `INSERT`/`UPDATE`/`DELETE`.

```sql
-- Read-only inventory. Safe to run on UAT.
SELECT 'orders' AS table_name, COALESCE(transport_mode, '(NULL)') AS code, COUNT(*) AS n
FROM orders GROUP BY 1, 2
UNION ALL
SELECT 'bills', COALESCE(transport_mode, '(NULL)'), COUNT(*) FROM bills GROUP BY 1, 2
UNION ALL
SELECT 'shipments', COALESCE(transport_mode, '(NULL)'), COUNT(*) FROM shipments GROUP BY 1, 2
UNION ALL
SELECT 'transport_legs', COALESCE(transport_mode, '(NULL)'), COUNT(*) FROM transport_legs GROUP BY 1, 2
UNION ALL
SELECT 'transport_movements', COALESCE(transport_mode, '(NULL)'), COUNT(*) FROM transport_movements GROUP BY 1, 2
UNION ALL
SELECT 'rate_cards', COALESCE(transport_mode, '(NULL)'), COUNT(*) FROM rate_cards GROUP BY 1, 2
UNION ALL
SELECT 'pricing_rules', COALESCE(transport_mode, '(NULL)'), COUNT(*) FROM pricing_rules GROUP BY 1, 2
UNION ALL
SELECT 'routes', COALESCE(transport_mode_code, '(NULL)'), COUNT(*) FROM routes GROUP BY 1, 2
UNION ALL
SELECT 'surcharge_conditions', dimension || '=' || COALESCE(value_text, '(NULL)'), COUNT(*)
FROM surcharge_conditions GROUP BY 1, 2
ORDER BY 1, 3 DESC;

SELECT COALESCE(jsonb_extract_path_text(context_json::jsonb, 'transportMode'), '(missing)') AS mode, COUNT(*)
FROM ratings GROUP BY 1;

SELECT kind, code, is_active, COUNT(*) FROM master_catalog_items GROUP BY 1, 2, 3 ORDER BY 1, 2;
SELECT code, is_active, COUNT(*) FROM currencies GROUP BY 1, 2 ORDER BY 1;
SELECT code, is_active, COUNT(*) FROM commodity_types GROUP BY 1, 2 ORDER BY 1;
SELECT code, is_active, COUNT(*) FROM economic_charge_types GROUP BY 1, 2 ORDER BY 1;
SELECT code, location_type, country_code, COUNT(*) FROM locations GROUP BY 1, 2, 3 ORDER BY 1;
SELECT code, transport_mode_code, service_type_code, COUNT(*) FROM routes GROUP BY 1, 2, 3;
SELECT role_code, is_active, COUNT(*) FROM party_roles GROUP BY 1, 2 ORDER BY 1;

SELECT r.id, r.rated_at, r.party_type_code, r.context_type,
       jsonb_extract_path_text(r.context_json::jsonb, 'transportMode') AS transport_mode,
       r.total_amount, r.currency_code, r.stale_at, r.stale_reason, r.rate_version_id
FROM ratings r
JOIN bills b ON b.id = r.bill_id
WHERE b.bill_no = 'TESTLSS002'
ORDER BY r.rated_at, r.id;

SELECT a.action, a.occurred_at, a.object_type, a.object_id, left(COALESCE(a.reason, ''), 200) AS reason
FROM audit_events a
JOIN bills b ON b.bill_no = 'TESTLSS002'
WHERE a.object_id = b.id
   OR a.reason ILIKE '%TESTLSS002%'
   OR a.before_json ILIKE '%TESTLSS002%'
   OR a.after_json ILIKE '%TESTLSS002%'
ORDER BY a.occurred_at;
```

Nếu cột `context_json` là `text`, giữ `::jsonb` như trên. Nếu đã là `jsonb`, bỏ cast.

## 4. Script đề xuất — chưa chạy

Chỉ uppercase chữ mode đang thường. Không đụng `ratings`, `rating_details`, `costs`, `revenues`, `accounts_payable`, `accounts_receivable`, `stale_at`. Không đụng rate card đã có version `published`. Không đụng `pricing_rules` của version `published`. Không đụng `surcharge_conditions` của version phụ phí `published`.

Trên SQLite local, nhánh draft của `pricing_rules` và `surcharge_conditions` hiện có 0 dòng chữ thường. Script vẫn đủ để UAT nếu có draft.

Mặc định kết thúc bằng `ROLLBACK`. PO đổi thành `COMMIT` sau khi xem `SELECT COUNT(*) FROM transport_mode_case_backup`.

```sql
-- PROPOSAL ONLY. Do not execute until PO signs.
BEGIN;

CREATE TABLE IF NOT EXISTS transport_mode_case_backup (
    backed_up_at timestamptz NOT NULL DEFAULT now(),
    table_name text NOT NULL,
    row_id uuid NOT NULL,
    old_value text,
    PRIMARY KEY (table_name, row_id)
);

INSERT INTO transport_mode_case_backup (table_name, row_id, old_value)
SELECT 'orders', id, transport_mode FROM orders
WHERE transport_mode IS NOT NULL AND transport_mode <> upper(btrim(transport_mode))
ON CONFLICT (table_name, row_id) DO NOTHING;

INSERT INTO transport_mode_case_backup (table_name, row_id, old_value)
SELECT 'bills', id, transport_mode FROM bills
WHERE transport_mode IS NOT NULL AND transport_mode <> upper(btrim(transport_mode))
ON CONFLICT (table_name, row_id) DO NOTHING;

INSERT INTO transport_mode_case_backup (table_name, row_id, old_value)
SELECT 'shipments', id, transport_mode FROM shipments
WHERE transport_mode IS NOT NULL AND transport_mode <> upper(btrim(transport_mode))
ON CONFLICT (table_name, row_id) DO NOTHING;

INSERT INTO transport_mode_case_backup (table_name, row_id, old_value)
SELECT 'transport_legs', id, transport_mode FROM transport_legs
WHERE transport_mode IS NOT NULL AND transport_mode <> upper(btrim(transport_mode))
ON CONFLICT (table_name, row_id) DO NOTHING;

INSERT INTO transport_mode_case_backup (table_name, row_id, old_value)
SELECT 'transport_movements', id, transport_mode FROM transport_movements
WHERE transport_mode IS NOT NULL AND transport_mode <> upper(btrim(transport_mode))
ON CONFLICT (table_name, row_id) DO NOTHING;

INSERT INTO transport_mode_case_backup (table_name, row_id, old_value)
SELECT 'routes', id, transport_mode_code FROM routes
WHERE transport_mode_code IS NOT NULL AND transport_mode_code <> upper(btrim(transport_mode_code))
ON CONFLICT (table_name, row_id) DO NOTHING;

INSERT INTO transport_mode_case_backup (table_name, row_id, old_value)
SELECT 'master_catalog_items', id, code FROM master_catalog_items
WHERE kind = 'transport_mode' AND code <> upper(btrim(code))
ON CONFLICT (table_name, row_id) DO NOTHING;

-- Draft rate cards only. A card with any published version is excluded.
INSERT INTO transport_mode_case_backup (table_name, row_id, old_value)
SELECT 'rate_cards', c.id, c.transport_mode
FROM rate_cards c
WHERE c.transport_mode IS NOT NULL
  AND c.transport_mode <> upper(btrim(c.transport_mode))
  AND NOT EXISTS (
      SELECT 1 FROM rate_versions v
      WHERE v.rate_card_id = c.id AND v.status = 'published' AND v.deleted_at IS NULL)
ON CONFLICT (table_name, row_id) DO NOTHING;

-- Draft pricing rules only. Published versions stay byte-for-byte.
INSERT INTO transport_mode_case_backup (table_name, row_id, old_value)
SELECT 'pricing_rules', r.id, r.transport_mode
FROM pricing_rules r
JOIN rate_versions v ON v.id = r.rate_version_id
WHERE v.status = 'draft'
  AND v.deleted_at IS NULL
  AND r.deleted_at IS NULL
  AND r.transport_mode IS NOT NULL
  AND r.transport_mode <> upper(btrim(r.transport_mode))
ON CONFLICT (table_name, row_id) DO NOTHING;

-- Draft surcharge conditions whose dimension is transport_mode.
INSERT INTO transport_mode_case_backup (table_name, row_id, old_value)
SELECT 'surcharge_conditions', sc.id, sc.value_text
FROM surcharge_conditions sc
JOIN surcharge_rules sr ON sr.id = sc.surcharge_rule_id
JOIN surcharge_versions sv ON sv.id = sr.surcharge_version_id
WHERE sc.dimension = 'transport_mode'
  AND sv.publish_status = 'draft'
  AND sc.deleted_at IS NULL
  AND sr.deleted_at IS NULL
  AND sv.deleted_at IS NULL
  AND sc.value_text IS NOT NULL
  AND sc.value_text <> upper(btrim(sc.value_text))
ON CONFLICT (table_name, row_id) DO NOTHING;

UPDATE orders o SET transport_mode = upper(btrim(o.transport_mode))
FROM transport_mode_case_backup b
WHERE b.table_name = 'orders' AND b.row_id = o.id;

UPDATE bills o SET transport_mode = upper(btrim(o.transport_mode))
FROM transport_mode_case_backup b
WHERE b.table_name = 'bills' AND b.row_id = o.id;

UPDATE shipments o SET transport_mode = upper(btrim(o.transport_mode))
FROM transport_mode_case_backup b
WHERE b.table_name = 'shipments' AND b.row_id = o.id;

UPDATE transport_legs o SET transport_mode = upper(btrim(o.transport_mode))
FROM transport_mode_case_backup b
WHERE b.table_name = 'transport_legs' AND b.row_id = o.id;

UPDATE transport_movements o SET transport_mode = upper(btrim(o.transport_mode))
FROM transport_mode_case_backup b
WHERE b.table_name = 'transport_movements' AND b.row_id = o.id;

UPDATE routes o SET transport_mode_code = upper(btrim(o.transport_mode_code))
FROM transport_mode_case_backup b
WHERE b.table_name = 'routes' AND b.row_id = o.id;

UPDATE master_catalog_items o SET code = upper(btrim(o.code))
FROM transport_mode_case_backup b
WHERE b.table_name = 'master_catalog_items' AND b.row_id = o.id;

UPDATE rate_cards o SET transport_mode = upper(btrim(o.transport_mode))
FROM transport_mode_case_backup b
WHERE b.table_name = 'rate_cards' AND b.row_id = o.id;

UPDATE pricing_rules o SET transport_mode = upper(btrim(o.transport_mode))
FROM transport_mode_case_backup b
WHERE b.table_name = 'pricing_rules' AND b.row_id = o.id;

UPDATE surcharge_conditions o SET value_text = upper(btrim(o.value_text))
FROM transport_mode_case_backup b
WHERE b.table_name = 'surcharge_conditions' AND b.row_id = o.id;

-- Inspect, then replace ROLLBACK with COMMIT only after PO approval.
ROLLBACK;
```

## 5. Rollback hoàn chỉnh

Chạy khi bảng `transport_mode_case_backup` còn và `old_value` là chữ trước UPDATE. Không đụng rating, cost, revenue, AP/AR.

```sql
BEGIN;

UPDATE orders o SET transport_mode = b.old_value
FROM transport_mode_case_backup b
WHERE b.table_name = 'orders' AND b.row_id = o.id;

UPDATE bills o SET transport_mode = b.old_value
FROM transport_mode_case_backup b
WHERE b.table_name = 'bills' AND b.row_id = o.id;

UPDATE shipments o SET transport_mode = b.old_value
FROM transport_mode_case_backup b
WHERE b.table_name = 'shipments' AND b.row_id = o.id;

UPDATE transport_legs o SET transport_mode = b.old_value
FROM transport_mode_case_backup b
WHERE b.table_name = 'transport_legs' AND b.row_id = o.id;

UPDATE transport_movements o SET transport_mode = b.old_value
FROM transport_mode_case_backup b
WHERE b.table_name = 'transport_movements' AND b.row_id = o.id;

UPDATE routes o SET transport_mode_code = b.old_value
FROM transport_mode_case_backup b
WHERE b.table_name = 'routes' AND b.row_id = o.id;

UPDATE master_catalog_items o SET code = b.old_value
FROM transport_mode_case_backup b
WHERE b.table_name = 'master_catalog_items' AND b.row_id = o.id;

UPDATE rate_cards o SET transport_mode = b.old_value
FROM transport_mode_case_backup b
WHERE b.table_name = 'rate_cards' AND b.row_id = o.id;

UPDATE pricing_rules o SET transport_mode = b.old_value
FROM transport_mode_case_backup b
WHERE b.table_name = 'pricing_rules' AND b.row_id = o.id;

UPDATE surcharge_conditions o SET value_text = b.old_value
FROM transport_mode_case_backup b
WHERE b.table_name = 'surcharge_conditions' AND b.row_id = o.id;

COMMIT;
```

Nếu `transport_mode_case_backup` không tồn tại thì dừng. Không có bảng này thì không biết chữ gốc.
