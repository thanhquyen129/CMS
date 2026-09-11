# Sprint 5 — Definition of Done (Revenue & Profitability)

TD6: *Revenue lifecycle; actual recognition; profitability read model* — Epic **E07** (+ thin Bill financial profile).

## Done (this sprint)

| Item | Status | Notes |
|------|--------|-------|
| Full `revenues` TD1 fields | Done | bill_id required; maturity layers Expected/Confirmed/Actual; money+currency; customer_party optional; statuses; effective_date; row_version |
| `revenue_adjustments` | Done | adjustment/reversal rows; no silent overwrite (C-009) |
| Maturity Expected→Confirmed→Actual | Done | preserves prior layer amounts + audit timestamps |
| Single Economic Revenue C-004 | Done | reject `document`/`accounts_receivable` source; idempotent by source_type+source_id |
| Tenant APIs + VI errors | Done | FluentValidation VI + AppException |
| Tenant isolation | Done | Global filter + `X-Tenant-Id` |
| Bill financial profile (derived) | Done | `GET /api/bills/{id}/financial-profile`; Best Available; profit = rev − cost per currency; no SoT totals on Bill |
| Currency safety | Done | totals split by `currency_code`; mixed flagged; no raw cross-currency sum |
| Migration `Sprint5_Revenue` | Done | Alters `revenues`; adds `revenue_adjustments`; does not alter Sprint 0–4 migrations |
| Tests | Done | 3 new; suite green (25) |
| Sprint 5 DoD doc | Done | This file |

## Deferred (later sprints)

| Item | Target | Reason |
|------|--------|--------|
| Documents / AP / AR | Sprint 6–7 | Non-goal |
| Settlement / collection | Later | Non-goal |
| Full approval workflow | Later | `approval_status` stub only |
| Revenue Type master FKs | Later | Codes as strings for thin slice |
| FX / base_amount roll-up | Later | No cross-currency sum without FX |
| JWT / OIDC | Later | Keep header bootstrap |
| Next.js UI / owner dashboard | Later | API-only this sprint |

## Headers (bootstrap until JWT)

| Header | Purpose |
|--------|---------|
| `X-Tenant-Id` | Tenant context (C-001) |
| `X-User-Id` | Actor for confirm/actualize audit |
| `X-Correlation-Id` | Request correlation |

## APIs

| Method | Path | Notes |
|--------|------|-------|
| POST | `/api/revenues` | Create Bill-attributable Expected revenue |
| GET | `/api/revenues` | List (`billId`, `financialMaturity` filters) |
| GET | `/api/revenues/{id}` | Get + adjustments |
| POST | `/api/revenues/{id}/confirm` | Expected → Confirmed (keeps ExpectedAmount) |
| POST | `/api/revenues/{id}/actualize` | Confirmed → Actual (keeps prior layers) |
| POST | `/api/revenues/{id}/adjustments` | Adjustment/reversal history row |
| GET | `/api/bills/{id}/financial-profile` | Derived Cost+Revenue+Profit by currency (Best Available) |

## Verify

```bash
dotnet test Cms.sln -c Release
```
