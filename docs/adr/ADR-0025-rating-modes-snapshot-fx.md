# ADR-0025 — Six rating modes, immutable snapshot, chargeable weight, and buy/sell permissions

**Status:** Accepted  
**Date:** 2026-09-22  
**Relates:** ADR-0024, MASTER 03, RT-01…RT-18, VER-01, VER-03, H-003

## Context

Sprint 3 already rates a published version by summing every matching rule (`fixed`, `unit_rate`, `percent_of_base`, `min_max_clamp`). Package C adds air/sea chargeable weight, six selection modes, FX on the rating run, and separate buy/sell permissions. A later edit of operational data must not rewrite a rating already stored.

## Decision

1. New calc methods sit beside the four existing ones: `weight_break_pivot`, `weight_step`, `container_rate`, `composite`. Rules with no charge code still each apply. Rules that share a charge code compete; the most specific wins. A tie is `AMBIGUOUS_RATE_RULE`. No match is `NO_APPLICABLE_RATE` and is not stored as zero.
2. Chargeable weight is `max(gross, volume × factor)` for air (default factor 167) and `max(gross kg / 1000, CBM)` for sea, then ceiled to the rounding step. An explicit quantity that differs from a confirmed `chargeable_weight_kg` needs `rate.quantity.override` and a reason.
3. Weight-break bands are rows. Pivot stores quantity × band price, then applies the minimum charge. Weight-step uses the band amount as a flat price when bands exist, and does not multiply again.
4. Composite components run in dependency order. A cycle is rejected. Container rates sum quantity × price per container type.
5. Only a published version whose effective window contains the rate date can be used. The rating row stores `context_json`, chargeable basis, original amount, FX source/date/rate, and the rounded amount. A later context edit does not update that JSON. Re-rate marks the prior row superseded and does not change its lines. Same currency keeps `total_amount` in the card currency so existing totals stay stable.
6. `POST /api/ratings/compare` quotes published cards in memory and does not insert a rating. `POST /api/rate-imports/preview` blocks duplicate card codes and overlapping breaks; commit writes nothing unless every row is valid, and the new version stays draft.
7. Buy and sell cards have separate read/write/publish permissions. Quantity override and re-rate are their own codes. A request with no user still follows the existing bootstrap allowance so current header-only tests keep working.

## Consequences

- Migration `RatingModesChargeableFx` adds the rating, rule, and rate-card columns plus `rate_breaks` and `container_rates`.
- UI-03 screens live under Bảng giá & Tính giá. Giá mua maps to vendor; giá bán maps to customer.
- Draft and expired versions cannot be selected for a rating on that date.
