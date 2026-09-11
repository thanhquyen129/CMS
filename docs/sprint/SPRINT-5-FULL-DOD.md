# Sprint 5 FULL — Definition of Done (Pass 2 Revenue & Profitability)

Pass 1 Sprint 5 delivered Revenue lifecycle thin slice + Bill financial profile Best Available.
Pass 2 closes TD6 E07 (+ E13 profile depth): FX stub parity with Cost FULL, optional confirm threshold, profitability views, C-004 harden.

## Done

| Item | Status | Notes |
|------|--------|-------|
| Revenue FX stub `base_amount` | Done | `Revenue:BaseCurrency` + `StubFxRatesToBase`; `FxRateId` null (ADR-0004) |
| Optional confirm approval threshold | Done | `Revenue:ConfirmApprovalThresholdBase`; pending until Sprint 9 approve |
| Maturity + adjustments parity Cost | Done | FX refresh on confirm/actualize/adjust; gate on confirm |
| Financial profile enhancements | Done | Variance Expected vs Actual; allocated cost in CostBestAvailable; multi-currency separated |
| `GET /api/bills/{id}/profitability?view=` | Done | `expected` \| `confirmed` \| `actual` \| `best` |
| C-004 Single Economic Revenue hardened | Done | Reject document/AR + aliases; VI message; defense in depth |
| Vietnamese errors + UI terms | Done | REVENUE_CONFIRM_APPROVAL_THRESHOLD, BILL_PROFITABILITY, … |
| Tests | Done | `Sprint5FullRevenueProfitabilityTests` (3); suite must stay green |
| DoD + handoff + ADR-0004 update | Done | This file |

## APIs (delta vs Pass 1)

| Method | Path | Notes |
|--------|------|-------|
| POST | `/api/revenues` | Fills `baseAmount`; may set `approvalStatus=pending` when over threshold |
| GET | `/api/revenues/{id}` | Exposes `baseAmount`, `fxRateId` |
| POST | `/api/revenues/{id}/confirm` | Blocks with VI 409 when over threshold and not approved |
| GET | `/api/bills/{id}/financial-profile` | + variance fields; allocated cost included |
| GET | `/api/bills/{id}/profitability?view=` | Explicit maturity view (default `best`) |

## Config

```json
"Revenue": {
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
| Recognition policy engine beyond stub version string | Later |
| Next.js Revenue / profitability UI | Later Pass 2 |

## Verify

```bash
dotnet test Cms.sln -c Release
```
