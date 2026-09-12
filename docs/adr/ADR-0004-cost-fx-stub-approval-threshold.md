# ADR-0004: Cost/Revenue FX stub + optional confirm approval threshold

- Status: Accepted
- Date: 2026-09-11
- Relates: TD1 D05/D06/D09 money fields (`base_amount`, `fx_rate_id`); C-014; Sprint 4 FULL Cost; Sprint 5 FULL Revenue; Sprint 8 FULL Settlement (Pass 2)

## Context
Pass 1 Cost/Revenue stored `base_amount` / `fx_rate_id` but never filled them. Cross-currency reporting must not sum raw amounts (C-014). Full FX table + market feed is later; go-live slice needs an honest stub.
Large cost/revenue confirm may need an approval gate before Expected→Confirmed without building a full multi-step matrix (Sprint 9 already owns Approval ≠ Permission).

## Decision
1. **FX stub** (`Cost` / `Revenue` / `Settlement` options): `BaseCurrency` (default `VND`) + `StubFxRatesToBase` map. On create/confirm/actualize/adjust/seed (Cost/Revenue) or create payment/collection/allocate (Settlement), set `BaseAmount = amount` when same currency; else `amount × stub rate`. `FxRateId` stays null until a real `fx_rates` entity exists.
2. Missing stub rate for a foreign currency → VI validation error (do not invent 1:1).
3. **Optional confirm threshold**: `ConfirmApprovalThresholdBase` (nullable) per module. When set and `BaseAmount` exceeds it, set `ApprovalStatus=pending` and **block confirm** until Sprint 9 approval sets `approved`. Null = disabled (Pass 1 behavior).
4. Allocation bases locked to `equal` | `quantity` | `manual_ratio` (C-006); reallocation supersedes prior finalized rows (history kept).
5. Revenue Single Economic Revenue (C-004) remains: reject document/AR sources inventing a second economic row.

## Consequences
- Reporting may use `BaseAmount` for same-tenant roll-ups with explicit stub caveat.
- Threshold is config-driven (not per-tenant DB yet) — fine for single-node go-live; tenant override is a follow-up.
- Real FX table / dated rates replace stub without changing Cost/Revenue API shape.
- Profitability views remain derived read models (TD1-DB-003/004) — never SoT on Bill.

## Alternatives rejected
- Silent 1:1 FX for unknown currencies — hides C-014 defects.
- Full approval matrix in Cost/Revenue modules — duplicates Sprint 9; gate only.
- Hard-coding rates in code — config keeps ops reversible.
- Sharing one config section only — separate `Cost` / `Revenue` thresholds keep ops independent.
