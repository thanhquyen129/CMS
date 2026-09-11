# Sprint 8 — Definition of Done (Settlement)

TD6: *Payment/collection; allocation; reversal; outstanding* — Epic **E10**.

## Done (this sprint)

| Item | Status | Notes |
|------|--------|-------|
| `payments` TD1 D09 | Done | Cash-out transaction; IDX-009 |
| `collections` | Done | Cash-in transaction; IDX-010 |
| `payment_allocations` | Done | N:N Payment↔AP; draft/finalized/reversed |
| `collection_allocations` | Done | N:N Collection↔AR; draft/finalized/reversed |
| Outstanding via finalized allocation only (AC-007) | Done | Draft does not change AP/AR outstanding |
| C-008 no over-allocation | Done | Over policy stub = 0; reject vs payment/collection amount and AP/AR ceiling |
| Partial settlement + unapplied | Done | `UnappliedAmount` / `AvailableToAllocate` on GET |
| Reversal without hard delete | Done | Status → `reversed`; restores `FinalizedSettledAmount` |
| C-003 / C-004 | Done | Payment/collection/allocate/finalize never invent Cost/Revenue |
| Tenant APIs + VI errors | Done | FluentValidation VI + AppException |
| Tenant isolation | Done | Global filter + `X-Tenant-Id` |
| Migration `Sprint8_Settlement` | Done | Four tables; does not alter Sprint 0–7 migrations |
| Tests | Done | 3 new; suite green (34) |
| Sprint 8 DoD doc | Done | This file |

## Deferred (later sprints / Pass 2)

| Item | Target | Reason |
|------|--------|--------|
| Reconciliation / exceptions | Sprint 9 | Non-goal |
| Financial close | Sprint 10 | Non-goal |
| Bank feed / auto-match cash | Later | Manual create Pass 1 |
| Over-settlement policy | Later | Stub = reject all over |
| Reverse allocation as new opposite row | Later | Pass 1 uses status=reversed |
| JWT / OIDC | Later | Keep header bootstrap |
| Next.js UI | Later | API-only this sprint |

## Headers (bootstrap until JWT)

| Header | Purpose |
|--------|---------|
| `X-Tenant-Id` | Tenant context (C-001) |
| `X-User-Id` | Actor for finalize/reverse audit |
| `X-Correlation-Id` | Request correlation |

## APIs

| Method | Path | Notes |
|--------|------|-------|
| POST | `/api/payments` | Create payment (cash out) |
| GET | `/api/payments` | List + unapplied |
| GET | `/api/payments/{id}` | Get + allocations |
| POST | `/api/payments/{id}/allocations` | Draft allocate → AP |
| POST | `/api/payment-allocations/{id}/finalize` | Finalize → updates AP outstanding |
| POST | `/api/payment-allocations/{id}/reverse` | Reverse / cancel draft |
| POST | `/api/collections` | Create collection (cash in) |
| GET | `/api/collections` | List + unapplied |
| GET | `/api/collections/{id}` | Get + allocations |
| POST | `/api/collections/{id}/allocations` | Draft allocate → AR |
| POST | `/api/collection-allocations/{id}/finalize` | Finalize → updates AR outstanding |
| POST | `/api/collection-allocations/{id}/reverse` | Reverse / cancel draft |

## Verify

```bash
dotnet test Cms.sln -c Release
```
