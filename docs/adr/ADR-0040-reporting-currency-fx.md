# ADR-0040 — Tenant reporting currency and FX snapshot

- Status: Accepted
- Date: 2026-09-29
- Related: `docs/po/29092026/LCMS_Reporting_Currency_FX_Architecture_Functional_Specification_v1.0` (FX-ARCH-01..12). Realized/unrealized FX gain-loss posting is deferred; the allocation stores the difference only.

## Context

Money was stored in the original currency and converted again at read time with today's rate or a silent stub. Totals could mix currencies or drop a currency without saying the total was incomplete. The reporting currency was a config default (`VND`), not a tenant setting.

## Decision

1. **Reporting currency is `Tenant.DefaultCurrencyCode`.** One value per tenant. Changing it is rejected once any cost, revenue, payment or collection has an FX snapshot.
2. **Each money record keeps the original amount and currency, plus a snapshot:** rate, source (`identity`, `auto_provider`, `manual`, `override`, `policy`), source name, rate date, book id, reason, actor, time, status (`converted`, `missing`, `requires_review`). `BaseAmount` is the reporting amount. A later book or provider change does not rewrite the snapshot.
3. **Same currency → rate 1, no rate input.** A different currency uses the dated book (inverse pair allowed) or, outside production, the traced config stub (`policy`). A missing rate rejects the write. It is never 0 and never 1.
4. **Manual entry and override need `fx.override`.** Override of a known rate needs a reason unless the tenant turns that off. Both write audit `fx.manual` / `fx.override`. Editing a book rate inserts a new version; it does not overwrite the row a snapshot points at.
5. **Aggregates sum reporting amounts only.** A row without a snapshot is counted as missing and left out. Profit is omitted while any included row is missing. Bill overview, dashboard, profitability, aging, cash and close snapshots use this. Close still keeps the original-currency totals and adds reporting metrics; a locked snapshot is not recalculated.
6. **AP/AR copy the source snapshot at recognition.** Settlement stays in the original currency. The allocation stores cash reporting amount, open-item reporting amount, and the difference. Allocation shares use largest-remainder so they sum to the source reporting amount.
7. **Migration is idempotent.** Same currency → identity. A linked `fx_rate_id` is copied. Anything else is `requires_review`, never filled from today's rate.

## Consequences

- Historical rows without a book rate stay out of reporting totals until someone enters a rate.
- Dashboard month series is in the reporting currency, including mixed-currency tenants.
- Gain/loss is trace only. Posting it to a ledger is a later decision.
