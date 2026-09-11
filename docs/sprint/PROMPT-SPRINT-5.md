# PROMPT — Sprint 5 Revenue & Profitability (paste vào New Chat / Cloud Agent)

Bạn đang làm **Sprint 5 — Revenue & Profitability** trong repo `c:\A1\git\cms` (base `main` đã có Sprint 0–4, gồm Cost).

## Mục tiêu TD6 Sprint 5
"Revenue lifecycle; actual recognition; profitability read model" — Epic **E07** (+ Bill financial profile mỏng từ E13 nếu cần cho drill-down).

## DoD mỏng nhưng đủ
1. Hoàn thiện `revenues` TD1: bill_id bắt buộc (Bill-attributable), financial_maturity expected/confirmed/actual, amount+currency, customer_party_id optional, record_status, approval_status stub, effective_date, row_version.
2. `revenue_adjustments` — không silent overwrite (cùng pattern Cost).
3. Maturity Expected→Confirmed→Actual không overwrite lớp trước (C-009); Single Economic Revenue (C-004) — không tạo revenue mới chỉ vì document/AR.
4. API tenant-scoped: create/list/get revenue; confirm/actualize; adjust; Vietnamese errors.
5. Read model mỏng: `GET /api/bills/{id}/financial-profile` — Cost + Revenue theo maturity (Best Available: Actual→Confirmed→Expected), profit = revenue − cost cùng view; **không** lưu derived totals như SoT trên Bill (TD1-DB-003/004).
6. Tests: maturity no-overwrite; cross-tenant; profile aggregates; currency không cộng raw khác loại (reject hoặc tách theo currency_code).
7. Migration `Sprint5_Revenue`, `docs/sprint/SPRINT-5-DOD.md`, handoff, PR → main.
8. `dotnet test` xanh.

## Non-goals
Financial documents/AP/AR (Sprint 6–7), settlement, JWT, Next.js, full dashboard.

## Ràng buộc
UUIDv7, soft-delete, tenant_id, không hard delete. Bill = Financial Anchor. Never secrets/alogex.
