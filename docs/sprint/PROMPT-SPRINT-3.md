# PROMPT — Sprint 3 Rate & Pricing (paste vào New Chat / Cloud Agent)

Bạn đang làm **Sprint 3 — Rate & Pricing** trong repo `c:\A1\git\cms` (base `main` đã có Sprint 0–2).

## Mục tiêu TD6 Sprint 3
"Rate card/version/rules/components; rating; snapshot; rerating" — Epic **E04**.

## DoD mỏng nhưng đủ
1. Entities + migration theo TD1 D04 (tối thiểu): `rate_cards`, `rate_versions`, `pricing_rules`, `pricing_rule_components`, `ratings`, `rating_details`.
2. Quy tắc: published `rate_version` **immutable**; version mới thay vì sửa mất lịch sử (C-011).
3. API tenant-scoped (`X-Tenant-Id`):
   - CRUD/list rate cards
   - Create rate version (draft → publish)
   - Add pricing rules/components trên version (chỉ draft)
   - `POST /api/ratings` — chạy rating mỏng: input Bill/context → tạo `ratings` + `rating_details` snapshot (Expected cost seed số học đơn giản OK)
4. Tests: publish làm immutable; tenant isolation; rating tạo snapshot.
5. `docs/sprint/SPRINT-3-DOD.md` + `docs/handoff.md` + commit + **PR vào main** (cloud branch) hoặc push theo convention repo.
6. `dotnet test` xanh. Vietnamese validation/errors. UUIDv7, soft-delete, row_version, tenant_id.

## Non-goals
Full formula engine phức tạp, Cost lifecycle (Sprint 4), JWT, Next.js.

## Ràng buộc
Không phá migration Sprint 0–2. Không hard delete. Không secret trong git. Never alogex.
