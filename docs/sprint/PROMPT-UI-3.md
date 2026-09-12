# PROMPT — UI-3 Control desk (paste vào New Chat / Cloud Agent)

Base: UI-0–U2 trên `main`. Đọc `docs/sprint/PLAN-UI.md`.

## Mục tiêu
Dashboard tóm tắt + hàng đợi Exception / Approval — controller thấy việc cần xử lý.

## DoD mỏng nhưng đủ
1. `/` hoặc `/dashboard` — `GET /api/dashboard/summary`.
2. Queues: `/queues/exceptions`, `/queues/approvals` từ API tương ứng.
3. Link sang Bill/object khi có id. Empty states thật.
4. `docs/sprint/UI-3-DOD.md` + handoff + PR.

## Non-goals
Full P&L reporting, close wizard, SLA automation UI.
