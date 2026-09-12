# ADR-0004: Cost/Revenue FX stub + optional confirm approval threshold

- Status: Accepted (amended 2026-09-12 — P06 dated `fx_rates`)
- Date: 2026-09-11
- Relates: TD1 D02/D05/D06/D09 (`fx_rates`, `base_amount`, `fx_rate_id`); C-014; Sprint 4/5/8 FULL; P06

## Context
Pass 1 Cost/Revenue stored `base_amount` / `fx_rate_id` but never filled them. Cross-currency reporting must not sum raw amounts (C-014). Full market FX feed is later; go-live needed an honest conversion path.
Large cost/revenue confirm may need an approval gate before Expected→Confirmed without building a full multi-step matrix (Sprint 9 already owns Approval ≠ Permission).

## Decision
1. **Dated `fx_rates` (P06 / TD1 D02)**: tenant-scoped rows `from/to/rate_date/rate/source/version`. Lookup = latest `rate_date ≤ asOf` (Cost/Revenue `EffectiveDate`, Payment/Collection `ValueDate`, dashboard asOf UTC date). When resolved: `BaseAmount = amount × rate` and set `FxRateId`. Same-currency ⇒ `BaseAmount = amount`, `FxRateId` null.
2. **Config fallback (ADR original stub)**: if no dated row, use module `StubFxRatesToBase` and leave `FxRateId` null. Missing both → VI validation (no silent 1:1).
3. **API**: `POST/GET/DELETE /api/fx-rates`, `GET /api/fx-rates/resolve` — ops upsert without redeploy.
4. **Optional confirm threshold**: `ConfirmApprovalThresholdBase` (nullable) per module. When set and `BaseAmount` exceeds it, set `ApprovalStatus=pending` and **block confirm** until Approval sets `approved`. Null = disabled.
5. Allocation bases locked to `equal` | `quantity` | `manual_ratio` (C-006); reallocation supersedes prior finalized rows (history kept).
6. Revenue Single Economic Revenue (C-004) remains: reject document/AR sources inventing a second economic row.

## Consequences
- Reporting may use `BaseAmount` for same-tenant roll-ups; prefer persisted `FxRateId` when present.
- Stub maps remain for tests/dev until rates are seeded; production should maintain `fx_rates`.
- Market feed / auto-import is a follow-up (source=`import`).
- Profitability views remain derived read models (TD1-DB-003/004) — never SoT on Bill.

## Alternatives rejected
- Silent 1:1 FX for unknown currencies — hides C-014 defects.
- Full approval matrix in Cost/Revenue modules — duplicates Sprint 9; gate only.
- Hard-coding rates in code — config + dated table keep ops reversible.
- Sharing one config section only — separate `Cost` / `Revenue` thresholds keep ops independent.
- Removing stub before `fx_rates` exists — would break existing environments mid-cutover.
