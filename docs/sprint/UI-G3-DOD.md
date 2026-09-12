# UAT G3 — UI thêm dòng chứng từ

**Status:** Done (FULL operable add-line path)  
**Date:** 2026-09-12  
**Source:** `docs/sprint/UAT-VPS-ONE-ROUND.md` gap G3

## Outcome
Operator **thêm dòng chứng từ** từ UI trên `/documents/{id}` (không Postman), đủ để khớp parity vòng UAT. Dòng mở để khớp; không tạo Cost/Revenue.

## Checklist

| # | Criterion | Status |
|---|-----------|--------|
| 1 | Form thêm dòng trên `/documents/[id]` khi đã nhận + còn hiệu lực | Done |
| 2 | BFF `POST /bff/financial-documents/{id}/lines` → API Pass 2 | Done |
| 3 | Prefill tiền tệ + `billId` + **số còn theo header** (UAT: dòng = total) | Done |
| 4 | Empty state không còn “Thêm dòng qua API” | Done |
| 5 | Copy CP6.5: dòng ≠ Cost; khớp bắt đầu matched=0 | Done |
| 6 | Metric tổng chứng từ / tổng dòng / còn header / tổng mở | Done |
| 7 | Bảng hiện loại (cost/revenue code) + link Bill | Done |
| 8 | Cảnh báo mềm khi tổng dòng vượt header (API không ép) | Done |
| 9 | CTA khi đã accept nhưng chưa có dòng mở → thêm dòng trước khớp | Done |

## Thin → Full (lượt này)

| Trước (thin) | Full |
|--------------|------|
| Amount trống | Prefill `max(0, total − Σ lines)` |
| Không đối chiếu header | Metric + note lệch tổng |
| Bảng chỉ amount/matched/open | + loại + Bill |
| Match empty “nhận thêm dòng” | CTA **Thêm dòng** về detail |
| Không báo sau submit | Success + soft over-total |

## APIs used (existing)
- `POST /api/financial-documents/{id}/lines` (`AddFinancialDocumentLine`)

## UI files
- `apps/web/components/AddDocumentLineForm.tsx`
- `apps/web/app/bff/financial-documents/[id]/lines/route.ts`
- `apps/web/app/documents/[id]/page.tsx`
- `apps/web/lib/documents.ts` (`canAddDocumentLine`, `documentLineCoverage`)
- Match CTAs: `match/page.tsx`, `AddMatchDetailForm`

## Non-goals (vẫn ngoài G3)
Edit/delete line · party pickers · ép API sum=header · G4–G6

## Operator path (UI-only — parity UAT chứng từ)
1. Nhận chứng từ (gắn Bill, total = confirmed cost)
2. **Thêm dòng** (prefill = total) → Accept → Khớp dòng↔cost
3. Settle/close (U5)

## Verify
- `npm run build` apps/web
- VPS: nhận → thêm dòng (số = header) → accept → khớp
