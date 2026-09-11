# PROMPT — Sprint 10 Financial Close (paste vào New Chat / Cloud Agent)

Bạn đang làm **Sprint 10 — Financial Close** trong repo `c:\A1\git\cms` (base `main` đã có Sprint 0–9).

## Mục tiêu TD6 Sprint 10
"Close eligibility; snapshot; lock; reopen/reclose" — Epic **E12**.

## DoD mỏng nhưng đủ (Pass 1)
1. Entities + migration TD1 D11: `financial_closes`, `financial_close_snapshots`, `financial_close_snapshot_details` (hoặc tương đương mỏng đủ).
2. **Close immutability (C-010 / AC-008):** snapshot đã chốt không UPDATE/DELETE nghiệp vụ; reopen/reclose tạo version/snapshot mới.
3. Close flow: open close period/scope → eligibility check stub (block nếu exception open critical — optional thin) → create immutable snapshot (hash/reference) → lock.
4. Reopen: không sửa snapshot cũ; tạo close version mới khi reclose.
5. API tenant-scoped: start close; snapshot; reopen; get snapshots; Vietnamese errors.
6. Tests: snapshot immutable after close; reopen keeps history; cross-tenant.
7. `docs/sprint/SPRINT-10-DOD.md`, handoff, PR → main. `dotnet test` xanh.

## Non-goals
Full reporting dashboard (Sprint 11), hardening/UAT (Sprint 12), JWT, Next.js.

## Ràng buộc
UUIDv7, soft-delete on non-snapshot entities; snapshots never hard-deleted. Never secrets/alogex.
