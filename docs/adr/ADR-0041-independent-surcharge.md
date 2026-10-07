# ADR-0041 — Independent surcharge beside the rate card

**Status:** Accepted  
**Date:** 2026-10-07  
**Relates:** ADR-0025, SUR-01…SUR-15

## Context

Surcharges were pricing-rule components inside a rate version. A shared charge (fuel, security, handling) had to be copied onto every card, and changing it meant a new rate version. Published rate versions and historical ratings must stay immutable.

## Decision

1. `surcharges` and `surcharge_versions` are tenant-scoped pricing objects. A version has its own validity window. Published versions are not updated; the next change is a new version.
2. Applicability lives on `surcharge_conditions` and optional `surcharge_scopes`. An empty field does not filter. A rate card or rate version link is optional.
3. Direction is `buy`, `sell`, or `both`. `both` is accepted only when the active `RATING_POLICY` body sets `allowBothDirection` to true. A rating run is against one card: buy writes an expected-cost line, sell writes an expected-revenue line.
4. The rating engine still prices the published rate version first, then adds every applicable published surcharge at the rate date. The same surcharge code contributes one line: the more specific rule wins, then the higher priority. Equal specificity and priority is `AMBIGUOUS_SURCHARGE` and stores nothing. A later version with a later `valid_from` wins when both are effective. Re-rate inserts a new rating and does not rewrite the previous lines.
5. Each surcharge line stores source id, version id, original amount, reporting amount, and the FX snapshot when the charge currency differs. Expected cost or revenue is seeded only from those lines. This change does not create actual cost or revenue.
6. Legacy components stay on the published rate version. Migration copies only draft components classified as surcharge, records `source_legacy_component_id`, and is safe to run again. Base freight and unclassified components are not copied.

## Consequences

- A published card that still embeds a surcharge component will keep charging it, and an independent surcharge with the same code is additional. Removing the embedded charge requires a new rate version.
- Migration `IndependentSurcharge` adds the surcharge tables and rating-detail source columns.
