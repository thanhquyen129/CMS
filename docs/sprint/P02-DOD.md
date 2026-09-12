# P02 — Adjust Cost/Revenue + số Confirm/Actual DoD

**Ngày:** 2026-09-12  
**PO:** TD6 E05/E07; C-009 · Checklist `PO-GAP-CHECKLIST.md`

## Done
1. Dialog **Xác nhận / Ghi nhận Thực tế** cho phép nhập số lớp đích (Bill panel + detail).
2. Dialog **Điều chỉnh**: loại adjustment|reversal, delta ≠ 0, lý do bắt buộc, ngày hiệu lực tuỳ chọn; preview số sau.
3. Lịch sử adjustments trên `/costs/[id]`, `/revenues/[id]`, `/costs/shared/[id]`; link **Lịch sử** từ Bill panel.
4. BFF: `POST /bff/costs|revenues/{id}/adjustments`.

## Verify
- `npm run build` apps/web OK (routes costs/revenues detail + BFF adjustments).

## Non-goals
- Approval gate trên adjust lớn (P03).
- Dated asOf adjustment events (P17).
