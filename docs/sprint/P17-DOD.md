# P17 — Dated adjustments cho asOf profile DoD

**Ngày:** 2026-09-13 · E13

## Done
1. `GetBillFinancialProfileQuery`: `AdjustmentAtAsOf` từ `accounts_payable_adjustments` / `accounts_receivable_adjustments` (`EffectiveDate ≤ asOf`).
2. Outstanding settlement bucket dùng adjustment dated khi có `?asOf=`.

## Verify
- Build + regression SprintP10 reverse/adjust paths.

## Non-goals
- Cost/Revenue maturity adjustment replay (chỉ AP/AR ledger dated).
