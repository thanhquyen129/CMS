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
3. **Write-off stub**: `POST …/accounts-payable|accounts-receivable/{id}/write-off` with `{ amount, reason }`. Caps at `Settlement:MaxWriteOffAmount` (default 1000 txn currency). Applies **negative** `AdjustmentAmount` + appends `[xóa nợ …]` note — never increases `FinalizedSettledAmount`, never invents Cost/Revenue.
4. **Idempotent finalize**: re-finalize of already-finalized allocation is safe no-op; reversed → 409 VI.

## Consequences
- Outstanding remains derived (C-015). Write-off is an honest adjustment trail, not fake cash.
- Real FX table can replace stub without changing Payment/Collection API shape.
- Larger write-offs / policy matrix deferred (approval queue in Sprint 9).

## Alternatives rejected
- Silent outstanding wipe on settle — hides remainder, breaks audit.
- Counting write-off as finalized settlement cash — conflates Cost≠Payment / Revenue≠Collection.
- Shared Cost/Revenue config only — Settlement ops need independent max write-off + rates.
