SELECT table_name, column_name, data_type
FROM information_schema.columns
WHERE table_name IN ('surcharge_conditions','surcharge_rules','surcharges','surcharge_versions')
ORDER BY table_name, ordinal_position;
