# PROMPT — UI-1 Bill hub (paste vào New Chat / Cloud Agent)

Base: `main` đã có **UI-0** (shell + login + proxy). Đọc `docs/sprint/PLAN-UI.md`.

## Mục tiêu
Ops/Finance: tìm Bill → mở **Financial Profile** (Expected / Confirmed / Actual) + profitability — màn Bill-centric đọc được trên VPS.

## DoD mỏng nhưng đủ
1. `/bills` — list + search (`GET /api/bills?q=`).
2. `/bills/[id]` — financial profile + profitability (`GET .../financial-profile`, `.../profitability`). Maturity layers scannable; số tiền format VI.
3. Labels từ terminology; empty/error/loading thật. Một drill-down rõ: list → detail.
4. `docs/sprint/UI-1-DOD.md` + handoff + PR. Không fake totals.

## Non-goals
Cost/Revenue mutate (U2), documents, queues, charts nặng.
