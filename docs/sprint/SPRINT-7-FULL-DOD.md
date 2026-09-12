# Sprint 7 FULL — Definition of Done (Pass 2 Exposure + AP/AR)

Pass 1 Sprint 7 delivered Exposure ≠ Recognized AP/AR + derived outstanding thin slice.
Pass 2 closes TD6 E09 depth: multi-recognize tracking, aging buckets (IDX-007/008), document→exposure link, outstanding vs finalized settlement.

## Done

| Item | Status | Notes |
|------|--------|-------|
| Partial/multi recognition + `recognized_amount` | Done | Sum of AP/AR slices is SoT; cache synced; over-recognize → 409 VI |
| Recognition slices on GET exposure | Done | `recognitions[]` with outstanding per slice |
| Aging fields on AP/AR | Done | `daysPastDue`, `agingBucket` (ADR-0006) |
| Aging bucket query | Done | `GET /api/accounts-payable/aging`, `GET /api/accounts-receivable/aging` |
| Document→exposure link | Done | Create with `financialDocumentId` + `POST …/link-document`; direction check; C-003/C-004 |
| Outstanding respects settlements | Done | Draft alloc unchanged; finalize reduces outstanding (AC-007 / C-015) |
| Vietnamese errors + UI terms | Done | AGING_*, LINK_DOCUMENT, RECOGNIZED_AMOUNT |
| Tests | Done | `Sprint7FullExposureApArTests` (3); suite **74 passed** |
| DoD + handoff + ADR-0006 | Done | This file |

## APIs (delta vs Pass 1)

| Method | Path | Notes |
|--------|------|-------|
| GET | `/api/payable-exposures/{id}` | + `recognitions[]` |
| GET | `/api/receivable-exposures/{id}` | + `recognitions[]` |
| POST | `/api/payable-exposures/{id}/link-document` | Optional doc link; no Cost/Revenue |
| POST | `/api/receivable-exposures/{id}/link-document` | Optional doc link; no Cost/Revenue |
| GET | `/api/accounts-payable` | + `daysPastDue`, `agingBucket`; optional `asOf` |
| GET | `/api/accounts-receivable` | + `daysPastDue`, `agingBucket`; optional `asOf` |
| GET | `/api/accounts-payable/aging` | Bucket summary + items (`asOf`, `counterpartyId`, `currencyCode`, `includeSettled`) |
| GET | `/api/accounts-receivable/aging` | Same for AR |

## Aging buckets (ADR-0006)

`no_due_date` | `current` | `1_30` | `31_60` | `61_90` | `90_plus`

## Deferred / follow-ups

| Item | Target |
|------|--------|
| Next.js aging UI / export | Later Pass 2 |
| Auto-exposure engine from documents | Later |
| Data Scope on AP/AR lists | Later (Sprint 1 FULL pattern) |
| Sprint 8 FULL settlement depth | Pass 2 S8 |

## Verify

```bash
dotnet test Cms.sln -c Release
```
