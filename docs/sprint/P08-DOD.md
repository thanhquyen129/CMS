# P08 — Auto Exposure từ chứng từ đã khớp DoD

**Ngày:** 2026-09-13  
**PO:** TD6 E09; Received ≠ Accepted ≠ Matched ≠ Recognized; C-003/C-004 · Checklist `PO-GAP-CHECKLIST.md`

## Done
1. `GET /api/document-matches/{id}/exposure-proposals` — đề xuất payable/receivable từ chi tiết khớp active.
2. `POST /api/document-matches/{id}/create-exposures` — tạo exposure gắn Cost/Revenue sẵn có; idempotent (`SourceType=document_match_detail`, `SourceId=detail.Id`).
3. `line_to_cost` → payable; `line_to_revenue` → receivable; `line_to_line` → skip (không invent C/R).
4. UI trên trang phiên khớp: **Xem đề xuất** / **Tạo exposure** + BFF.
5. Guard: tạo exposure không được tăng số Cost/Revenue.

## Verify
- `dotnet test` filter `SprintP08` — 2 passed (line↔cost idempotent; line↔line skip).

## Non-goals (follow-up)
- Auto-create ngay khi add match detail (MVP = CTA tay sau khi khớp).
- Confirm match session (P09).
- Reverse recognize (P10).
