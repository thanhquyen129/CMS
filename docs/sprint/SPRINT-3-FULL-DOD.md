# Sprint 3 FULL — Definition of Done (Pass 2 Rate & Pricing)

Pass 1 Sprint 3 delivered thin D04 (fixed/unit_rate only; free-form applicability unused; re-rate append without supersedes link).
Pass 2 closes **E04** operable depth: applicability filters, formula types, re-rate history, optional Expected Cost seed on rating.

## Done

| Item | Status | Notes |
|------|--------|-------|
| Applicability fields on `pricing_rules` | Done | `service_type_code`, `party_type_code`, `route_code` (null = any); filtered at rating time |
| Formula `percent_of_base` | Done | `%` of explicit `baseAmount` or running total of prior lines |
| Formula `min_max_clamp` | Done | `unitAmount × qty` clamped to `min_amount` / `max_amount` |
| Keep `fixed` / `unit_rate` | Done | Unchanged Pass 1 math |
| Re-rate `supersedes_rating_id` | Done | New rating links prior; prior → `superseded`; details never overwritten |
| Rating context inputs | Done | `quantity`, `weight`, service/party/route, optional `baseAmount` |
| List rating history | Done | `GET /api/bills/{billId}/ratings` |
| C-011 publish immutability | Done | Rules/components still blocked after publish (VI Conflict) |
| Optional `seedExpectedCosts` | Done | Calls Sprint 4 idempotent seed (`source_type=rating_detail`) |
| Migration | Done | `Sprint3Full_RatePricing` (additive; no rewrite of Pass 1 S3 migration) |
| Vietnamese errors | Done | FluentValidation + AppException |
| Tests | Done | `Sprint3FullRatePricingTests` (applicability, clamp/percent, re-rate, seed, isolation/immutable) |
| DoD | Done | This file |

## APIs (delta)

| Method | Path | Notes |
|--------|------|-------|
| POST | `/api/rate-versions/{id}/rules` | + applicability + min/max + new calc methods |
| POST | `/api/ratings` | + weight/context/`supersedesRatingId`/`seedExpectedCosts`/`baseAmount` |
| GET | `/api/ratings/{id}` | + status, supersedes, context fields |
| GET | `/api/bills/{billId}/ratings` | History newest first |
| POST | `/api/ratings/{id}/seed-expected-costs` | Unchanged Sprint 4 endpoint |

## Deferred / follow-ups

| Item | Target |
|------|--------|
| Full DSL / expression formula language | Later Pass 2 |
| Route master / geo applicability | Later (route stub codes only) |
| Next.js rate card UI | Later |
| Auto-copy rules when creating new version from published | Later |

## Verify

```bash
dotnet test Cms.sln -c Release
```
