# UAT G3 — UI thêm dòng chứng từ (+ API integrity)

**Status:** Done (FULL + ADR-0012 integrity)  
**Date:** 2026-09-12  
**Source:** `docs/sprint/UAT-VPS-ONE-ROUND.md` gap G3 · `docs/adr/ADR-0012-document-line-integrity.md`

## Outcome
Operator thêm/sửa/xóa dòng, chọn đối tác lúc nhận, và Accept chỉ khi `Σ dòng = tổng chứng từ`.

## Checklist

| # | Criterion | Status |
|---|-----------|--------|
| 1–9 | G3 FULL trước (add + coverage + match CTA) | Done |
| 10 | `PUT/DELETE …/lines/{lineId}` + BFF + UI Sửa/Xóa | Done |
| 11 | Accept gate `Σ = TotalAmount` (ADR-0012) | Done |
| 12 | Draft: `Σ ≤ Total`; sau Accept khóa add/delete/đổi số | Done |
| 13 | Party picker trên nhận chứng từ; validate party active | Done |
| 14 | Tests `DocumentLineIntegrityTests` + Sprint6/10 order | Done |

## Non-goals
Party trên từng dòng · sửa header total từ UI · G5–G6

## Verify
- `dotnet test` DocumentLineIntegrity + Sprint6*
- `npm run build` apps/web
- VPS: nhận (+đối tác) → thêm dòng đủ tổng → sửa/xóa → Accept → khớp
