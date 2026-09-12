# ADR-0006 — AP/AR aging buckets (E09 FULL)

## Status
Accepted — 2026-09-12

## Context
TD1 IDX-007/008 index AP/AR by `(tenant, counterparty, due_date, settlement_status)` for aging.
Pass 1 deferred aging reports; Pass 2 Sprint 7 FULL exposes derived aging on API.

## Decision
- Aging is **derived**, never stored as SoT.
- As-of date defaults to UTC today (`DateOnly.FromDateTime(DateTime.UtcNow)`); optional `asOf` query param.
- `daysPastDue` = `(asOf − dueDate).Days` when `dueDate` present; null when no due date.
- Bucket codes (stable public API):

| Code | Rule |
|------|------|
| `no_due_date` | `dueDate` is null |
| `current` | `daysPastDue <= 0` (not yet due / due today) |
| `1_30` | 1…30 days past due |
| `31_60` | 31…60 |
| `61_90` | 61…90 |
| `90_plus` | ≥ 91 |

- Outstanding in aging = C-015 derived (`recognized + adjustment − finalized_settled`). Draft allocations do not reduce outstanding (AC-007).
- Settled rows (`settlement_status = settled` and outstanding ≤ 0) remain listable but aging summary defaults to **open outstanding only** (`outstanding > 0`) unless `includeSettled=true`.

## Consequences
- Changing bucket boundaries later needs a new API version or additive codes.
- No EF migration (computed fields only).
- **P07:** Combined `GET /api/aging/summary` + CSV `GET /api/aging/export`; AP aging requires `cost.read`, AR requires `revenue.read`. Dashboard money totals omit Cost / Revenue / Margin when the actor lacks the matching permission (H View Cost ≠ Revenue).

## Alternatives rejected
- Storing `aging_bucket` column — drifts vs clock/`asOf`.
- UI-only aging — finance control needs API/query first.
- Inferring Margin from Cost-only or Revenue-only — violates financial privilege separation.
