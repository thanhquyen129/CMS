# PROMPT — Pass 2 / Sprint 2 FULL Operational Reference

Bạn đang làm **Pass 2 — Sprint 2 FULL** trong `c:\A1\git\cms`. Pass 1 S2 + Pass 2 S0–S1 đã trên `main`.

## Mục tiêu TD6 E03 đủ hơn thin
Bill-centric operational graph đủ depth: legs/movements bridges, sync idempotency, search.

## Must ship
1. Entities + migration: `transport_legs`, `transport_movements`, bridges `bill_leg_links`, `leg_movement_links`, `bill_movement_links` (TD1 D03) — UUIDv7, tenant_id, soft-delete, row_version.
2. APIs: upsert leg/movement (external identity C-002); link to bill/shipment/leg; expand `GET /api/bills/{id}/graph` with legs/movements.
3. Search: `GET /api/bills?q=` or `/api/search/operational?q=` — search bill_no / external_id / order external_id within tenant.
4. Idempotent upserts; JWT + data scope respect from Pass 2 S1 where listing.
5. Tests: isolation; idempotency; graph includes new nodes; search.
6. `SPRINT-2-FULL-DOD.md` + handoff + PR → main. `dotnet test` xanh.

## Non-goals
GPS/e-POD TMS tropes, Cost/Revenue changes, Next.js.

## Ràng buộc
Never secrets/alogex. Vietnamese errors. Read `SPRINT-2-DOD.md` deferred.
