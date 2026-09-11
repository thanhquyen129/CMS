# Sprint 11 — Definition of Done (Financial Profile & Reporting)

TD6: *Bill financial profile; dashboard; control queues; reporting projections* — Epic **E13**.

## Done (this sprint)

| Item | Status | Notes |
|------|--------|-------|
| Enhance `GET /api/bills/{id}/financial-profile` | Done | Maturity breakdown; allocated cost; AP/AR settlement outstanding by currency; `asOfTimestamp` |
| Optional `?asOf=` projection stub | Done | Filters Cost/Revenue `EffectiveDate`, allocation `FinalizedAt`, AP/AR `RecognizedAt`; limitation note when set |
| `GET /api/dashboard/summary` | Done | Counts: bills, open exceptions, pending approvals, open closes; Best Available totals by currency |
| Control queues | Done | `GET /api/queues/exceptions` (open only); `GET /api/queues/approvals` (pending only) |
| Derived read only | Done | No SoT derived totals written onto Bill entity |
| Vietnamese labels | Done | Dashboard/queue/profile notes via `VietnameseUiTerms` |
| Tenant isolation | Done | Global filter + `X-Tenant-Id`; cross-tenant profile/dashboard/queues covered |
| Tests | Done | 3 new; suite green (43) |
| Sprint 11 DoD doc | Done | This file |

## Deferred (later sprints / Pass 2)

| Item | Target | Reason |
|------|--------|--------|
| Full historical maturity reconstruction at `asOf` | Pass 2 | Thin filter by EffectiveDate/FinalizedAt/RecognizedAt only |
| FX / base currency roll-up | Later | Totals stay per `currency_code` |
| Next.js dashboard / queue UI | Later | API-only Pass 1 |
| Hardening / NFR / load | Sprint 12 | Non-goal |
| JWT / OIDC | Later | Keep header bootstrap |
| P&L from financial-close snapshots | Later | Live Best Available projection only |

## Headers (bootstrap until JWT)

| Header | Purpose |
|--------|---------|
| `X-Tenant-Id` | Tenant context (C-001) |
| `X-User-Id` | Actor (approvals/closes when needed) |
| `X-Correlation-Id` | Request correlation |

## APIs

| Method | Path | Notes |
|--------|------|-------|
| GET | `/api/bills/{id}/financial-profile` | Enhanced derived profile; optional `?asOf=yyyy-MM-dd` |
| GET | `/api/dashboard/summary` | Tenant dashboard counts + Best Available totals |
| GET | `/api/queues/exceptions` | Open exception control queue (`severity` optional) |
| GET | `/api/queues/approvals` | Pending approval control queue (`objectType` optional) |

## Verify

```bash
dotnet test Cms.sln -c Release
```
