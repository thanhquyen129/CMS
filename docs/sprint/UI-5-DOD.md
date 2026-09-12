# UI-5 DoD — Settlement + Close (thin)

**Status:** Done  
**Date:** 2026-09-12  
**Plan:** `docs/sprint/PLAN-UI.md` · Prompt: `PROMPT-UI-5.md`

## Outcome
Operator **tất toán** AP/AR qua Thanh toán / Thu tiền (nháp → chốt phân bổ); **chốt tài chính** Bill/kỳ với snapshot bất biến + P&L đọc được. Copy CP6.5: Payment ≠ Cost; Collection ≠ Revenue.

## Checklist

| # | Criterion | Status |
|---|-----------|--------|
| 1 | `/settlements` list + create payment/collection + allocate/finalize qua BFF | Done |
| 2 | `/financial-closes` list + start + snapshot + reopen + P&L read | Done |
| 3 | Bill panel CTA settlement + close theo Bill; copy CP6.5; empty/loading/error thật | Done |
| 4 | This DoD + handoff + README/orchestration U5 | Done |
| 5 | Chỉ `apps/web/**` + docs; không invent money backend | Done |

## APIs used (existing — Pass 2 S8/S10)
- `GET/POST /api/payments`, `POST …/{id}/allocations`, `POST /api/payment-allocations/{id}/finalize`
- `GET/POST /api/collections`, allocations + finalize (mirror)
- `GET/POST /api/financial-closes`, `POST …/{id}/snapshot`, `POST …/{id}/reopen`, `GET …/{id}/pnl`
- BFF: `/bff/payments/**`, `/bff/collections/**`, `/bff/payment-allocations/**`, `/bff/collection-allocations/**`, `/bff/financial-closes/**`

## UI files
- `apps/web/app/settlements/**`, `apps/web/app/financial-closes/**`
- `apps/web/lib/settlements.ts`, `apps/web/lib/financial-closes.ts`
- Components: `CreateCashTxnForm`, `AllocateCashForm`, `FinalizeAllocationButton`, `StartFinancialCloseForm`, `CloseSnapshotButton`, `ReopenCloseButton`
- AppShell nav + middleware + BillDocumentsApArPanel CTAs

## Non-goals (deferred)
Bank feed, reverse allocation UI, write-off/recognize from UI, match UI, per-action RBAC beyond JWT.

## Follow-ups
- Reverse allocation UX (API sẵn).
- Write-off / recognize thin UI.
- Match UI (`/api/document-matches`).
- List documents `billId` filter (API).

## Verify
- `npm run build` trong `apps/web`
- VPS (sau deploy): `/settlements`, `/financial-closes`, Bill CTAs, `/health`
