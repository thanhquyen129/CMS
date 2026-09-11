# Sprint 9 — Definition of Done (Financial Control)

TD6: *Reconciliation; variance; exception; approval* — Epic **E11**.

## Done (this sprint)

| Item | Status | Notes |
|------|--------|-------|
| `reconciliations` TD1 D10 | Done | Type/rule/version/status session |
| `reconciliation_details` | Done | Source/target/matched/variance |
| `variances` | Done | Derived/control fact; not inbox |
| `exceptions` | Done | Rule/severity/owner/SLA (IDX-011) |
| `approvals` | Done | Independent workflow on object ref |
| Variance ≠ Exception | Done | Reconcile creates Variance only; Exception opened separately |
| Permission ≠ Approval | Done | Approval handlers never call `IPermissionService` |
| Tenant APIs + VI errors | Done | FluentValidation VI + AppException |
| Soft-delete / row_version / tenant_id | Done | Via `TenantEntityBase` + global filters |
| Tenant isolation | Done | Global filter + `X-Tenant-Id` |
| Migration `Sprint9_FinancialControl` | Done | Five tables; does not alter Sprint 0–8 migrations |
| Tests | Done | 3 new; suite green (37) |
| Sprint 9 DoD doc | Done | This file |

## Deferred (later sprints / Pass 2)

| Item | Target | Reason |
|------|--------|--------|
| Financial close snapshots | Sprint 10 | Non-goal |
| Full SLA inbox UI / auto-escalate variance→exception | Later | Manual open Pass 1 |
| Multi-step approval chains / thresholds | Later | Single request/decide Pass 1 |
| Bank feed / auto-reconcile | Later | Manual session Pass 1 |
| JWT / OIDC | Later | Keep header bootstrap |
| Next.js UI | Later | API-only this sprint |

## Headers (bootstrap until JWT)

| Header | Purpose |
|--------|---------|
| `X-Tenant-Id` | Tenant context (C-001) |
| `X-User-Id` | Actor for approval/exception audit |
| `X-Correlation-Id` | Request correlation |

## APIs

| Method | Path | Notes |
|--------|------|-------|
| POST | `/api/reconciliations` | Create/start session |
| GET | `/api/reconciliations` | List (+ optional status) |
| GET | `/api/reconciliations/{id}` | Get + details |
| POST | `/api/reconciliations/{id}/details` | Add source/target/matched; auto Variance if delta ≠ 0 |
| GET | `/api/variances` | List control facts |
| GET | `/api/variances/{id}` | Get variance |
| POST | `/api/exceptions` | Open exception (severity/owner/due) |
| GET | `/api/exceptions` | List inbox |
| GET | `/api/exceptions/{id}` | Get |
| POST | `/api/exceptions/{id}/resolve` | Resolve stub |
| POST | `/api/exceptions/{id}/close` | Close stub |
| POST | `/api/approvals` | Request approval on cost/revenue/document/settlement… |
| GET | `/api/approvals` | List |
| GET | `/api/approvals/{id}` | Get |
| POST | `/api/approvals/{id}/approve` | Approve (not Permission) |
| POST | `/api/approvals/{id}/reject` | Reject (not Permission) |

## Verify

```bash
dotnet test Cms.sln -c Release
```
