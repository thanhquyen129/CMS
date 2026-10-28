# Kiểm kê dữ liệu và phương án migration (CHƯA CHẠY)

Lượt này không mở DB PO và không chạy script dưới đây.

## Đã biết từ kiểm kê read-only 2026-10-28 (`lcms_dev.db`)

| Bảng | Giá trị mode thấy được | Ghi chú |
|---|---|---|
| `orders`, `bills`, `shipments` | `air`, `sea`, `road`, `rail` | 100% chữ thường trong seed |
| `transport_legs`, `transport_movements` | NULL trên bộ seed volume | Chưa populate |
| `rate_cards`, `pricing_rules` | `air` / `sea` hoặc NULL | Bảng đã phát hành nằm trong nhóm này |
| `master_catalog_items` kind `transport_mode` | `air` trên DB seed | Upsert quản trị lại lưu `AIR` |
| `ratings.context_json` | `transportMode=air` | Không được UPDATE |
| `surcharge_conditions` dimension `transport_mode` | 0 dòng tại thời điểm RCA | Ghi mới sẽ là `AIR` |

Bill `TESTLSS002`: mode `air`, service `FREIGHT`, 14 rating, local `stale_at` NULL. UAT từng có stale giả `AIR → air`. Số tiền không đổi: Net 1.740.000, VAT 139.200, Gross 1.879.200 VND.

## Script đề xuất — chỉ chạy sau khi PO duyệt

Script idempotent. Bước 1 chép giá trị cũ. Bước 2 chỉ sửa cột mode giao dịch và mã danh mục. Không đụng `ratings`, `rating_details`, `costs`, `revenues`, `accounts_payable`, `accounts_receivable`, `stale_at`.

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
SELECT 'master_catalog_items', id, code FROM master_catalog_items
WHERE kind = 'transport_mode' AND code <> upper(btrim(code))
ON CONFLICT (table_name, row_id) DO NOTHING;

-- Draft rate cards and draft pricing rules only. Published rows stay byte-for-byte.
INSERT INTO transport_mode_case_backup (table_name, row_id, old_value)
SELECT 'rate_cards', c.id, c.transport_mode
FROM rate_cards c
WHERE c.transport_mode IS NOT NULL
  AND c.transport_mode <> upper(btrim(c.transport_mode))
  AND NOT EXISTS (
      SELECT 1 FROM rate_versions v
      WHERE v.rate_card_id = c.id AND v.status = 'published')
ON CONFLICT (table_name, row_id) DO NOTHING;

UPDATE orders SET transport_mode = upper(btrim(transport_mode))
WHERE id IN (SELECT row_id FROM transport_mode_case_backup WHERE table_name = 'orders');
UPDATE bills SET transport_mode = upper(btrim(transport_mode))
WHERE id IN (SELECT row_id FROM transport_mode_case_backup WHERE table_name = 'bills');
UPDATE shipments SET transport_mode = upper(btrim(transport_mode))
WHERE id IN (SELECT row_id FROM transport_mode_case_backup WHERE table_name = 'shipments');
UPDATE transport_legs SET transport_mode = upper(btrim(transport_mode))
WHERE id IN (SELECT row_id FROM transport_mode_case_backup WHERE table_name = 'transport_legs');
UPDATE transport_movements SET transport_mode = upper(btrim(transport_mode))
WHERE id IN (SELECT row_id FROM transport_mode_case_backup WHERE table_name = 'transport_movements');
UPDATE master_catalog_items SET code = upper(btrim(code))
WHERE id IN (SELECT row_id FROM transport_mode_case_backup WHERE table_name = 'master_catalog_items');
UPDATE rate_cards SET transport_mode = upper(btrim(transport_mode))
WHERE id IN (SELECT row_id FROM transport_mode_case_backup WHERE table_name = 'rate_cards');

-- Replace COMMIT with this until PO approves:
ROLLBACK;
```

## Rollback

Khôi phục từ `transport_mode_case_backup.old_value` theo `table_name` + `row_id`. Không đụng rating, cost, revenue, AP/AR. Chạy trên bản backup vừa tạo trong cùng cửa sổ bảo trì. Nếu backup table chưa có, không có cách biết chữ gốc — vì vậy không chạy UPDATE khi chưa có bảng backup.
