# ADR-0033 — revenue.create and money-write SoD

- Status: Accepted
- Date: 2026-09-24
- Relates: ADR-0016, H-009, CRP-02

## Context

Confirm and actualize already require `cost.confirm` / `revenue.confirm` / `*.actualize`. Create and adjust did not. Any signed-in user with a role could post a revenue row. There was no `revenue.create` action, so Cost ≠ Revenue could not be enforced on the write that creates the economic fact.

## Decision

1. Add `revenue.create` to the permission catalog.
2. Grant it to Admin (all catalog actions), FinancialController, and RevenueAccountant. CostAccountant and Ops do not receive it.
3. `POST /api/costs` requires `cost.create`. `POST /api/revenues` requires `revenue.create`.
4. Adjust follows the current maturity: expected → create, confirmed → confirm, actual → actualize. Same split for revenue.
5. Existing tenants pick up the new grant on the next system-role seed (missing catalog rows only; Admin soft-revokes stay revoked).

## Consequences

- A Cost Accountant token cannot create or list revenue; a Revenue Accountant token cannot create cost.
- Bootstrap with no actor (tests / first header path) still allows, matching `IPermissionService`.
- UI hiding remains cosmetic. The API is the boundary.

## Alternatives rejected

- Reuse `revenue.confirm` for create — blocks expected revenue before confirm.
- Infer the right from the role name — breaks Action × Scope.
