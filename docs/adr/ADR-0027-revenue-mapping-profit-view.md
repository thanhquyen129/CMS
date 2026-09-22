# ADR-0027 — One economic revenue, many Bills, profit by one maturity

**Status:** Accepted  
**Date:** 2026-09-22  
**Relates:** ADR-0026, MASTER 05, RV-06, RV-10…RV-18, RV-FINAL-01…06, C-004, C-014

## Context

Sprint 5 stores one revenue on one Bill and shows profit per currency. Package E splits that economic revenue across Bills and stops mixing maturity layers or currencies into one total.

## Decision

1. `revenue_mappings` plus details split one revenue across at least two Bills. The same remainder rule as cost allocation keeps the lines equal to the source amount. Finalize is immutable. A later split supersedes the previous finalized row. The parent amount is not added again on the anchor Bill.
2. A finalized mapping counts only in the maturity that was locked, and in the best view. Expected and Actual are not added together.
3. Revenue of 0 makes the margin null (shown as N/A). Gross profit is still returned, including a loss.
4. A reporting currency converts each bucket with a stored FX rate and a trace (source, date, rate, original, converted). A missing rate leaves the reporting total empty. USD and VND are not added raw. Same currency uses rate 1, source `identity`.
5. Group totals are by customer, service, mode, route, or movement, and by currency. A movement total is the sum of Bill profit on that movement, not a separate movement ledger.
6. If another system owns `actual_revenue`, LCMS actualize is rejected with `RV-06` unless a reason is stored. The prior actual amount is not overwritten in silence.

## Consequences

- Migration `RevenueMappingProfitability` creates `revenue_mappings` and `revenue_mapping_details`.
- `GET /api/bills/{id}/profitability` accepts `reportingCurrency` and returns `marginRate`.
- `GET /api/profitability/bills` and `/groups` feed the revenue list and report.
- Permission `revenue.mapping.override` is required to replace an automatic split line.
- Đối soát doanh thu is not a nav item until that flow exists.
