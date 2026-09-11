# Sprint 2 FULL — Definition of Done (Pass 2 Operational Reference)

Pass 1 Sprint 2 delivered thin Order/Shipment + Bill graph (orders/shipments only).
Pass 2 closes TD6 E03 depth: transport legs/movements bridges, sync idempotency, search + Data Scope.

## Done

| Item | Status | Notes |
|------|--------|-------|
| `transport_legs` + C-002 unique | Done | Shipment 1:N; upsert `PUT /api/transport-legs` |
| `transport_movements` + C-002 unique | Done | Upsert `PUT /api/transport-movements` |
| `bill_leg_links` | Done | Idempotent link Bill↔Leg |
| `leg_movement_links` | Done | Idempotent link Leg↔Movement |
| `bill_movement_links` | Done | Idempotent link Bill↔Movement |
| Expand `GET /api/bills/{id}/graph` | Done | + Legs + Movements (bridges ∪ shipment legs ∪ leg movements) |
| `GET /api/search/operational?q=` | Done | bill_no / bill external_id / order external_id (+ order_no) |
| `GET /api/bills?q=` | Done | Same search filter on list; Data Scope from S1 |
| JWT + Data Scope on search/list | Done | `bill.read` + own/organization/all |
| Vietnamese errors + UI terms | Done | VI FluentValidation / AppException; TRANSPORT_* terms |
| Migration | Done | `Sprint2Full_TransportLegsMovements` |
| Tests | Done | `Sprint2FullOperationalReferenceTests` (4); suite **60 passed** |
| DoD + handoff | Done | This file |

## APIs (delta)

| Method | Path | Notes |
|--------|------|-------|
| PUT | `/api/transport-legs` | Idempotent upsert (C-002) |
| PUT | `/api/transport-movements` | Idempotent upsert (C-002) |
| POST | `/api/bills/{billId}/legs/{legId}` | Link Bill↔Leg |
| POST | `/api/transport-legs/{legId}/bills/{billId}` | Alternate link path |
| POST | `/api/transport-legs/{legId}/movements/{movementId}` | Link Leg↔Movement |
| POST | `/api/bills/{billId}/movements/{movementId}` | Link Bill↔Movement |
| POST | `/api/transport-movements/{movementId}/bills/{billId}` | Alternate link path |
| GET | `/api/bills/{id}/graph` | Orders + Shipments + Legs + Movements |
| GET | `/api/search/operational?q=` | Operational search within tenant |
| GET | `/api/bills?q=` | Bill list with optional search |

## Deferred / follow-ups

| Item | Target |
|------|--------|
| GPS / e-POD / TMS tropes | Out of scope (non-goal) |
| Full-text / search index | Later (ILIKE substring OK) |
| Soft-delete endpoints for Order/Leg | Later |
| Next.js operational UI | Later Pass 2 |
| Data Scope on Order/Shipment lists | Later (Bill search scoped) |

## Verify

```bash
dotnet test Cms.sln -c Release
```
