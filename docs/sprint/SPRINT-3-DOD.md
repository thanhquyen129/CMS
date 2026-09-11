# Sprint 3 — Definition of Done (Rate & Pricing)

TD6: *Rate card/version/rules/components; rating; snapshot; rerating* — Epic **E04** thinnest operable slice.

## Done (this sprint)

| Item | Status | Notes |
|------|--------|-------|
| D04 entities + migration | Done | `rate_cards`, `rate_versions`, `pricing_rules`, `pricing_rule_components`, `ratings`, `rating_details` |
| UUIDv7 / tenant_id / soft-delete / row_version | Done | Via `TenantEntityBase` + global filters |
| Published `rate_version` immutable (C-011) | Done | Rules/components blocked after publish; create new version |
| Rate card CRUD/list | Done | `POST/GET/PUT/DELETE /api/rate-cards` |
| Draft → publish version | Done | `POST .../versions`, `POST /api/rate-versions/{id}/publish` |
| Rules/components on draft only | Done | Conflict VI if published |
| `POST /api/ratings` snapshot | Done | Expected seed: `fixed` or `unit_rate × quantity` → `rating_details` |
| Vietnamese validation/errors | Done | FluentValidation VI + AppException |
| Tenant isolation | Done | Global filter + `X-Tenant-Id` |
| Migration `Sprint3_RatePricing` | Done | Does not alter Sprint 0–2 migrations |
| Tests: immutable / isolation / snapshot | Done | 3 new; suite green (19) |
| Sprint 3 DoD doc | Done | This file |

## Deferred (later sprints)

| Item | Target | Reason |
|------|--------|--------|
| Full formula / expression engine | Later | Non-goal this sprint |
| Cost Expected lifecycle from rating | Sprint 4 | M05 Cost Management |
| Cost Type / Revenue Type master FKs | Later | Codes as strings for thin slice |
| JWT / OIDC | Later | Keep header bootstrap |
| Next.js UI | Later | API-only this sprint |

## Headers (bootstrap until JWT)

| Header | Purpose |
|--------|---------|
| `X-Tenant-Id` | Tenant context (C-001) |
| `X-User-Id` | Actor for permission checks (optional) |
| `X-Correlation-Id` | Request correlation |

## APIs

| Method | Path | Notes |
|--------|------|-------|
| POST | `/api/rate-cards` | Create rate card |
| GET | `/api/rate-cards` | List |
| GET | `/api/rate-cards/{id}` | Get |
| PUT | `/api/rate-cards/{id}` | Update metadata |
| DELETE | `/api/rate-cards/{id}` | Soft-delete |
| POST | `/api/rate-cards/{id}/versions` | Create draft version (auto `version_no`) |
| GET | `/api/rate-cards/{id}/versions` | List versions |
| GET | `/api/rate-versions/{id}` | Get version |
| POST | `/api/rate-versions/{id}/publish` | Publish (immutable thereafter) |
| POST | `/api/rate-versions/{id}/rules` | Add rule (draft only) |
| GET | `/api/rate-versions/{id}/rules` | List rules |
| POST | `/api/pricing-rules/{id}/components` | Add component (draft only) |
| POST | `/api/ratings` | Rate Bill against published version → snapshot |
| GET | `/api/ratings/{id}` | Get rating + details |

## Verify

```bash
dotnet test Cms.sln -c Release
```
