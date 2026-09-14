# ADR-0016: System role catalog (7 roles) + Admin toggle

- Status: Accepted
- Date: 2026-09-14
- Relates: TD1 D01; ADR-0003; H-009 Permission ≠ Approval; View Cost ≠ Revenue

## Context
CMS already stores Role × Action × Data Scope but only seeded `Admin`. Operators need operable job roles for logistics finance, and Administrators must turn permissions on/off without inventing a second auth model.

## Decision
1. **Seven system roles** (tenant-scoped, `IsSystem=true`): `Admin`, `FinancialController`, `CostAccountant`, `RevenueAccountant`, `Ops`, `MasterData`, `Viewer`.
2. Defaults are **templates** — Admin may enable/disable any Action on a role (and change Data Scope) via `PUT /api/roles/{id}/permissions`.
3. **Protect** `Admin`: cannot revoke `user.manage` or `role.manage` from the Admin role (tenant would lock itself out).
4. **Cost ≠ Revenue**: CostAccountant has no `revenue.*`; RevenueAccountant has no `cost.*`; Margin requires both reads (existing dashboard rule).
5. **Permission ≠ Approval**: approval queues stay independent of this matrix.
6. Seed on tenant create / bootstrap / migrate: create missing system roles; for non-Admin roles assign defaults **only when the role is first created** (do not re-grant after Admin revoke). Admin role continues to receive every new catalog Action at `data_scope=all`.

## Consequences
- Identity APIs require `user.manage` / `role.manage`.
- UI `/admin/access` is the operator surface for assign role + toggle permissions.
- Custom roles (`IsSystem=false`) remain allowed.

## Alternatives rejected
- Infer permissions from role display name — breaks Action × Scope independence.
- Per-user permission overrides day one — YAGNI; assign multiple roles or clone a custom role.
- Dozens of micro-roles (AP clerk, AR clerk, close-only…) — fold into Controller / Accountants until measured pain.
