# Sprint 2 — Definition of Done (Operational Reference)

TD6: *Operational sync; Bill-centric reference graph; search/drill-down baseline* — Epics **E03 / E14** thinnest operable slice.

## Done (this sprint)

| Item | Status | Notes |
|------|--------|-------|
| Order entity + upsert/list API | Done | `PUT/GET /api/orders`, `GET /api/orders/{id}` |
| Shipment entity + upsert API | Done | `PUT /api/shipments` (depth for Bill graph) |
| `order_bill_links` N:N | Done | `POST /api/orders/{orderId}/bills/{billId}` (idempotent) |
| `bill_shipment_links` N:N | Done | `POST /api/bills/{billId}/shipments/{shipmentId}` (idempotent) |
| Bill graph drill-down | Done | `GET /api/bills/{id}/graph` → orders + shipments |
| Idempotent order upsert | Done | Unique `(tenant_id, source_system, external_id)` C-002 |
| Vietnamese validation/errors | Done | FluentValidation VI + AppException messages |
| Tenant isolation | Done | Existing global filter + `X-Tenant-Id` |
| Migration `Sprint2_OperationalReference` | Done | Does not alter Sprint 0/1 migrations |
| Isolation + idempotency + graph tests | Done | 4 new tests; suite green |
| Sprint 2 DoD doc | Done | This file |

## Deferred (later sprints)

| Item | Target | Reason |
|------|--------|--------|
| `transport_legs` / `transport_movements` + leg/movement bridges | Later | Optional depth beyond Bill↔Shipment |
| Full Order CRUD soft-delete endpoints | Later | Upsert/list sufficient for sync slice |
| Cost / Revenue on Bill | Phase 2+ | Finance path |
| JWT / OIDC | Later | Keep header bootstrap |
| Next.js UI | Later | API-only this sprint |
| Search/index beyond graph | Later | Graph is drill-down baseline |

## Headers (bootstrap until JWT)

| Header | Purpose |
|--------|---------|
| `X-Tenant-Id` | Tenant context (C-001) |
| `X-User-Id` | Actor for permission checks (optional) |
| `X-Correlation-Id` | Request correlation |

## APIs

| Method | Path | Notes |
|--------|------|-------|
| PUT | `/api/orders` | Idempotent upsert by source_system + external_id |
| GET | `/api/orders` | List tenant orders |
| GET | `/api/orders/{id}` | Get order |
| POST | `/api/orders/{orderId}/bills/{billId}` | Link Order↔Bill |
| PUT | `/api/shipments` | Idempotent upsert shipment |
| POST | `/api/bills/{billId}/shipments/{shipmentId}` | Link Bill↔Shipment |
| GET | `/api/bills/{id}/graph` | Bill → orders/shipments read model |

## Verify

```bash
dotnet test Cms.sln -c Release
```
