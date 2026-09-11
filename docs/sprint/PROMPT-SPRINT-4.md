# PROMPT — Sprint 4 Cost (paste vào New Chat / Cloud Agent)

Bạn đang làm **Sprint 4 — Cost** trong repo `c:\A1\git\cms` (base `main` đã có Sprint 0–3, gồm Rate/Rating).

## Mục tiêu TD6 Sprint 4
"Cost lifecycle; actualization; adjustment; allocation" — Epics **E05, E06**.

## DoD mỏng nhưng đủ
1. Hoàn thiện `costs` theo TD1 baseline (không stub): financial_maturity (expected/confirmed/actual), attribution_type (direct/shared), amount+currency, bill_id nullable cho shared, record_status, approval_status stub, effective_date, row_version.
2. `cost_adjustments` — không silent overwrite; adjustment/reversal tạo bản ghi mới (C-009/C-013).
3. Allocation mỏng: `cost_allocations` + `cost_allocation_details`; finalize chỉ khi basis hợp lệ; conservation SUM(details)=allocatable sau rounding (C-005/C-006).
4. Maturity: Expected→Confirmed→Actual qua transition có audit fields; **không** overwrite số maturity trước.
5. Rating → Expected Cost: từ `ratings`/`rating_details` (Sprint 3) tạo Cost Expected (idempotent theo source).
6. API tenant-scoped: create/list/get cost; confirm/actualize; adjust; allocate/finalize; Vietnamese errors.
7. Tests: maturity no-overwrite; allocation conservation; cross-tenant isolation; Single Economic Cost (không tạo cost mới chỉ vì allocation).
8. Migration `Sprint4_Cost`, `docs/sprint/SPRINT-4-DOD.md`, handoff, PR → main.
9. `dotnet test` xanh.

## Non-goals
Revenue (Sprint 5), Documents/AP/AR, JWT, Next.js, full approval workflow.

## Ràng buộc
UUIDv7, soft-delete, tenant_id, không hard delete. Bill = Financial Anchor. Never secrets/alogex.
