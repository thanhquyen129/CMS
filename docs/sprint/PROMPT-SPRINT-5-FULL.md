# PROMPT — Pass 2 / Sprint 5 FULL Revenue & Profitability

Bạn đang làm **Pass 2 — Sprint 5 FULL** trong `c:\A1\git\cms`. Pass 1 S5 + Pass 2 S0–S4 đã trên `main`.

## Mục tiêu TD6 E07 (+ E13 profile) đủ hơn thin
Revenue lifecycle parity with Cost FULL; profitability views; recognition policy stub.

## Must ship
1. Revenue maturity + adjustments parity with Cost patterns; FX stub `base_amount` when ≠ base currency (same ADR-0004 pattern).
2. Optional approval threshold before confirm (config), reuse Approvals.
3. Financial profile enhancements: Best Available per line; variance Expected vs Actual; allocated cost inclusion; multi-currency separated (never sum raw).
4. `GET /api/bills/{id}/profitability?view=expected|confirmed|actual|best` explicit view param.
5. Single Economic Revenue (C-004) hardened tests (reject document/AR sources).
6. Tests + `SPRINT-5-FULL-DOD.md` + handoff + PR. `dotnet test` xanh (must not drop below current suite).

## Non-goals
Documents/AP/AR changes, Next.js.

## Ràng buộc
Never secrets/alogex. Vietnamese errors. Read `SPRINT-5-DOD.md` deferred.
