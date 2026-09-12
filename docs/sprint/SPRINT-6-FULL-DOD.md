# Sprint 6 FULL — Definition of Done (Pass 2 Financial Documents)

Pass 1 Sprint 6 delivered D07 intake + N:N match thin slice (tolerance stub 0, method `manual`).
Pass 2 closes TD6 E08 depth: match methods, tolerance policy, accept-before-match, duplicate control, reverse/cancel.

## Done

| Item | Status | Notes |
|------|--------|-------|
| Match methods `line_to_line` / `line_to_cost` / `line_to_revenue` | Done | Method enforces target shape; link-only Cost/Revenue (C-003/C-004) |
| Tolerance policy | Done | `Documents:DefaultToleranceAbsolute` + `DefaultTolerancePercent`; per-match override; C-007 |
| Accept-before-match | Done | `RequireAcceptBeforeMatch` default true; Received ≠ Accepted gate |
| Soft cancel/void document | Done | `RecordStatus` cancelled/voided; blocks while active match details |
| Duplicate document control | Done | Active unique type+no+counterparty (IDX-006 / BR-FIN-023) |
| Open match amounts API | Done | `GET /api/financial-documents/open-amounts` |
| Reverse match detail + cancel match | Done | Soft status; restores open amounts (RV-003); no hard delete |
| Vietnamese errors + UI terms | Done | Gate/tolerance/duplicate/reverse messages; MATCH_METHOD_* terms |
| Tests | Done | `Sprint6FullFinancialDocumentTests` (3); Pass 1 Sprint6 updated; suite **74 passed** |
| DoD + handoff + ADR-0005 | Done | This file |

## APIs (delta vs Pass 1)

| Method | Path | Notes |
|--------|------|-------|
| POST | `/api/document-matches` | Requires method; optional toleranceAbsolute/Percent |
| POST | `/api/document-matches/{id}/details/{detailId}/reverse` | Soft reverse detail |
| POST | `/api/document-matches/{id}/cancel` | Soft cancel session (no active details) |
| POST | `/api/financial-documents/{id}/cancel` | Soft cancel/void document |
| GET | `/api/financial-documents/open-amounts` | List open match amounts (`onlyOpen` default true) |

## Config

```json
"Documents": {
  "DefaultToleranceAbsolute": 0,
  "DefaultTolerancePercent": 0,
  "EnforceDuplicateControl": true,
  "RequireAcceptBeforeMatch": true
}
```

## Deferred / follow-ups

| Item | Target |
|------|--------|
| Auto-match engine | Later Pass 2 |
| Confirm match session workflow beyond draft/cancel | Later |
| E-invoice providers | Later |
| Next.js Documents UI | Later Pass 2 |
| AP/AR recognition changes | Sprint 7 FULL |

## Verify

```bash
dotnet test Cms.sln -c Release
```
