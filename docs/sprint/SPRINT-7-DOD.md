# Sprint 7 — Definition of Done (Exposure + AP/AR)

TD6: *Exposure; AP/AR recognition; partial recognition; outstanding* — Epic **E09**.

## Done (this sprint)

| Item | Status | Notes |
|------|--------|-------|
| `payable_exposures` TD1 D08 | Done | Obligation before recognized AP; status open/partial/full |
| `receivable_exposures` | Done | Expected collectible before recognized AR |
| `accounts_payable` | Done | Separate recognition record; IDX-007 |
| `accounts_receivable` | Done | Separate recognition record; IDX-008 |
| Exposure ≠ Recognized AP/AR (CP3/TD4) | Done | Recognize creates new AP/AR row; exposure kept |
| Outstanding derived (C-015) | Done | `recognized + adjustment − finalized_settled`; no user SoT field |
| Partial recognition | Done | Multiple recognize calls; reject over-recognize |
| C-003 / C-004 | Done | Recognize never invents Cost/Revenue |
| Tenant APIs + VI errors | Done | FluentValidation VI + AppException |
| Tenant isolation | Done | Global filter + `X-Tenant-Id` |
| Migration `Sprint7_ExposureApAr` | Done | Adds four tables; does not alter Sprint 0–6 migrations |
| Tests | Done | 3 new; suite green (31) |
| Sprint 7 DoD doc | Done | This file |

## Deferred (later sprints)

| Item | Target | Reason |
|------|--------|--------|
| Payment / Collection settlement | Sprint 8 | Non-goal; `FinalizedSettledAmount` stays 0 |
| Dedicated AP/AR adjustment tables | Later | Pass 1 uses `AdjustmentAmount` on AP/AR |
| Aging UI / reports | Sprint 7 FULL | API aging buckets shipped (`SPRINT-7-FULL-DOD.md`); UI deferred |
| Auto-exposure from documents/costs | Later | Manual create + optional document link (Pass 2 FULL) |
| JWT / OIDC | Later | Keep header bootstrap |
| Next.js UI | Later | API-only this sprint |

## Headers (bootstrap until JWT)

| Header | Purpose |
|--------|---------|
| `X-Tenant-Id` | Tenant context (C-001) |
| `X-User-Id` | Actor for recognize/adjust audit |
| `X-Correlation-Id` | Request correlation |

## APIs

| Method | Path | Notes |
|--------|------|-------|
| POST | `/api/payable-exposures` | Create payable exposure |
| GET | `/api/payable-exposures` | List (status filter) |
| GET | `/api/payable-exposures/{id}` | Get + open amount |
| POST | `/api/payable-exposures/{id}/recognize` | Partial/full → new AP |
| POST | `/api/receivable-exposures` | Create receivable exposure |
| GET | `/api/receivable-exposures` | List (status filter) |
| GET | `/api/receivable-exposures/{id}` | Get + open amount |
| POST | `/api/receivable-exposures/{id}/recognize` | Partial/full → new AR |
| GET | `/api/accounts-payable` | List; Outstanding derived |
| GET | `/api/accounts-payable/{id}` | Get; Outstanding derived |
| POST | `/api/accounts-payable/{id}/adjust` | Adjust; outstanding recomputed |
| GET | `/api/accounts-receivable` | List; Outstanding derived |
| GET | `/api/accounts-receivable/{id}` | Get; Outstanding derived |
| POST | `/api/accounts-receivable/{id}/adjust` | Adjust; outstanding recomputed |

## Verify

```bash
dotnet test Cms.sln -c Release
```
