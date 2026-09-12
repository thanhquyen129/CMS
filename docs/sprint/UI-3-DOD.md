# UI-3 DoD — Control desk (Dashboard + Exception/Approval queues)

**Status:** Done  
**Date:** 2026-09-12  
**Plan:** `docs/sprint/PLAN-UI.md` · Prompt: `PROMPT-UI-3.md`

## Outcome
Controller sau login thấy **bảng điều khiển** (tóm tắt + hàng đợi) tiếng Việt CP6.5: ngoại lệ / phê duyệt cần xử lý, link sang Bill khi có id — không control giả.

## Checklist

| # | Criterion | Status |
|---|-----------|--------|
| 1 | `/` → `/dashboard`; `GET /api/dashboard/summary` với nhãn VI từ terminology | Done |
| 2 | `/queues/exceptions`, `/queues/approvals` từ API Pass 2 | Done |
| 3 | Link Bill/object khi có id; empty/loading/error thật | Done |
| 4 | This DoD + handoff append + README/orchestration U3 Done | Done |
| 5 | Chỉ `apps/web/**` (+ docs sprint/handoff); không sửa money path / migrations | Done |

## APIs used (existing — Pass 2)
- `GET /api/dashboard/summary`
- `GET /api/queues/exceptions` (`?overdueOnly=true` optional)
- `GET /api/queues/approvals`
- `GET /api/terminology`

## UI files
- `apps/web/app/page.tsx` (redirect → dashboard)
- `apps/web/app/dashboard/page.tsx` + `loading.tsx`
- `apps/web/app/queues/exceptions/page.tsx` + `loading.tsx`
- `apps/web/app/queues/approvals/page.tsx` + `loading.tsx`
- `apps/web/lib/control-desk.ts`
- `apps/web/components/AppShell.tsx` (nav dashboard + queues)
- `apps/web/middleware.ts`, `apps/web/app/globals.css`

## Non-goals (deferred)
Full P&L reporting, close wizard, SLA automation UI, approve/reject actions on queue (read-only U3).

## Follow-ups
- Deep-link Cost/Revenue/Document từ Approval `objectType`+`objectId` khi U4+ có màn chi tiết.
- Decide Approval từ UI (API đã có Pass 2).
- Variance queue / reconciliation queue UI (API `/api/queues/reconciliations` đã có).

## Verify
- `npm run build` trong `apps/web` xanh
- VPS (sau merge/deploy): login → `/dashboard` counts + mở queues (empty thật nếu chưa có data) + `/health` OK
