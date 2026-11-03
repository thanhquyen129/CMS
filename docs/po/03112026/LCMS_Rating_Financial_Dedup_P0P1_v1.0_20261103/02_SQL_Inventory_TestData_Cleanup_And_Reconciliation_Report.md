# SQL Inventory, Cleanup & Reconciliation (MG-01 → MG-16) — Remaster

**Environment:** Local SQLite `src/LCMS.Api/lcms_dev.db` (test tenant) · **Production:** không chạy cleanup  
**Scripts:** `sql/run_mg_inventory.py`, `sql/seed_lsstest_bill.py`, `sql/verify_lsstest_effective.py`

## Kết quả chạy thật (rowcounts)

Nguồn: `sql/MG_inventory_result.json` + `sql/MG01_backup_manifest.json` + `sql/LSSTEST_effective_verify.json`

| Mã | Việc | Số dòng / bằng chứng |
|----|------|----------------------|
| MG-01 | Backup + SHA-256 | `sha256=140fc9fd0b65dc97…`; bytes≈5.394.432; file `sql/lcms_dev_backup_*.db` |
| MG-02 | Inventory Rating | ratings=16; rating_details=17 |
| MG-03 | Inventory Cost/Revenue | costs_active=130; revenues_active=127; costs_superseded≥1 |
| MG-04 | Document/AP/AR | docs=248; AP=124; AR=123 (không xóa) |
| MG-05 | Dup Expected-only | dup_expected_groups=0 sau seed/cleanup; maturity_mix Actual+Expected giữ cho TC-30 |
| MG-06 | Dry-run mapping | backfill `economic_identity_key` khi null |
| MG-07 | Confirm test tenant | local `lcms_dev.db` |
| MG-08 | Schema | `economic_identity_key` / `service_occurrence_key` / `applied_from_rating_id` = true |
| MG-09 | Supersede Expected trùng | affected_rows=0 trên pass cuối (đã sạch Expected-only); DONGGOI seed tự supersede 1 dòng |
| MG-10 | Seed LSSTEST02112026002 | 4 cost + 2 revenue; verify Effective Cost=1740000 Revenue=2440000 Profit=700000 |
| MG-11 | TESTLSS002 | có trong `sample_bills` |
| MG-12 | After | costs_active=130; dup_expected=0 |
| MG-13 | Post-check | `LSSTEST_effective_verify.json` assert_* = true |
| MG-14 | Rollback | restore MG-01 backup file |
| MG-15 | Idempotent re-run | affected=0 |
| MG-16 | Handover package | Excel/MD/screenshots cập nhật |

## SELECT đối soát LSSTEST02112026002

```sql
SELECT cost_type_code, financial_maturity, record_status, expected_amount, actual_amount, economic_identity_key
FROM costs c JOIN bills b ON b.id=c.bill_id
WHERE b.bill_no='LSSTEST02112026002' ORDER BY cost_type_code, created_at;
```

Kết quả thực tế (rút gọn): FREIGHT actual 1.440.000 + FREIGHT expected 1.440.000 (cùng key, Actual thắng) + DONGGOI expected 300.000 active + 1 superseded → **Effective Cost 1.740.000**.  
Revenue: FREIGHT actual 2.040.000 + DONGGOI expected 400.000 → **Effective Revenue 2.440.000** · **Profit 700.000**.

## Rollback

1. Restore `sql/lcms_dev_backup_*.db` (MG-01)  
2. Revert git commit remaster  
3. Không chạy cleanup trên Production
