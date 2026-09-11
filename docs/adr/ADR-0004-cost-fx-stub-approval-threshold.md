# ADR-0004: Cost FX stub + optional confirm approval threshold

- Status: Accepted
- Date: 2026-09-11
- Relates: TD1 D05 money fields (`base_amount`, `fx_rate_id`); C-014; Sprint 4 FULL (Pass 2)

## Context
Pass 1 Cost stored `base_amount` / `fx_rate_id` but never filled them. Cross-currency reporting must not sum raw amounts (C-014). Full FX table + market feed is later; go-live slice needs an honest stub.
Large-cost confirm may need an approval gate before Expected→Confirmed without building a full multi-step matrix (Sprint 9 already owns Approval ≠ Permission).

## Decision
1. **FX stub** (`Cost` options): `BaseCurrency` (default `VND`) + `StubFxRatesToBase` map. On create/confirm/actualize/adjust/seed, set `BaseAmount = amount` when same currency; else `amount × stub rate`. `FxRateId` stays null until a real `fx_rates` entity exists.
2. Missing stub rate for a foreign currency → VI validation error (do not invent 1:1).
3. **Optional confirm threshold**: `ConfirmApprovalThresholdBase` (nullable). When set and `BaseAmount` exceeds it, set `ApprovalStatus=pending` and **block confirm** until Sprint 9 approval sets `approved`. Null = disabled (Pass 1 behavior).
4. Allocation bases locked to `equal` | `quantity` | `manual_ratio` (C-006); reallocation supersedes prior finalized rows (history kept).

## Consequences
- Reporting may use `BaseAmount` for same-tenant roll-ups with explicit stub caveat.
- Threshold is config-driven (not per-tenant DB yet) — fine for single-node go-live; tenant override is a follow-up.
- Real FX table / dated rates replace stub without changing Cost API shape.

## Alternatives rejected
- Silent 1:1 FX for unknown currencies — hides C-014 defects.
- Full approval matrix in Cost module — duplicates Sprint 9; gate only.
- Hard-coding rates in code — config keeps ops reversible.
