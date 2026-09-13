# P09 — Confirm match session + auto-suggest DoD

**Ngày:** 2026-09-13  
**PO:** TD6 E08; ADR-0005; C-007 · Checklist `PO-GAP-CHECKLIST.md`

## Done
1. `POST /api/document-matches/{id}/confirm` — draft → confirmed (≥1 active detail); audit `document_match.confirm`; khóa thêm chi tiết.
2. `GET /api/document-matches/{id}/suggestions` — đề xuất ứng viên |Δ| ≤ dung sai phiên (cost/revenue/line theo method); **không auto-apply**.
3. UI phiên khớp: **Xác nhận phiên khớp** (primary) + **Xem đề xuất** trong dung sai; đảo/hủy vẫn được sau confirm.
4. BFF confirm + suggestions.

## Verify
- `dotnet test` filter `SprintP09` — 3 passed.

## Non-goals (follow-up)
- Auto-match engine / auto-apply suggestions.
- Tenant DB tolerance (P19/P20).
- Gate P08 create-exposures bắt buộc confirmed (vẫn cho draft).
