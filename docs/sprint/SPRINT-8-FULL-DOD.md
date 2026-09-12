# Sprint 8 FULL — Definition of Done (Pass 2 Settlement)

Pass 1 Sprint 8 delivered payment/collection + draft/finalize/reverse allocation thin slice.
Pass 2 closes TD6 E10 depth: unapplied multi-allocation, FX stub, write-off stub, idempotent finalize.

## Done

| Item | Status | Notes |
|------|--------|-------|
| Unapplied amount + subsequent allocate | Done | `UnappliedAmount` / `AvailableToAllocate`; allocate remaining until fully applied |
| Multi-AP/AR allocation from one cash txn | Done | N draft/finalized rows; C-008 reject over payment/collection or AP/AR ceiling |
| C-008 over-allocate reject | Done | Over policy stub = 0 (unchanged) |
| FX stub `base_amount` on settlement | Done | `Settlement:BaseCurrency` + `StubFxRatesToBase`; `FxRateId` null (ADR-0004/0008) |
| Write-off stub with reason | Done | Negative `AdjustmentAmount` + note; `MaxWriteOffAmount`; not silent wipe |
| Idempotent finalize | Done | Re-finalize finalized → no-op 204; reversed → 409 VI |
| Vietnamese errors + UI terms | Done | WRITE_OFF, SETTLEMENT_BASE_AMOUNT, IDEMPOTENT_FINALIZE, … |
| Tests | Done | `Sprint8FullSettlementTests` (3); suite green |
| DoD + handoff + ADR-0008 | Done | This file |
| Migration `Sprint8Full_Settlement` | Done | base_amount / fx_rate_id / allocation currency_code |

## APIs (delta vs Pass 1)

| Method | Path | Notes |
|--------|------|-------|
| POST | `/api/payments` | Fills `baseAmount` |
| GET | `/api/payments/{id}` | + `baseAmount`, `fxRateId`; allocation FX fields |
| POST | `/api/collections` | Fills `baseAmount` |
| GET | `/api/collections/{id}` | + `baseAmount`, `fxRateId` |
| POST | `/api/payment-allocations/{id}/finalize` | Idempotent |
| POST | `/api/collection-allocations/{id}/finalize` | Idempotent |
| POST | `/api/accounts-payable/{id}/write-off` | `{ amount, reason }` |
| POST | `/api/accounts-receivable/{id}/write-off` | `{ amount, reason }` |

## Config

```json
"Settlement": {
  "BaseCurrency": "VND",
  "StubFxRatesToBase": { "USD": 25000, "EUR": 27000 },
  "MaxWriteOffAmount": 1000
}
```

## Deferred / follow-ups

| Item | Target |
|------|--------|
| Bank feed reconciliation / auto-match | Sprint 9 |
| Real `fx_rates` table + dated rates | Later Pass 2 |
| Write-off approval / larger policy | Sprint 9+ |
| Opposite-row reverse allocation | Later |
| Next.js Settlement UI | Pass UI |

## Verify

```bash
dotnet test Cms.sln -c Release
```
