-- LCMS-CR-RATING-FINANCIAL-DEDUP-01 — MG inventory / dry-run (TEST TENANT ONLY)
-- Replace :tenant_id and :bill_no before execute. Take DB snapshot first (MG-01).

-- MG-02 Rating inventory
SELECT b.bill_no, r.party_type_code, r.status, COUNT(*) AS rating_runs,
       SUM((SELECT COUNT(*) FROM rating_details d WHERE d.rating_id = r.id)) AS details
FROM ratings r
JOIN bills b ON b.id = r.bill_id
WHERE r.tenant_id = :tenant_id AND r.deleted_at IS NULL
GROUP BY b.bill_no, r.party_type_code, r.status
ORDER BY b.bill_no;

-- MG-03 Cost/Revenue by identity candidate
SELECT b.bill_no, c.cost_type_code, c.economic_identity_key, c.financial_maturity,
       c.record_status, c.expected_amount, c.actual_amount, c.source_type
FROM costs c
JOIN bills b ON b.id = c.bill_id
WHERE c.tenant_id = :tenant_id AND b.bill_no = :bill_no AND c.deleted_at IS NULL
ORDER BY c.cost_type_code, c.created_at;

SELECT b.bill_no, r.revenue_type_code, r.economic_identity_key, r.financial_maturity,
       r.record_status, r.expected_amount, r.actual_amount, r.source_type
FROM revenues r
JOIN bills b ON b.id = r.bill_id
WHERE r.tenant_id = :tenant_id AND b.bill_no = :bill_no AND r.deleted_at IS NULL
ORDER BY r.revenue_type_code, r.created_at;

-- MG-05 Dry-run: active duplicates sharing economic_identity_key
SELECT economic_identity_key, COUNT(*) AS active_rows,
       SUM(COALESCE(actual_amount, confirmed_amount, expected_amount)) AS naive_sum
FROM costs
WHERE tenant_id = :tenant_id AND record_status = 'active' AND deleted_at IS NULL
  AND economic_identity_key IS NOT NULL
GROUP BY economic_identity_key
HAVING COUNT(*) > 1;

-- MG-09 Controlled supersede of Expected-only duplicates (TEST ONLY — review dry-run first)
-- UPDATE costs c SET record_status = 'superseded', updated_at = NOW()
-- WHERE c.tenant_id = :tenant_id
--   AND c.record_status = 'active'
--   AND c.actual_amount IS NULL AND c.confirmed_amount IS NULL
--   AND c.financial_maturity = 'expected'
--   AND c.economic_identity_key IN (
--     SELECT economic_identity_key FROM costs
--     WHERE tenant_id = :tenant_id AND record_status = 'active' AND deleted_at IS NULL
--     GROUP BY economic_identity_key HAVING COUNT(*) > 1
--   )
--   AND c.id NOT IN (
--     SELECT DISTINCT ON (economic_identity_key) id FROM costs
--     WHERE tenant_id = :tenant_id AND record_status = 'active' AND deleted_at IS NULL
--     ORDER BY economic_identity_key, created_at DESC
--   );
