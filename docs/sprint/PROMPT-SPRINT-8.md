# PROMPT — Sprint 8 Settlement (paste vào New Chat / Cloud Agent)

Bạn đang làm **Sprint 8 — Settlement** trong repo `c:\A1\git\cms` (base `main` đã có Sprint 0–7).

## Mục tiêu TD6 Sprint 8
"Payment/collection; allocation; reversal; outstanding" — Epic **E10**.

## DoD mỏng nhưng đủ (Pass 1)
1. Entities + migration TD1 D09: `payments`, `collections`, `payment_allocations`, `collection_allocations`.
2. Settlement chỉ đổi outstanding **qua finalized allocation** (AC-007 / C-008) — không sửa AP/AR outstanding như field nhập tay.
3. Partial settlement + unapplied amount; over-allocation bị reject trừ policy stub = không cho over.
4. Reversal/adjustment: tạo allocation reverse hoặc status cancelled — không hard delete, không silent overwrite.
5. API tenant-scoped: create payment/collection; allocate to AP/AR; finalize; reverse; get outstanding reflects settlement; Vietnamese errors.
6. Tests: partial settle; over-allocate rejected; outstanding decreases only after finalize; cross-tenant; C-003/C-004 không tạo Cost/Revenue.
7. `docs/sprint/SPRINT-8-DOD.md`, handoff, PR → main. `dotnet test` xanh.

## Non-goals
Reconciliation/exception (Sprint 9), close (Sprint 10), JWT, Next.js, bank feed.

## Ràng buộc
UUIDv7, soft-delete, tenant_id, row_version. Never secrets/alogex.
