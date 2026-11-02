SELECT t.code AS tenant,
       s.code AS surcharge_code,
       s.name,
       s.direction,
       sv.version_no,
       sv.publish_status,
       sv.valid_from::text AS valid_from,
       sv.valid_to::text AS valid_to,
       coalesce(sr.calculation_mode,'') AS calculation_mode,
       coalesce(sr.basis,'') AS basis,
       coalesce(sr.currency_code,'') AS currency_code,
       sr.rate_amount_percent,
       coalesce((
         SELECT string_agg(sc.dimension || ':' || sc.operator || ':' || coalesce(sc.value_text, sc.value_from::text, ''), '; ' ORDER BY sc.dimension)
         FROM surcharge_conditions sc
         WHERE sc.surcharge_rule_id = sr.id AND sc.deleted_at IS NULL
       ), '') AS conditions,
       coalesce((
         SELECT string_agg(distinct coalesce(ss.route_code,''), ',' ORDER BY coalesce(ss.route_code,''))
         FROM surcharge_scopes ss
         WHERE ss.surcharge_rule_id = sr.id AND ss.deleted_at IS NULL
       ), '') AS route_scope,
       coalesce(sv.vat_rate::text,'') AS vat_rate
FROM surcharges s
JOIN tenants t ON t.id = s.tenant_id AND t.deleted_at IS NULL
JOIN surcharge_versions sv ON sv.surcharge_id = s.id AND sv.deleted_at IS NULL
LEFT JOIN surcharge_rules sr ON sr.surcharge_version_id = sv.id AND sr.deleted_at IS NULL
WHERE s.deleted_at IS NULL
ORDER BY t.code, s.code, sv.version_no;
