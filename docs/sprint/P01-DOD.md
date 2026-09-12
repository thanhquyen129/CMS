# P01 — Shared allocation UI DoD

**Ngày:** 2026-09-12  
**PO:** TD6 E05/E06; C-005/C-006 · Checklist `PO-GAP-CHECKLIST.md`

## Done
1. Màn `/costs/shared` — list chi phí attribution=shared.
2. Tạo shared cost (`/costs/shared/new`) — không gắn Bill.
3. Chi tiết `/costs/shared/[id]`: lịch sử phiên phân bổ; tạo nháp với basis **equal / quantity / manual_ratio**; chọn ≥2 Bill; **chốt** (dialog VI).
4. BFF: `POST /bff/costs/{id}/allocations`, `POST /bff/cost-allocations/{id}/finalize`.
5. API: filter `attributionType` trên `GET /api/costs`; enforce ≥2 Bill khi create/finalize allocation.
6. Nav + dashboard shortcut + link từ Bill cost panel.

## Verify
- `dotnet test` filter Sprint4Full: allocation paths xanh.
- `npm run build` apps/web.

## Non-goals (follow-up)
- Manual override amount trên UI (API đã hỗ trợ).
- Confirm/actualize shared cost trên cùng màn (vẫn qua API; Bill panel cho Direct).
- Eligible-bill rule engine (explicit list đủ MVP).
