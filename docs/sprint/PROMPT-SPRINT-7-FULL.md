# PROMPT — Pass 2 / Sprint 7 FULL Exposure + AP/AR

Bạn đang làm **Pass 2 — Sprint 7 FULL** trong `c:\A1\git\cms`. Pass 1 S7 + Pass 2 S0–S6 đã trên `main`.

## Mục tiêu TD6 E09 đủ hơn thin
Exposure → Recognition đầy đủ hơn: partial/multi recognition, aging fields, link từ document match (không tạo Cost/Revenue), outstanding derived với settlement.

## Must ship
1. Multiple partial recognitions against one exposure until fully recognized; track `recognized_amount` on exposure.
2. Aging fields on AP/AR: `due_date`, `days_overdue` derived (or computed in query); list aging buckets stub (`current`/`1-30`/`31-60`/`60+`).
3. Optional: create exposure/AP from accepted+matched document lines (link only — still C-003/C-004).
4. Outstanding reflects Sprint 8 finalized settlements already on main.
5. Tests: partial recognize; over-recognize rejected; aging query; tenant isolation; no Cost on recognize.
6. `SPRINT-7-FULL-DOD.md` + handoff + PR. `dotnet test` xanh (không regress).

## Non-goals
New settlement engine (already S8); Next.js aging UI.

## Ràng buộc
Never secrets/alogex. Vietnamese errors. Read `SPRINT-7-DOD.md` deferred.
