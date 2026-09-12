# UAT G1 — UI tạo Bill / Cost / Revenue / Exposure→Recognize

**Status:** Done  
**Date:** 2026-09-12  
**Source:** `docs/sprint/UAT-VPS-ONE-ROUND.md` gap G1 (blocker UI-only go-live)

## Outcome
Operator hoàn tất bước tạo từ UI (không Postman): **Bill → Cost → Revenue → Exposure → Recognize AP/AR**. Confirm/actualize + settle/close đã có từ U2–U5.

## Checklist

| # | Criterion | Status |
|---|-----------|--------|
| 1 | `/bills/new` + CTA trên danh sách Bill | Done |
| 2 | `/bills/{id}/costs/new` — Cost trực tiếp gắn Bill (lớp Dự kiến) | Done |
| 3 | `/bills/{id}/revenues/new` — Revenue gắn Bill | Done |
| 4 | `/ap-ar/exposures/new` — payable/receivable exposure | Done |
| 5 | `/ap-ar/exposures/{id}/recognize` — ghi nhận → AP/AR (partial OK; C-015 không nhập outstanding) | Done |
| 6 | CTA trên Bill detail + `/ap-ar` (exposure tab Recognize) | Done |
| 7 | Copy CP6.5: Cost≠Payment; Revenue≠Collection; Exposure≠AP/AR; empty/error thật | Done |
| 8 | BFF cookie → API existing (không invent money API) | Done |

## APIs (Pass 2 — unchanged)
- `POST /api/bills`
- `POST /api/costs`, `POST /api/revenues`
- `POST /api/payable-exposures`, `…/recognize`
- `POST /api/receivable-exposures`, `…/recognize`

## BFF
- `/bff/bills`, `/bff/costs`, `/bff/revenues`
- `/bff/payable-exposures`, `/bff/payable-exposures/[id]/recognize`
- `/bff/receivable-exposures`, `/bff/receivable-exposures/[id]/recognize`

## UI files
- Forms: `CreateBillForm`, `CreateCostForm`, `CreateRevenueForm`, `CreateExposureForm`, `RecognizeExposureForm`
- Pages: `bills/new`, `bills/[id]/costs/new`, `bills/[id]/revenues/new`, `ap-ar/exposures/new`, `ap-ar/exposures/[id]/recognize`
- Panels: `BillCostRevenuePanel` (+billId), `BillDocumentsApArPanel`, `bills/page`, `ap-ar/page`

## Non-goals
Shared cost create/allocate UI; write-off; reverse recognize; party pickers; document line (G3).

## Operator path (UI-only vòng mỏng G1)
1. Login → `/bills` → **Tạo Bill**
2. Trên Bill → **Tạo chi phí** / **Tạo doanh thu** → Xác nhận (U2)
3. `/ap-ar` hoặc Bill panel → **Tạo exposure** → **Ghi nhận → AP/AR**
4. Tiếp settle/close (U5) như trước

## Verify
- `npm run build` trong `apps/web`
- VPS: tạo Bill mới từ UI → Cost/Revenue → Exposure → Recognize → thấy outstanding trên `/ap-ar`
