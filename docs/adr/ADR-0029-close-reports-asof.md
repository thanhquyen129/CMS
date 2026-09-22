# ADR-0029 — Close gates, late documents, maturity reports, as-of aging

**Status:** Accepted  
**Date:** 2026-09-22  
**Relates:** ADR-0010, ADR-0006, ADR-0028, MASTER 08, CR-02…CR-08, CR-12…CR-14, CR-16, CR-20, CR-23, CR-27, RPT-01, RPT-02, FA-24

## Context

Financial close already blocked on critical exceptions, unmatched documents, and unsettled AP/AR. Package G completes the close checklist and makes reports ask for maturity and as-of instead of mixing “best available” silently.

## Decision

1. Snapshot eligibility also blocks open cost allocations and revenue mappings, and unapplied payment/collection cash above a configurable threshold. Critical exceptions in `waiting` block close until waived. Approved waivers are written into the snapshot as `waiver` / `waiver_count` lines. Prior snapshots stay immutable.
2. A late document whose date falls in a locked period is rejected when the amount exceeds `LateDocumentMaterialThreshold` (default 0). The operator must reopen. Below the threshold the document may still be received; the locked snapshot is never rewritten.
3. Report hub requires an explicit maturity view and optional as-of. Catalogue links only to real screens: profit/revenue report, bills, costs, aging, cash settlement, exceptions, closes.
4. Aging outstanding at as-of sums only settlements finalized on or before that date. Tenant `FinancialJson` may set `agingBucket1Days` / `2` / `3` (defaults 30/60/90) until the policy registry exists.
5. Aging CSV export writes an audit `report.export` event with the same filters as the screen.
6. `GET /api/reports/cash-settlement?asOf=` lists payment and collection unapplied cash for drill-down.

## Consequences

- No new migration (string statuses and JSON settings only).
- Sprint 7 aging assertions treat post-asOf settlement as excluded at a historical as-of.
- Full policy registry remains package H.
