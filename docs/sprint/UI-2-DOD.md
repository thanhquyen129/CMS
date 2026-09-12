# UI-2 DoD — Cost & Revenue confirm on Bill

**Status:** Done  
**Date:** 2026-09-12  
**Plan:** `docs/sprint/PLAN-UI.md` · Prompt: `PROMPT-UI-2.md`

## Outcome
Trên Bill detail: list Chi phí / Doanh thu theo Bill; **Xác nhận** (Expected→Confirmed) và **Ghi nhận Thực tế** (Confirmed→Actual) với dialog tiếng Việt; refresh financial profile sau mutate; không silent overwrite maturity.

## Checklist

| # | Criterion | Status |
|---|-----------|--------|
| 1 | List costs/revenues theo Bill; confirm (+ actualize) + dialog VI (CP6.5/`/api/terminology`) | Done |
| 2 | Refresh profile sau mutate (`router.refresh`); lỗi API tiếng Việt | Done |
| 3 | Respect 403/409 — ẩn/khóa action, không control giả | Done |
| 4 | This DoD + handoff append + README Pass UI + orchestration U2 Done | Done |
| 5 | Chỉ `apps/web/**` (+ docs); không invent backend | Done |

## APIs used (existing)
- `GET /api/costs?billId=` / `GET /api/revenues?billId=`
- `POST /api/costs/{id}/confirm` · `/actualize`
- `POST /api/revenues/{id}/confirm` · `/actualize`
- `GET /api/terminology` · Bill financial-profile (refresh via RSC)

## BFF (cookie → Bearer)
- `/bff/costs/[id]/confirm|actualize`
- `/bff/revenues/[id]/confirm|actualize`

## UI files
- `apps/web/app/bills/[id]/page.tsx`
- `apps/web/components/BillCostRevenuePanel.tsx`
- `apps/web/lib/costs-revenues.ts`, `costs-revenues-server.ts`, `bff-api.ts`, `terminology.ts`
- `apps/web/app/bff/costs/**`, `apps/web/app/bff/revenues/**`

## Non-goals (deferred)
Shared allocation UI, documents, settlement finalize, amount override form (API optional amount — UI dùng số hiện tại).

## Follow-ups (API / product)
- Optional confirmed/actual amount override trong dialog (API đã nhận body).
- Permission catalog chưa có `cost.confirm` / `revenue.confirm` riêng — UI dựa 403 runtime.
- Shared cost không gắn Bill: không hiện trên list theo `billId` (đúng API).

## Verify
- `npm run build` trong `apps/web` xanh
- Không đụng API / `dotnet test`
