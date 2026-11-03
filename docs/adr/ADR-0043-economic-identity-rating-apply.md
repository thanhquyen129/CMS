# ADR-0043 — Economic identity and Apply Rating vs Calculate

**Status:** Accepted  
**Date:** 2026-11-03  
**Relates:** ADR-0025, ADR-0041, LCMS-CR-RATING-FINANCIAL-DEDUP-01

## Context

Re-rating and seeding by `rating_detail` id created extra Cost/Revenue rows for the same obligation. Bill totals summed every active row, so Expected + Actual of one freight (or two Expected seeds) inflated Cost and Profit.

## Decision

1. `CreateRating` / calculate stays comparison-only. It does not seed Cost/Revenue unless an explicit apply path runs.
2. Each Cost/Revenue stores `EconomicIdentityKey` (tenant, bill/leg/shipment anchor, BUY|SELL, charge code, counterparty, service occurrence). Matching uses that key, not amount equality.
3. `ApplyRatingToFinancials` (and seed-from-rating) is atomic, idempotent, BUY→Cost / SELL→Revenue only, and seeds only `applied` (or legacy null-bucket) rating details.
4. One active Expected revision per identity: older Expected rows become `superseded`. Confirmed/Actual amounts are never overwritten by apply; Expected may update for variance on the same row.
5. `EffectiveFactSelector` picks Best Available per identity (`Actual > Confirmed > Expected`) and is the single source for Bill financial profile, profitability, and export totals.

## Consequences

- Legacy duplicate active rows need reconcile/backfill of identity keys then supersede of shadowed Expected.
- UI “Tính thử” must not auto-seed; “Áp dụng vào hồ sơ tài chính” calls Apply with confirmation.
