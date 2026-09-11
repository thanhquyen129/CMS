# Sprint 4 — Definition of Done (Cost)

TD6: *Cost lifecycle; actualization; adjustment; allocation* — Epics **E05, E06** thinnest operable slice.

## Done (this sprint)

| Item | Status | Notes |
|------|--------|-------|
| Full `costs` TD1 fields | Done | maturity, attribution, bill_id nullable (shared), money+currency, statuses, effective_date, row_version; layer amounts Expected/Confirmed/Actual |
| `cost_adjustments` | Done | adjustment/reversal rows; no silent overwrite (C-009/C-013) |
| `cost_allocations` + `cost_allocation_details` | Done | draft → finalize; versioned; supersede prior finalized |
| Finalize conservation C-005 | Done | SUM(details)=allocatable after rounding (last-line residual) |
| Finalize basis C-006 | Done | reject basis ≤ 0 / missing |
| Maturity Expected→Confirmed→Actual | Done | preserves prior layer amounts + audit timestamps |
| Seed Expected from Rating | Done | idempotent via `source_type=rating_detail` + `source_id` |
| Single Economic Cost C-003 | Done | allocation does not create new Cost rows |
| Tenant APIs + VI errors | Done | FluentValidation VI + AppException |
| Tenant isolation | Done | Global filter + `X-Tenant-Id` |
| Migration `Sprint4_Cost` | Done | Alters `costs`; adds adjustment/allocation tables; does not alter Sprint 0–3 migrations |
| Tests | Done | 3 new; suite green (22) |
| Sprint 4 DoD doc | Done | This file |

## Deferred (later sprints)

| Item | Target | Reason |
|------|--------|--------|
| Revenue lifecycle | Sprint 5 | Non-goal this sprint |
| Documents / AP / AR | Later | Non-goal |
| Full approval workflow | Later | `approval_status` stub only |
| Cost Type / Category master FKs | Later | Codes as strings for thin slice |
| JWT / OIDC | Later | Keep header bootstrap |
| Next.js UI | Later | API-only this sprint |

## Headers (bootstrap until JWT)

| Header | Purpose |
|--------|---------|
| `X-Tenant-Id` | Tenant context (C-001) |
| `X-User-Id` | Actor for confirm/actualize/finalize audit |
| `X-Correlation-Id` | Request correlation |

## APIs

| Method | Path | Notes |
|--------|------|-------|
| POST | `/api/costs` | Create direct (bill required) or shared (bill null) |
| GET | `/api/costs` | List (`billId`, `financialMaturity` filters) |
| GET | `/api/costs/{id}` | Get + adjustments + allocations |
| POST | `/api/costs/{id}/confirm` | Expected → Confirmed (keeps ExpectedAmount) |
| POST | `/api/costs/{id}/actualize` | Confirmed → Actual (keeps prior layers) |
| POST | `/api/costs/{id}/adjustments` | Adjustment/reversal history row |
| POST | `/api/costs/{id}/allocations` | Draft allocation + details (shared only) |
| POST | `/api/cost-allocations/{id}/finalize` | C-005/C-006 finalize |
| POST | `/api/ratings/{id}/seed-expected-costs` | Idempotent Expected Cost seed |

## Verify

```bash
dotnet test Cms.sln -c Release
```
