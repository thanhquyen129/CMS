-- LCMS-CR-20261014-01 AC-23. Read-only census of mixed declaration versions.
-- Does not UPDATE, DELETE, or assign declaration_mode (BR-08, H-008).
--
-- psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f docs/ops/rate-card-mixed-declaration.sql
-- Optional tenant: add AND c.tenant_id = '...' to the final WHERE.
--
-- Sample row (no production database was scanned in this change):
-- rate_card_id | rate_card_code | version_number | count_rules | count_matrix_bands | status
-- (empty result means this database has no mixed version)

WITH rule_shape AS (
    SELECT
        v.rate_card_id,
        v.id AS version_id,
        v.version_no,
        v.status,
        COUNT(*) FILTER (WHERE r.calc_method <> 'weight_break_pivot') AS count_rules,
        COUNT(*) FILTER (WHERE r.calc_method = 'weight_break_pivot') AS count_matrix_rules
    FROM rate_versions v
    JOIN pricing_rules r ON r.rate_version_id = v.id AND r.deleted_at IS NULL
    WHERE v.deleted_at IS NULL
    GROUP BY v.rate_card_id, v.id, v.version_no, v.status
),
bands AS (
    SELECT r.rate_version_id, COUNT(b.id) AS count_matrix_bands
    FROM pricing_rules r
    JOIN rate_breaks b ON b.pricing_rule_id = r.id AND b.deleted_at IS NULL
    WHERE r.deleted_at IS NULL
      AND r.calc_method = 'weight_break_pivot'
    GROUP BY r.rate_version_id
)
SELECT
    c.id AS rate_card_id,
    c.code AS rate_card_code,
    s.version_no AS version_number,
    s.count_rules,
    COALESCE(b.count_matrix_bands, 0) AS count_matrix_bands,
    s.status
FROM rule_shape s
JOIN rate_cards c ON c.id = s.rate_card_id AND c.deleted_at IS NULL
LEFT JOIN bands b ON b.rate_version_id = s.version_id
WHERE s.count_rules > 0
  AND s.count_matrix_rules > 0
ORDER BY c.code, s.version_no;
