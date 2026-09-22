# ADR-0032 — Soft-delete operational relationship links

**Status:** Accepted  
**Date:** 2026-09-22  
**Relates:** ADR-0024 §6, W-K3, H-003

## Context

Order–Bill, Bill–Shipment, Bill–Leg, and Bill–Movement links could be created (`link.create`) but not removed. Wrong links stayed on the Bill financial graph forever.

## Decision

1. `DELETE` endpoints soft-delete the link row (`DeletedAt` / `DeletedBy`) — no hard delete.
2. Audit action `link.remove` records the pair and optional reason.
3. Bill graph exposes `linkId` only for **direct** links. Legs/movements inferred via shipment show `linkId: null` and are not unlinkable from the Bill drawer (honest UX).
4. Re-linking the same pair after unlink creates a new row (unique active filter via soft-delete query filter).

## Consequences

- UI: Bill drawer tab Liên quan shows «Gỡ liên kết» when `linkId` is present.
- Tests: `UnlinkRelationTests`.
