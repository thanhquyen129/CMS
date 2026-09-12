# ADR-0011: Dashboard base-currency roll-up stub + close snapshot P&L shape

- Status: Accepted
- Date: 2026-09-12
- Relates: C-014; ADR-0004; TD6 E13; Sprint 11 FULL Financial Profile & Reporting

## Context
Pass 1 dashboard kept Best Available totals per `currency_code` only. Controllers need a single-number roll-up for scanning without inventing a market FX feed.
Close snapshots already store immutable metric rows (`revenue_total`, `cost_total`, …). Reporting needs a derived P&L read without writing totals onto Bill (TD1-DB-003/004).

## Decision
1. **Dashboard base roll-up**: optional `BaseCurrencyRollUp` on `GET /api/dashboard/summary` converts Best Available cost/revenue per currency via dated `fx_rates` (asOf = UTC today) with `StubFxRatesToBase` fallback (ADR-0004). Missing rate → validation error (no silent 1:1).
2. **Caveat in payload**: `FxStubNote` states conversion source (dated fx_rates and/or stub fallback); not a live market feed.
3. **Close P&L stub**: `GET /api/financial-closes/{id}/pnl` derives `ProfitTotal = revenue_total − cost_total` from the latest (or selected) immutable snapshot details. Read-only; never mutates snapshot or Bill.
4. **asOf maturity**: reconstruct Confirmed/Actual layers from `ConfirmedAt` / `ActualizedAt` when `?asOf=` is set; residual limits documented on the profile response and DoD.

## Consequences
- Ops must keep Cost/Revenue stub rate maps aligned for dashboard roll-up.
- P&L is snapshot-metric projection only — not a general ledger trial balance.
- Residual asOf limits (adjustments without history timestamps) stay explicit for audit honesty.

## Alternatives rejected
- Summing raw mixed currencies — violates C-014.
- Writing derived P&L onto Bill — violates SoT / TD1-DB-003.
- Inventing dated FX table in Sprint 11 — out of scope; stub is enough for go-live slice.
