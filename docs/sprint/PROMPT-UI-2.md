# PROMPT — UI-2 Cost & Revenue actions (paste vào New Chat / Cloud Agent)

Base: UI-0 + UI-1 trên `main`. Đọc `docs/sprint/PLAN-UI.md`.

## Mục tiêu
Trên Bill detail: **xác nhận Chi phí** và **xác nhận Doanh thu** (CTA chính rõ; maturity không silent overwrite).

## DoD mỏng nhưng đủ
1. List costs/revenues theo Bill; actions confirm (và actualize nếu API đã có) với confirm dialog VI.
2. Refresh profile sau mutate; hiện lỗi API VI.
3. Respect disabled states khi API 403/409; không control giả.
4. `docs/sprint/UI-2-DOD.md` + handoff + PR.

## Non-goals
Shared allocation UI phức tạp, documents, settlement finalize.
