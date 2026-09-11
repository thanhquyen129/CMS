# PROMPT — Pass 2 / Sprint 3 FULL Rate & Pricing

Bạn đang làm **Pass 2 — Sprint 3 FULL** trong `c:\A1\git\cms`. Pass 1 S3 + Pass 2 S0–S2 đã trên `main`.

## Mục tiêu TD6 E04 đủ hơn thin
Rate/pricing engine vận hành được: applicability, multi-component formulas, re-rate history, link rating → Expected cost (đã có seed Pass 1 S4 — harden).

## Must ship
1. Pricing rule applicability fields (service type / party / route stub codes) — filter which rules apply when rating.
2. Component formula types beyond fixed/unit_rate: at least `percent_of_base` and `min_max_clamp` (min/max amount bounds).
3. Re-rate: create new `ratings` version linked to prior (`supersedes_rating_id`); keep history immutable.
4. API: rate with bill context + quantity/weight inputs; list rating history for bill; publish version still immutable (C-011).
5. Hook: after rating, optional flag `seedExpectedCosts=true` calling existing Sprint 4 seed (idempotent).
6. Tests: applicability filter; clamp; re-rate history; tenant isolation; immutable publish.
7. `SPRINT-3-FULL-DOD.md` + handoff + PR → main. `dotnet test` xanh.

## Non-goals
Full DSL formula language, Next.js rate card UI.

## Ràng buộc
Never secrets/alogex. Vietnamese errors. Read `SPRINT-3-DOD.md` deferred.
