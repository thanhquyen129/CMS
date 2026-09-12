# PROMPT — Pass 2 / Sprint 7 FULL Exposure + AP/AR

Bạn đang làm **Pass 2 — Sprint 7 FULL** trong `c:\A1\git\cms`. Pass 1 S7 + Pass 2 S0–S6 trên `main` (hoặc base gần nhất nếu S6 FULL đang PR song song).

## Mục tiêu TD6 E09 đủ hơn thin
Exposure; AP/AR recognition; partial/multi recognition; aging; outstanding tôn trọng settlement.

## Must ship
1. Partial/multi recognition với `recognized_amount` tracking; reject over-recognize.
2. Aging fields/buckets query trên AP/AR (IDX-007/008).
3. Optional document→exposure link **không** tạo Cost/Revenue (C-003/C-004).
4. Outstanding tôn trọng settlements (finalized only — AC-007).
5. Tests + `SPRINT-7-FULL-DOD.md` + handoff + PR. `dotnet test` xanh.

## Non-goals
Settlement engine changes (Sprint 8 FULL), JWT/OIDC, Next.js aging UI, auto-exposure engine.

## Ràng buộc
Never secrets/alogex. Vietnamese errors. Read `SPRINT-7-DOD.md` deferred.
