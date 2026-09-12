# Sprint 9 FULL — Definition of Done (Pass 2 Financial Control)

Pass 1 Sprint 9 delivered reconciliation/variance/exception/approval thin slice.
Pass 2 closes TD6 E11 depth: batch recon, variance severity thresholds, exception SLA/escalate/object link, multi-step approval, confirm block on critical exception.

## Done

| Item | Status | Notes |
|------|--------|-------|
| Reconciliation multi-detail batch | Done | `POST /api/reconciliations/{id}/details/batch` |
| Auto Variance on unmatched/delta | Done | Same as Pass 1; batch shares writer |
| Variance severity from amount thresholds | Done | `FinancialControl:VarianceSeverityThresholds` |
| Exception severity/owner/due_at SLA | Done | Default SLA hours when `dueAt` omitted |
| Escalate stub | Done | `POST …/escalate` → status `escalated` + severity bump |
| Exception object_type/object_id | Done | Link for confirm gate + inbox filter |
| Inbox filter severity/status/object/overdue | Done | Query params on `GET /api/exceptions` |
| Multi-step approval (level 1/2) | Done | `requiredLevel` / `currentLevel`; reject requires reason |
| Permission ≠ Approval | Done | Handlers never mutate Permission/RolePermission |
| Block Cost/Revenue confirm on critical open exception | Done | Config flag default **true** |
| Vietnamese errors + UI terms | Done | Escalate/SLA/block/batch terms |
| Tests | Done | `Sprint9FullFinancialControlTests` (3); suite **83 passed** |
| DoD + handoff + ADR-0009 | Done | This file |
| Migration `Sprint9Full_FinancialControl` | Done | severity / object link / approval levels / escalate fields |

## APIs (delta vs Pass 1)

| Method | Path | Notes |
|--------|------|-------|
| POST | `/api/reconciliations/{id}/details/batch` | Multi-line; auto Variance + severity |
| GET | `/api/variances` | + `severity` |
| POST | `/api/exceptions` | + `objectType`/`objectId`; SLA default |
| GET | `/api/exceptions` | + `objectType`, `overdueOnly` |
| POST | `/api/exceptions/{id}/escalate` | Escalate stub |
| POST | `/api/approvals` | + `requiredLevel` (1\|2) |
| GET | `/api/approvals/{id}` | + `requiredLevel`, `currentLevel` |

## Config

```json
"FinancialControl": {
  "VarianceSeverityThresholds": { "Medium": 100, "High": 1000, "Critical": 10000 },
  "DefaultExceptionSlaHours": { "low": 168, "medium": 72, "high": 24, "critical": 8 },
  "BlockConfirmOnCriticalException": true
}
```

## Deferred / follow-ups

| Item | Target |
|------|--------|
| Auto-escalate variance → exception | Later (keeps Variance ≠ Exception) |
| Per-tenant threshold / SLA tables | Later |
| Full approver matrix / sequential user list | Later |
| Bank feed auto-reconcile | Later |
| Financial close changes | Sprint 10 |
| Next.js inbox UI | Pass UI |

## Verify

```bash
dotnet test Cms.sln -c Release
```
