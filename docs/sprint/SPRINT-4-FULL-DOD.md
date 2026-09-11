# Sprint 4 FULL — Definition of Done (Pass 2 Cost)

Pass 1 Sprint 4 delivered Cost lifecycle thin slice (maturity, adjustments, allocation conservation, rating seed).
Pass 2 closes TD6 E05/E06 depth: formal allocation bases, Shared/Direct harden, FX stub, optional confirm threshold.

## Done

| Item | Status | Notes |
|------|--------|-------|
| Allocation bases `equal` / `quantity` / `manual_ratio` | Done | Reject unknown bases (C-006); `equal` forces weight=1 |
| C-005 conservation on finalize | Done | SUM(details)=allocatable after rounding (last-line residual) |
| C-006 basis gate | Done | basis ≤ 0 / missing / unsupported → reject |
| Reallocation supersedes history | Done | Prior `finalized` → `superseded`; `supersedes_allocation_id`; versions kept |
| Shared vs Direct rules | Done | Direct requires Bill; Shared Bill null; allocate Shared only |
| FX stub `base_amount` | Done | `Cost:BaseCurrency` + `StubFxRatesToBase`; `FxRateId` null until real FX (ADR-0004) |
| Optional confirm approval threshold | Done | `Cost:ConfirmApprovalThresholdBase`; pending until Sprint 9 approve |
| Vietnamese errors + UI terms | Done | VI validation/conflict; ALLOCATION_BASIS_*, BASE_AMOUNT, … |
| Tests | Done | `Sprint4FullCostTests` (3); suite **63 passed** |
| DoD + handoff + ADR-0004 | Done | This file |

## APIs (delta vs Pass 1)

| Method | Path | Notes |
|--------|------|-------|
| POST | `/api/costs` | Fills `baseAmount`; may set `approvalStatus=pending` when over threshold |
| GET | `/api/costs/{id}` | Exposes `baseAmount`, `fxRateId`, allocation `supersedesAllocationId` |
| POST | `/api/costs/{id}/confirm` | Blocks with VI 409 when over threshold and not approved |
| POST | `/api/costs/{id}/allocations` | `allocationBasis` ∈ equal\|quantity\|manual_ratio |
| POST | `/api/cost-allocations/{id}/finalize` | Conservation + supersede prior finalized |

## Config

```json
"Cost": {
  "BaseCurrency": "VND",
  "StubFxRatesToBase": { "USD": 25000, "EUR": 27000 },
  "ConfirmApprovalThresholdBase": null
}
```

## Deferred / follow-ups

| Item | Target |
|------|--------|
| Real `fx_rates` table + dated rates | Later Pass 2 |
| Per-tenant FX / threshold overrides | Later |
| Full approval matrix / multi-step | Sprint 9 depth / later |
| Next.js Cost UI | Later Pass 2 |

## Verify

```bash
dotnet test Cms.sln -c Release
```
