# PROMPT — Sprint 6 Financial Documents (paste vào New Chat / Cloud Agent)

Bạn đang làm **Sprint 6 — Financial Documents** trong repo `c:\A1\git\cms` (base `main` đã có Sprint 0–5).

## Mục tiêu TD6 Sprint 6
"Document intake/acceptance; line detail; N:N matching" — Epic **E08**.

## DoD mỏng nhưng đủ (Pass 1)
1. Entities + migration TD1 D07: `financial_documents`, `financial_document_lines`, `document_matches`, `document_match_details`.
2. Tách trạng thái: **Received ≠ Accepted ≠ Matched** (C-005 document separation / AC-005) — không gộp một status duy nhất.
3. API tenant-scoped: create/receive document; accept; add lines; start match; match detail N:N (line↔line hoặc line↔cost/revenue stub link); Vietnamese errors.
4. Matching: tổng matched_amount không vượt open amount (C-007) trừ tolerance stub = 0.
5. **Không** tạo Cost/Revenue mới chỉ vì nhận chứng từ (C-003/C-004) — chỉ link/match.
6. Tests: state separation; over-match rejected; cross-tenant isolation.
7. `docs/sprint/SPRINT-6-DOD.md`, handoff, PR → main. `dotnet test` xanh.

## Non-goals (Pass 2 / later sprints)
Full AP/AR recognition (Sprint 7), settlement, e-invoice providers, JWT, Next.js.

## Ràng buộc
UUIDv7, soft-delete, tenant_id, row_version, không hard delete. Never secrets/alogex.
