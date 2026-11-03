# SQL Inventory, Cleanup & Reconciliation (MG-01 → MG-16)

**Environment:** Local / Staging test tenant only · **Production:** không chạy cleanup  
**Script:** `sql/reconcile_rating_financial_dedup.sql`

## MG checklist

| Mã | Việc | DEV |
|----|------|-----|
| MG-01 | Snapshot DB trước dọn — bắt buộc ops trước UPDATE | READY (runbook) |
| MG-02 | Inventory Rating BUY/SELL | Script sẵn |
| MG-03 | Inventory Cost/Revenue theo identity/maturity/source | Script sẵn |
| MG-04 | Document/AP/AR — không xóa trong batch | Giữ nguyên |
| MG-05/06 | Dry-run trùng identity | Script HAVING COUNT>1 |
| MG-07 | Confirm test tenant | Ops |
| MG-08 | Schema identity columns + index | Migration COMPLETED |
| MG-09 | Supersede Expected trùng (commented UPDATE) | READY |
| MG-10/11 | Dataset UAT (LSSTEST… / multi-leg / partial) | Logic + selector fixture |
| MG-12 | Rowcount before/after | Ghi khi chạy Staging |
| MG-13…16 | Post-check / rollback plan | Snapshot restore |

## Đối soát công thức Bill mẫu (điều kiện)

Khi 2 FREIGHT BUY cùng identity (1 Actual + 1 Expected lặp) và 1 DONGGOI BUY hiệu lực; SELL FREIGHT Actual 2.040.000 + DONGGOI Expected 400.000 đã activation:

| Chỉ tiêu | Giá trị |
|----------|---------|
| Effective Cost | 1.740.000 VND |
| Effective Revenue | 2.440.000 VND |
| Best Available Profit | 700.000 VND |

Chứng minh selector: `RatingFinancialDedupP0Tests.TC30_BillSample_EffectiveCost_1740000_WhenTwoObligations`.

## Rollback

1. Restore snapshot MG-01  
2. `dotnet ef database update <previous>` hoặc migration `Down`  
3. Revert git commit bàn giao
