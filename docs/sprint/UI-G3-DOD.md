# UAT G3 — UI thêm dòng chứng từ

**Status:** Done  
**Date:** 2026-09-12  
**Source:** `docs/sprint/UAT-VPS-ONE-ROUND.md` gap G3

## Outcome
Operator **thêm dòng chứng từ** từ UI trên `/documents/{id}` (không Postman). Dòng mở để khớp; không tạo Cost/Revenue.

## Checklist

| # | Criterion | Status |
|---|-----------|--------|
| 1 | Form thêm dòng trên `/documents/[id]` khi đã nhận + còn hiệu lực | Done |
| 2 | BFF `POST /bff/financial-documents/{id}/lines` → API Pass 2 | Done |
| 3 | Prefill tiền tệ chứng từ + `billId`; amount > 0 | Done |
| 4 | Empty state không còn “Thêm dòng qua API” | Done |
| 5 | Copy CP6.5: dòng ≠ Cost; khớp bắt đầu matched=0 | Done |

## APIs used (existing)
- `POST /api/financial-documents/{id}/lines` (`AddFinancialDocumentLine`)

## UI files
- `apps/web/components/AddDocumentLineForm.tsx`
- `apps/web/app/bff/financial-documents/[id]/lines/route.ts`
- `apps/web/app/documents/[id]/page.tsx`
- `apps/web/lib/documents.ts` (`canAddDocumentLine`)

## Non-goals
Edit/delete line · sum validation vs header total · party pickers · G4–G6

## Verify
- `npm run build` apps/web
- VPS: chứng từ đã nhận → thêm dòng → bảng cập nhật → khớp được
