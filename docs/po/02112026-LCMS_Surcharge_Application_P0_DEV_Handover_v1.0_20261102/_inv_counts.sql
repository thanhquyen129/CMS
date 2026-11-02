SELECT 'published_versions' AS metric, count(*)::text AS value
FROM surcharge_versions WHERE deleted_at IS NULL AND lower(publish_status) = 'published'
UNION ALL
SELECT 'all_versions', count(*)::text FROM surcharge_versions WHERE deleted_at IS NULL
UNION ALL
SELECT 'surcharge_headers', count(*)::text FROM surcharges WHERE deleted_at IS NULL
UNION ALL
SELECT 'donggoi_versions', count(*)::text
FROM surcharges s JOIN surcharge_versions sv ON sv.surcharge_id = s.id
WHERE s.deleted_at IS NULL AND sv.deleted_at IS NULL AND s.code ILIKE '%DONGGOI%';
