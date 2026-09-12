# Sprint 11 FULL — Definition of Done (Pass 2 Financial Profile & Reporting)

Pass 1 Sprint 11 delivered enhanced financial-profile, dashboard counts/totals, and open/pending queues.
Pass 2 closes TD6 E13 depth: asOf maturity reconstruction, FX/base roll-up stub, richer queue filters, close snapshot P&L.

## Done

| Item | Status | Notes |
|------|--------|-------|
| asOf maturity reconstruct | Done | ConfirmedAt / ActualizedAt gate Confirmed & Actual layers; Best Available uses projected layers |
| Allocated cost + settlement outstanding | Done | Allocation FinalizedAt; AP/AR outstanding via RecognizedAt + finalized allocations at asOf |
| Residual asOf limits documented | Done | AdjustmentAmount live-only; missing layer timestamps treated as present; note on DTO + this DoD |
| Dashboard Best Available + base FX roll-up stub | Done | `BaseCurrencyRollUp` via Cost/Revenue StubFxRatesToBase; ADR-0011 |
| Counts: open variances + overdue exceptions | Done | `OpenVarianceCount`, `OverdueExceptionCount` |
| Exception queue filters | Done | status / severity / overdueOnly / objectType |
| Approval queue filters | Done | status / objectType / requiredLevel |
| Reconciliations-open stub | Done | `GET /api/queues/reconciliations` (draft \| in_progress) |
| Close snapshot P&L stub | Done | `GET /api/financial-closes/{id}/pnl` — derived from immutable metrics |
| Derived read only | Done | No SoT totals written onto Bill |
| Vietnamese labels/errors | Done | `VietnameseUiTerms` + VI NotFound messages |
| Tenant isolation | Done | Global filter + tests (profile / queues / P&L) |
| Tests | Done | `Sprint11FullFinancialProfileReportingTests` (4); suite **93 passed** |
| DoD + handoff + ADR-0011 | Done | This file |

## Residual asOf limits (honest)

| Limit | Why |
|-------|-----|
| `AdjustmentAmount` (write-off etc.) | No dated adjustment history — live value used at asOf |
| Missing `ConfirmedAt` / `ActualizedAt` | Layer included (cannot prove absence) |
| No full ledger time-travel | Reconstruct from maturity timestamps + allocation FinalizedAt only |

## APIs

| Method | Path | Notes |
|--------|------|-------|
| GET | `/api/bills/{id}/financial-profile?asOf=` | Maturity reconstruct + settlement outstanding |
| GET | `/api/dashboard/summary` | Counts + totals + optional base roll-up (`includeBaseCurrencyRollUp`) |
| GET | `/api/queues/exceptions` | status, severity, overdueOnly, objectType |
| GET | `/api/queues/approvals` | status, objectType, requiredLevel |
| GET | `/api/queues/reconciliations` | open reconciliations stub |
| GET | `/api/financial-closes/{id}/pnl` | Snapshot P&L; optional `snapshotId` |

## Deferred / follow-ups

| Item | Target |
|------|--------|
| Real dated FX table / market feed | Later |
| Next.js dashboard / queue UI | Pass UI |
| Hardening / NFR / load | Sprint 12 |
| Full adjustment history for asOf | Later |

## Verify

```bash
dotnet test Cms.sln -c Release
```
