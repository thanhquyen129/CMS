# Sprint 1 FULL — Definition of Done (Pass 2 Identity + Master Data)

Pass 1 Sprint 1 delivered thin Identity/Master (Action permission stub; Data Scope `all`/`own` only; no party_roles).
Pass 2 closes E01/E02 operable depth on JWT foundation (Sprint 0 FULL).

## Done

| Item | Status | Notes |
|------|--------|-------|
| Data Scope `all` / `organization` / `own` | Done | Independent of Action on `role_permissions.data_scope` |
| Bill list + get scoped | Done | `GET /api/bills`, `GET /api/bills/{id}` via `bill.read` + scope |
| Cost list + get scoped | Done | `GET /api/costs`, `GET /api/costs/{id}` via `cost.read` + scope |
| Organization subtree scope | Done | Home org + descendants |
| Own scope via `CreatedBy` | Done | DbContext stamps actor from JWT `sub` / Dev header |
| Inactive user denied | Done | VI 403 «Tài khoản không còn hiệu lực.» |
| JWT `sub` as actor | Done | `CreatedBy` on Bill create (JWT path tested) |
| `party_roles` table + APIs | Done | Assign / list / revoke; UNIQUE tenant+party+role_code |
| Org tree + children | Done | `GET /api/organizations/tree`, `.../{id}/children` |
| Currency harden | Done | Seed VND/USD/EUR; get-by-code; inactive reject on Cost; baseline decimals locked |
| Role permission assign w/ scope | Done | `POST /api/roles/{id}/permissions` |
| User `OrganizationId` + update | Done | `PUT /api/users/{id}` |
| Migration | Done | `Sprint1Full_IdentityMasterData` |
| Tests | Done | `Sprint1FullIdentityMasterTests` |
| DoD + ADR | Done | This file + ADR-0003 |

## APIs (delta)

| Method | Path | Notes |
|--------|------|-------|
| GET | `/api/bills` | Data-scope filtered |
| POST | `/api/roles/{id}/permissions` | `{ actionCode, dataScope }` |
| GET | `/api/roles/{id}/permissions` | List Role × Action × Scope |
| PUT | `/api/users/{id}` | displayName, isActive, organizationId |
| GET | `/api/organizations/tree` | Nested tree |
| GET | `/api/organizations/{id}/children` | Direct children |
| POST/GET/DELETE | `/api/business-parties/{id}/roles` | party_roles |
| GET | `/api/currencies/{code}` | ISO lookup |
| GET | `/api/currencies?activeOnly=` | Optional filter |

## Core Action codes (extended)

`bill.create`, `bill.read`, `cost.create`, `cost.read`, `master.org.manage`, `master.party.manage`, `master.currency.manage`, `user.manage`, `role.manage`

## Deferred / follow-ups

| Item | Target |
|------|--------|
| OIDC / external IdP | Later Pass 2 |
| Data Scope on Revenue / Documents / AP-AR | Later FULL sprints |
| Multi-org membership per user | Later |
| Full ISO 4217 catalog import | Later |

## Verify

```bash
dotnet test Cms.sln -c Release
```
