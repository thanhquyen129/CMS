SELECT b.bill_no, r.id::text AS rating_id, r.total_amount, r.status, r.rated_at::text,
       rd.component_code, rd.amount, rd.source_type, rd.financial_maturity
FROM bills b
JOIN ratings r ON r.bill_id = b.id AND r.deleted_at IS NULL
LEFT JOIN rating_details rd ON rd.rating_id = r.id AND rd.deleted_at IS NULL
WHERE b.bill_no = 'TESTLSS002' AND b.deleted_at IS NULL
ORDER BY r.rated_at, rd.component_code;
