# ADR-0003: Permission × Data Scope (all / organization / own)

- Status: Accepted
- Date: 2026-09-11
- Relates: TD1 D01; CP7 Permission ≠ Approval; Sprint 1 FULL (Pass 2); builds on ADR-0002 JWT actor

## Context
Pass 1 stored `data_scope` on `role_permissions` but only stubbed `all`/`own` and did not enforce scope on reads.
Multi-tenant finance control needs **Action permission** and **Data Scope** as independent dimensions (H-guardrail / implementation plan).

## Decision
1. Data Scope values: `all` | `organization` | `own` (widen: all > organization > own when multiple RolePermissions match).
2. Enforce on Bill + Cost **list/get** at minimum (`bill.read` / `cost.read`).
3. `organization` = user's home `users.organization_id` + organization subtree (parent→children).
4. `own` = `CreatedBy ==` JWT `sub` (or Dev `X-User-Id`).
5. Out-of-scope get returns **404** (no existence leak). List silently filters.
6. Add nullable `organization_id` on `users`, `bills`, `costs` for scope keys.
7. Inactive users (`users.is_active=false`) are denied on any `IPermissionService` check.

## Consequences
- Creates must stamp `CreatedBy` (DbContext) and preferably `OrganizationId`.
- Admin seed remains `data_scope=all`.
- **P21 (2026-09-13):** same contract enforced on Revenue (`revenue.read`), Financial Documents (`bill.read`), AP (`cost.read`), AR (`revenue.read`). Org scope joins via `Bill.OrganizationId` when the row has no direct org column.

## Alternatives rejected
- Infer scope from role name — violates independent dimensions.
- Soft-fail out-of-scope get as empty 200 — hides authorization bugs.
- ABAC policy engine day one — YAGNI; RolePermission matrix is enough for go-live slice.
