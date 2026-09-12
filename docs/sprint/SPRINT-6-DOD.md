# Sprint 6 — Definition of Done (Financial Documents)

TD6: *Document intake/acceptance; line detail; N:N matching* — Epic **E08**.

## Done (this sprint)

| Item | Status | Notes |
|------|--------|-------|
| `financial_documents` TD1 D07 | Done | type/no/direction/amounts; **ReceiptStatus ≠ AcceptanceStatus ≠ MatchingStatus** |
| `financial_document_lines` | Done | line amount + cached matched_amount; open = amount − matched |
| `document_matches` | Done | method/status/version; tolerance stub = 0 |
| `document_match_details` | Done | N:N line↔line or line↔Cost/Revenue stub link |
| Received ≠ Accepted ≠ Matched (AC-005) | Done | three independent status fields; accept/match do not overwrite receipt |
| C-007 no over-match | Done | reject when matched_sum > open (+ tolerance 0) |
| C-003 / C-004 | Done | receive/match never create Cost/Revenue; link-only |
| Tenant APIs + VI errors | Done | FluentValidation VI + AppException |
| Tenant isolation | Done | Global filter + `X-Tenant-Id` |
| Migration `Sprint6_FinancialDocuments` | Done | Adds four tables; does not alter Sprint 0–5 migrations |
| Tests | Done | 3 new; suite green (28) |
| Sprint 6 DoD doc | Done | This file |

## Deferred (later sprints)

| Item | Target | Reason |
|------|--------|--------|
| AP/AR recognition / exposures | Sprint 7 | Non-goal |
| Settlement / payment allocation | Later | Non-goal |
| Auto-match engine | Later | Manual methods only Pass 1/2 |
| E-invoice providers | Later | Non-goal |
| Confirm match session workflow | Later / S6 FULL partial | Draft + reverse/cancel in FULL; confirm workflow still deferred |
| JWT / OIDC | Later | Keep header bootstrap |
| Next.js UI | Later | API-only this sprint |
| Tolerance policy + match methods + duplicate + accept gate | Sprint 6 FULL | Done in Pass 2 |

## Headers (bootstrap until JWT)

| Header | Purpose |
|--------|---------|
| `X-Tenant-Id` | Tenant context (C-001) |
| `X-User-Id` | Actor for receive/accept audit |
| `X-Correlation-Id` | Request correlation |

## APIs

| Method | Path | Notes |
|--------|------|-------|
| POST | `/api/financial-documents` | Receive/create (Receipt=received; Accept/Match unchanged) |
| GET | `/api/financial-documents` | List (type/receipt/acceptance/matching filters) |
| GET | `/api/financial-documents/{id}` | Get + lines + open amounts |
| POST | `/api/financial-documents/{id}/accept` | Acceptance only (AC-005) |
| POST | `/api/financial-documents/{id}/lines` | Add detail line |
| POST | `/api/document-matches` | Start draft match (tolerance=0) |
| GET | `/api/document-matches/{id}` | Get + details |
| POST | `/api/document-matches/{id}/details` | N:N detail; C-007 enforced |

## Verify

```bash
dotnet test Cms.sln -c Release
```
