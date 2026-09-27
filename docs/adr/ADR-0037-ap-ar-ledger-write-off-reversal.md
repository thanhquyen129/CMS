# ADR-0037: AR/AP ledger as a derived read model; write-off reversal as a compensating row

- Status: Accepted
- Date: 2026-09-27
- Relates: C-013 (no silent overwrite), C-015 (derived outstanding), C-003/C-004, ADR-0036 (If-Match)
- Trigger: UAT AR Regression 2026-09-27 (AR-LEDGER-01, AR-WO-01, AR-TRACE-02, AR-REF-01, AR-UI-01)

## Context
UAT could not trace the AR chain 280 → 260 → 280 → 250 on HAWB-UAT-001. The money moves were already stored in
immutable rows (AR recognition, `accounts_receivable_adjustments` with before/after, collection allocations with
`finalized_at` / `reversed_at`), but no screen joined them, the only lookup key users had was the manual
reference number, write-off could not be undone, and adjust / allocation-reverse wrote no audit event.

## Decision
1. **Ledger = read model, not a new table.** `GET /api/accounts-{receivable|payable}/{id}/ledger` merges recognition,
   adjustment rows (`adjustment`, `write_off`, `write_off_reversal`, `reverse_recognize`), finalized allocations
   (−settled amount in AR/AP currency) and allocation reversals (+settled amount) ordered by time, with a running
   balance. The response carries `reconciled = (running balance == DeriveOutstanding())`; a mismatch is shown to the
   user, never hidden. Entry id is the internal row id; the collection/payment reference number is metadata only.
2. **Write-off reversal = compensating adjustment row.** `POST /api/accounts-{receivable|payable}/{id}/write-offs/{adjustmentId}/reverse`
   adds `adjustment_type = write_off_reversal` with `reverses_adjustment_id` → original write-off.
   A unique filtered index `(tenant_id, reverses_adjustment_id)` blocks double reversal. The original row is never
   edited or deleted. Revenue / Cost count is guarded unchanged. Permission: `ar.write_off` / `ap.write_off`;
   reason mandatory; If-Match per ADR-0036; audit `accounts_*.write_off_reverse`.
   Reversal is applied immediately (no approval): it restores the claim/obligation, which is the conservative direction.
3. **Bill history** = `GET /api/bills/{id}/financial-history` (AR ledgers need `revenue.read`, AP ledgers need
   `cost.read`, each with its own data scope). Replaces the Bill audit tab that listened for an object type nobody wrote.
4. Adjust AR/AP and allocation reverse now append audit events; allocation reverse subtracts
   `SettledAmount ?? Amount` (AR/AP currency), not the cash amount. Allocation `currency_code` is stamped from the
   cash document (migration back-fills existing rows).

## Consequences
- No dual-write ledger that can drift; the ledger is only as good as the source rows, and drift is visible via `reconciled`.
- Historical cross-currency allocations reversed before this fix may show `reconciled = false`; fix by controlled data correction, not by silently recomputing.
- Write-off approval threshold stays tenant policy on the server (`MaxWriteOffAmount` + approval matrix); the UI no longer hard-codes it.

## Alternatives rejected
- Physical `ar_ledger_entries` table written on every command: second source of truth, needs back-fill and dual-write discipline for every future money command.
- "Adjustment +30" to undo a write-off: loses the link to the original write-off and misstates the reason trail.
