# Sprint 1 — Definition of Done (Identity + Master Data)

TD6: *Tenant/user/access; organization/party/currency; base configuration* — Epics **E01 / E02** thinnest operable slice.

## Done (this sprint)

| Item | Status | Notes |
|------|--------|-------|
| User entity + API | Done | `POST/GET /api/users` (tenant header) |
| Role + Permission + UserRole | Done | Action codes; Data Scope stub `all`/`own` |
| Assign user↔role | Done | `POST /api/users/{userId}/roles/{roleId}` |
| Permission check on Bill create | Done | `IPermissionService`; VI 403 + correlation id |
| Soft bootstrap | Done | No `X-User-Id` ⇒ allow; Admin seeded on tenant create |
| CreateTenant seeds Admin | Done | Core action catalog + Admin role permissions |
| Organization CRUD | Done | ` /api/organizations` tenant-scoped, soft-delete |
| BusinessParty CRUD | Done | `/api/business-parties` tenant-scoped, soft-delete |
| Currency list + upsert | Done | Global catalog `/api/currencies` |
| Migration `Sprint1_IdentityMaster` | Done | Does not alter `InitialTd1` |
| Cross-tenant isolation tests | Done | User / Org / Party |
| Permission denial test | Done | Vietnamese message + correlation id |
| Sprint 1 DoD doc | Done | This file |

## Deferred (later sprints)

| Item | Target | Reason |
|------|--------|--------|
| JWT / OIDC full auth | Later | Keep `X-Tenant-Id` + `X-User-Id` bootstrap |
| Full Data Scope matrix | Later | Stub `all`/`own` only; no ABAC |
| Approval workflow | Later | Permission ≠ Approval (H-guardrail) |
| CP5 policy versioning | Later | Beyond Sprint 1 |
| party_roles table | Later | Optional; not required for operable slice |
| Cost / Revenue business logic | Phase 2+ | Finance path |
| Next.js UI | Later | API-only this sprint |

## Headers (bootstrap until JWT)

| Header | Purpose |
|--------|---------|
| `X-Tenant-Id` | Tenant context (C-001) |
| `X-User-Id` | Actor for permission checks (optional; omit = bootstrap allow) |
| `X-Correlation-Id` | Request correlation |

## Core Action codes

`bill.create`, `bill.read`, `master.org.manage`, `master.party.manage`, `master.currency.manage`, `user.manage`, `role.manage`

## Verify

```bash
dotnet test Cms.sln -c Release
curl -fsS http://194.233.89.26/health
curl -fsS http://194.233.89.26/ready
```
