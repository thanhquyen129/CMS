# UI-4 DoD — Documents & AP/AR read (thin)

**Status:** Done  
**Date:** 2026-09-12  
**Plan:** `docs/sprint/PLAN-UI.md` · Prompt: `PROMPT-UI-4.md`

## Outcome
Operator thấy **Nhận ≠ Chấp nhận ≠ Khớp** trên chứng từ; đọc được **AP/AR outstanding** (và exposure) — copy CP6.5; không gộp Cost = Payment.

## Checklist

| # | Criterion | Status |
|---|-----------|--------|
| 1 | Document list/detail + receive/accept qua BFF (API Pass 2 sẵn) | Done |
| 2 | `/ap-ar` + Bill panel: exposure / AP/AR outstanding (read) | Done |
| 3 | Copy CP6.5; empty/loading/error thật; không control giả | Done |
| 4 | This DoD + handoff append + README/orchestration **U4 Done** (Pass UI track complete) | Done |
| 5 | Chỉ `apps/web/**` + docs append; không invent money backend | Done |

## APIs used (existing — Pass 2)
- `GET/POST /api/financial-documents`, `POST …/{id}/accept`
- `GET /api/accounts-payable`, `GET /api/accounts-receivable`
- `GET /api/payable-exposures`, `GET /api/receivable-exposures`
- `GET /api/terminology`
- BFF: `/bff/financial-documents`, `/bff/financial-documents/[id]/accept`

## UI files
- `apps/web/app/documents/**` (list, detail, receive)
- `apps/web/app/ap-ar/**`
- `apps/web/components/DocumentStatusTriad.tsx`, `DocumentAcceptButton.tsx`, `ReceiveDocumentForm.tsx`, `BillDocumentsApArPanel.tsx`
- `apps/web/lib/documents.ts`, `apps/web/lib/ap-ar.ts`
- `AppShell` nav + middleware + Bill detail panel + approval deep-link document

## Non-goals (deferred)
Auto-match engine UI, bank feed, e-invoice provider screens, settlement/payment UI, write-off/recognize from UI.

## Follow-ups
- **API:** `GET /api/financial-documents` thiếu `billId` trên list DTO / query filter — Bill không lọc được chứng từ gắn Bill (UI ghi chú trung thực).
- Match UI (`/api/document-matches`) — start/add detail/reverse.
- Settlement UI (payment/collection allocate/finalize).
- Approve/reject từ hàng đợi; aging report screens đầy đủ.

## Verify
- `npm run build` trong `apps/web` xanh
- VPS (sau merge/deploy): `/documents`, `/ap-ar`, Bill detail AP/AR block, `/health`
