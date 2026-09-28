# ADR-0038 — AR/AP balance reconciliation for legacy cross-currency reversals (FIN-DATA-01)

- Status: Accepted
- Date: 2026-09-28
- Related: ADR-0037 (AR/AP ledger + write-off reversal), UAT AR Regression follow-up 2026-09-28 (FIN-DATA-01)

## Context

Before the 2026-09-27 fix, reversing a *finalized* allocation subtracted the cash amount (`Amount`, cash currency)
from `FinalizedSettledAmount` instead of the settled amount (`SettledAmount`, AR/AP currency), clamped at 0.
For cross-currency allocations this left the stored balance different from the ledger derived from source rows.
ADR-0037 surfaces this as `reconciled = false`; PO requires inventory, comparison and an audited correction —
no silent rewrite, no untracked DB update.

## Decision

1. **Inventory** `GET /api/ap-ar/balance-reconciliation`: per tenant, re-derive
   `adjustment = Σ adjustment rows` and `settled = Σ (SettledAmount ?? Amount)` of finalized, non-reversed allocations,
   and list every AR/AP whose stored aggregates differ. Each row carries current vs ledger balance, stored vs derived
   totals, and the reversed cross-currency allocations that explain it (cash amount/currency vs settled amount).
2. **Cause classification**: `legacy_cross_currency_reversal` only when adjustments match, the settled total differs,
   and at least one reversed finalized allocation is cross-currency (cash currency ≠ AR/AP currency or settled ≠ cash).
   Everything else is `unexplained` and is **never** auto-corrected.
3. **Correction** `POST /api/accounts-{receivable|payable}/{id}/settlement-correction {reason}` + If-Match:
   sets `FinalizedSettledAmount` to the derived total, recomputes settlement status, appends a note, and writes audit
   `accounts_*.settlement_correction` with before/after, ledger balance and the legacy allocation list.
   Allocations, cash documents, adjustment rows and close snapshots are untouched. 409 when already reconciled
   or unexplained.
4. **Permission** new action `apar.reconcile` (Admin, FinancialController). AR rows additionally need `revenue.read`,
   AP rows `cost.read`, both with data scope (Cost ≠ Revenue; out-of-scope ⇒ 404 on correction).

## Consequences

- Current balance becomes equal to the ledger; history of the correction is in the audit trail and on the
  reconciliation page ("Lịch sử đối soát").
- The inventory scans AR/AP, adjustments and finalized allocations of the tenant in memory (SQLite cannot sum
  decimals in SQL). Acceptable for an on-demand controller report; move to SQL aggregation on Postgres if tenants
  grow beyond ~100k open items. Route is on the money-path rate limit.
- Rollback: the audit before-JSON holds the previous `finalizedSettled`; a reverse correction is a controlled manual
  step (never automatic).
