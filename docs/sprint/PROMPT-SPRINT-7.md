# PROMPT — Sprint 7 Exposure + AP/AR (paste vào New Chat / Cloud Agent)

Bạn đang làm **Sprint 7 — Exposure + AP/AR** trong repo `c:\A1\git\cms` (base `main` đã có Sprint 0–6).

## Mục tiêu TD6 Sprint 7
"Exposure; AP/AR recognition; partial recognition; outstanding" — Epic **E09**.

## DoD mỏng nhưng đủ (Pass 1)
1. Entities + migration TD1 D08: `payable_exposures`, `receivable_exposures`, `accounts_payable`, `accounts_receivable`.
2. Tách **Exposure ≠ Recognized AP/AR** (CP3/TD4): exposure trước; recognition tạo AP/AR record riêng — không gộp một status.
3. Outstanding **derived** (C-015): không cho user nhập outstanding như SoT; derive từ recognition ± adjustment − finalized settlement (settlement có thể = 0 ở Pass 1).
4. Partial recognition: cho phép nhận một phần số tiền exposure.
5. API tenant-scoped: create/list exposure; recognize → AP/AR; get outstanding; Vietnamese errors.
6. **Không** tạo Cost/Revenue mới khi recognize (C-003/C-004).
7. Tests: exposure≠recognition; outstanding derived; cross-tenant; partial recognize.
8. `docs/sprint/SPRINT-7-DOD.md`, handoff, PR → main. `dotnet test` xanh.

## Non-goals
Payment/Collection settlement (Sprint 8), JWT, Next.js, full aging UI.

## Ràng buộc
UUIDv7, soft-delete, tenant_id, row_version, không hard delete. Never secrets/alogex.
