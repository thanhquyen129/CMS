# UAT G1 — UI tạo Bill / Cost / Revenue / Exposure→Recognize

**Status:** Done (FULL operable create path)  
**Date:** 2026-09-12  
**Source:** `docs/sprint/UAT-VPS-ONE-ROUND.md` gap G1 (blocker UI-only go-live)

## Outcome
Operator hoàn tất **tạo + xác nhận có chỉnh số + exposure gắn cost/revenue + recognize** từ UI (không Postman). Settle/close = U5; document line = G3.

## Checklist

| # | Criterion | Status |
|---|-----------|--------|
| 1 | `/bills/new` + CTA trên danh sách Bill | Done |
| 2 | `/bills/{id}/costs/new` — Cost trực tiếp gắn Bill (lớp Dự kiến) | Done |
| 3 | `/bills/{id}/revenues/new` — Revenue gắn Bill | Done |
| 4 | `/ap-ar/exposures/new` — payable/receivable; prefill bill/amount; gắn cost/revenue | Done |
| 5 | `/ap-ar/exposures/{id}/recognize` — partial OK; prefill hạn; C-015 không nhập outstanding | Done |
| 6 | CTA trên Bill detail + `/ap-ar` (+ Exposure từ dòng Cost/Revenue) | Done |
| 7 | Confirm/Actual **có chỉnh số** (Expected ≠ Confirmed như vòng UAT) | Done |
| 8 | Copy CP6.5; empty/error thật; BFF → API sẵn có | Done |

## Thin → Full (lượt này)
| Trước (thin) | Full |
|--------------|------|
| Confirm body `{}` | `confirmedAmount` / `actualAmount` trong dialog |
| Exposure không gắn Cost/Revenue | Select/UUID `costId`/`revenueId` + prefill amount |
| Recognize không prefill hạn | `defaultDueDate` từ exposure |
| CTA exposure chỉ từ AP/AR | Thêm nút Exposure trên từng dòng Cost/Revenue |

## Non-goals (vẫn ngoài G1)
Shared cost create/allocate UI · party pickers · write-off · reverse recognize · document line (G3).

## Operator path (UI-only — parity UAT create)
1. `/bills` → **Tạo Bill**
2. **Tạo chi phí** (1.000.000) → **Xác nhận** chỉnh 1.100.000 → **Tạo doanh thu** → xác nhận
3. Dòng Cost → **Exposure** (gắn cost) → **Ghi nhận → AP**; tương tự Revenue → AR
4. Settle/close (U5); chứng từ (G2/G3)

## Verify
- `npm run build` apps/web
- VPS: Expected≠Confirmed trên UI; exposure gắn cost; recognize có hạn
