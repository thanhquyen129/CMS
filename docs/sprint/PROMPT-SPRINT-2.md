# PROMPT — Sprint 2 Operational Reference (paste vào New Chat)

Bạn đang làm **Sprint 2 — Operational Reference** trong repo `c:\A1\git\cms` (nhánh `main`).

## Đã xong trước đó
- Sprint 0: foundation, observability, terminology, deploy
- Sprint 1: User/Role/Permission, Org/Party/Currency, Bill permission gate
- Có sẵn: Tenant, Bill (Financial Anchor), tenant filter, soft-delete, UUIDv7

## Mục tiêu TD6 Sprint 2
"Operational sync; Bill-centric reference graph; search/drill-down baseline" — Epics **E03, E14**.

## DoD mỏng nhưng đủ
1. Entities + migration: `orders`, `shipments` (optional depth), bridge `order_bill_links`, `bill_shipment_links` theo TD1 (N:N).
2. CQRS/API tenant-scoped:
   - CRUD/list Order (external_id + source_system — C-002 uniqueness)
   - Link Order↔Bill, Bill↔Shipment
   - `GET /api/bills/{id}/graph` — drill-down Bill → orders/shipments (read model mỏng)
3. Idempotent upsert Order by `(tenant_id, source_system, external_id)`.
4. Tests: cross-tenant isolation; duplicate external id rejected; bill graph returns linked refs.
5. `docs/sprint/SPRINT-2-DOD.md` + cập nhật `docs/handoff.md` + commit + push.

## Non-goals
Cost/Revenue logic, JWT, Next.js, full transport_legs/movements (có thể stub hoặc hoãn nếu quá lớn — ghi Deferred).

## Ràng buộc
Diff nhỏ, FluentValidation tiếng Việt, không hard delete, không phá migration cũ. `dotnet test` phải xanh.
