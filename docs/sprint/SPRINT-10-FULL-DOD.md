# Sprint 10 FULL — Definition of Done (Pass 2 Financial Close)

Pass 1 Sprint 10 delivered close sessions, immutable snapshots, reopen/reclose versioning, and a critical-exception eligibility stub.
Pass 2 closes TD6 E12 depth: full eligibility checklist, period lock on live ledger mutations, Controlled vs Strict policy, AC-008 kept.

## Done

| Item | Status | Notes |
|------|--------|-------|
| Eligibility — critical exceptions | Done | open/in_progress/escalated critical in scope; VI reason |
| Eligibility — unmatched accepted docs stub | Done | accepted + unmatched/partially_matched; VI reason |
| Eligibility — unsettled AP/AR above threshold stub | Done | outstanding > `UnsettledApArOpenBalanceThreshold` (default 0); VI reason |
| Eligibility config flags (default on) | Done | `FinancialClose:Eligibility:*` |
| Period lock on Locked close | Done | Blocks Cost/Revenue confirm + Payment/Collection allocate/finalize |
| `FinancialClose:EnforcePeriodLock` default true | Done | Controlled respects flag; Strict always enforces |
| Policy Controlled vs Strict on close record | Done | Strict forces eligibility + period lock (no bypass) |
| Snapshots insert-only (C-010 / AC-008) | Done | Unchanged DbContext guard; reopen/reclose append only |
| Reopen/reclose history | Done | Pass 1 behavior retained; VI errors |
| Vietnamese errors + UI terms | Done | Eligibility / period lock / Strict terms |
| Tests | Done | `Sprint10FullFinancialCloseTests` (3); suite green |
| DoD + handoff + ADR-0010 | Done | This file |

## Config

```json
"FinancialClose": {
  "EnforcePeriodLock": true,
  "Eligibility": {
    "BlockOnCriticalExceptions": true,
    "BlockOnUnmatchedAcceptedDocuments": true,
    "BlockOnUnsettledApArAboveThreshold": true,
    "UnsettledApArOpenBalanceThreshold": 0
  }
}
```

## APIs (unchanged paths; behavior deepened)

| Method | Path | Notes |
|--------|------|-------|
| POST | `/api/financial-closes` | `policyVersion`: `controlled` \| `strict` |
| POST | `/api/financial-closes/{id}/snapshot` | Full eligibility checklist → lock |
| POST | `/api/financial-closes/{id}/reopen` | No snapshot mutation |
| POST | `/api/costs/{id}/confirm` | Period lock when Locked |
| POST | `/api/revenues/{id}/confirm` | Period lock when Locked |
| POST | `/api/payments/{id}/allocations` | Period lock when Locked |
| POST | `/api/payment-allocations/{id}/finalize` | Period lock when Locked |
| POST | `/api/collections/{id}/allocations` | Period lock when Locked |
| POST | `/api/collection-allocations/{id}/finalize` | Period lock when Locked |

## Deferred / follow-ups

| Item | Target |
|------|--------|
| Reporting dashboard / P&L from snapshots | Sprint 11 |
| Hardening / UAT / Strict policy matrix expansion | Sprint 12 |
| Per-tenant eligibility threshold tables | Later |
| Next.js close UI | Pass UI |

## Verify

```bash
dotnet test Cms.sln -c Release
```
