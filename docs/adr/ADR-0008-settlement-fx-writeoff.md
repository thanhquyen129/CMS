# ADR-0008: Settlement FX stub + small remainder write-off

- Status: Accepted
- Date: 2026-09-12
- Relates: TD1 D09 money fields; C-008; C-014; C-015; ADR-0004; Sprint 8 FULL Settlement (Pass 2)

## Context
Pass 1 Settlement stored cash amounts without `base_amount`. Cross-currency reporting must not sum raw payment/collection amounts (C-014). Full FX table is later.
Small AP/AR remainders after partial settle are common (bank rounding). Silently zeroing outstanding or faking cash settlement would break audit (AC-007 / C-015).

## Decision
1. **FX stub** (`Settlement` options): `BaseCurrency` (default `VND`) + `StubFxRatesToBase`. On create payment/collection and allocate, set `BaseAmount`; `FxRateId` stays null until real `fx_rates` (same pattern ADR-0004).
2. Missing stub rate → VI validation (no silent 1:1).
3. **Write-off**: `POST …/accounts-payable|accounts-receivable/{id}/write-off` with `{ amount, reason }`.
   - Amount ≤ `Settlement:MaxWriteOffAmount` (default 1000 txn currency) → apply **immediately** (negative `AdjustmentAmount` + `[xóa nợ …]` note).
   - Amount > threshold → **Approval gate (P03)**: create pending `approvals` (`objectType=accounts_payable|accounts_receivable`, payload in `Notes`); return **202** `{ requiresApproval, approvalId }`. Outstanding unchanged until final approve, then apply same write-off path.
   - Never increases `FinalizedSettledAmount`, never invents Cost/Revenue.
4. **Idempotent finalize**: re-finalize of already-finalized allocation is safe no-op; reversed → 409 VI.

## Consequences
- Outstanding remains derived (C-015). Write-off is an honest adjustment trail, not fake cash.
- Real FX table can replace stub without changing Payment/Collection API shape.
- Over-threshold write-offs are auditable via Approval queue (Permission ≠ Approval).
- Approver matrix / multi-level policy → P12.

## Alternatives rejected
- Silent outstanding wipe on settle — hides remainder, breaks audit.
- Counting write-off as finalized settlement cash — conflates Cost≠Payment / Revenue≠Collection.
- Shared Cost/Revenue config only — Settlement ops need independent max write-off + rates.
