-- Idempotent seed of baseline EconomicChargeTypes for 'ops' and 'demo' tenants (closes GAP DATA-01).
-- Safe to run multiple times on PostgreSQL (production / UAT / local).

BEGIN;

WITH target_tenants AS (
    SELECT id AS tenant_id, code AS tenant_code
    FROM tenants
    WHERE deleted_at IS NULL
      AND LOWER(code) IN ('ops', 'demo')
),
baseline_items(code, name) AS (
    VALUES
        ('FREIGHT',  'Cước vận tải chính (Base freight)'),
        ('PICKUP',   'Phí lấy hàng tận nơi'),
        ('DELIVERY', 'Phí giao hàng'),
        ('REMOTE',   'Phụ phí vùng xa / hải đảo'),
        ('HANDLING', 'Phí bốc xếp / nâng hạ'),
        ('FUEL',     'Phụ phí nhiên liệu')
),
updated_existing AS (
    UPDATE economic_charge_types e
    SET is_active = TRUE,
        updated_at = NOW()
    FROM target_tenants t, baseline_items b
    WHERE e.tenant_id = t.tenant_id
      AND UPPER(e.code) = b.code
      AND e.deleted_at IS NULL
      AND e.is_active = FALSE
    RETURNING e.id
)
INSERT INTO economic_charge_types (
    id,
    tenant_id,
    code,
    name,
    is_active,
    row_version,
    created_at
)
SELECT
    gen_random_uuid(),
    t.tenant_id,
    b.code,
    b.name,
    TRUE,
    decode('0000000000000001', 'hex'),
    NOW()
FROM target_tenants t
CROSS JOIN baseline_items b
WHERE NOT EXISTS (
    SELECT 1
    FROM economic_charge_types e
    WHERE e.tenant_id = t.tenant_id
      AND UPPER(e.code) = b.code
      AND e.deleted_at IS NULL
);

COMMIT;

SELECT t.code AS tenant_code, e.code, e.name, e.is_active
FROM economic_charge_types e
JOIN tenants t ON t.id = e.tenant_id
WHERE e.deleted_at IS NULL
  AND LOWER(t.code) IN ('ops', 'demo')
ORDER BY t.code, e.code;
