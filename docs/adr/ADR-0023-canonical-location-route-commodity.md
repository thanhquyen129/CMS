# ADR-0023 — Canonical location, route, commodity, and party snapshot

**Status:** Accepted  
**Date:** 2026-09-22  
**Relates:** H-002, H-003, ADR-0017, ADR-0022, MASTER 02 / FR-001…003

## Context

`master_catalog` stored location and route as JSON attributes. Rating, import, and search need one place code (plus IATA, UN/LOCODE, and source aliases). A Bill must keep the party facts that were true when the role was assigned; later master edits must not rewrite that row. Required Bill roles are a tenant policy, not a fixed three-party shape.

## Decision

1. Canonical tables: `locations`, `location_aliases`, `routes`, `route_stops`, `commodity_types`, `operational_party_snapshots`.
2. Once a tenant has any location, origin and destination must resolve to code, IATA, UN/LOCODE, alias, or id. An empty catalog still accepts free text.
3. A route is optional. Origin and destination may be stored directly. Intermediate stops are `route_stops`. Origin and destination must differ.
4. Bill roles with foreign keys: customer, payer, shipper, consignee, bill_to. Policy JSON on `tenant_settings.bill_party_policy_json` lists which of those are required and whether walk-in snapshots are allowed. Default is none required.
5. A snapshot is immutable. A later capture sets `superseded_at` on the previous current row. Master party edits do not update the snapshot.
6. Existing catalog rows of kind `location` and `transport_route` are copied once by migration `CanonicalReferenceMasters`. The catalog rows stay for other kinds.

## Consequences

- Bill and Order gain location and route foreign keys. Display codes remain `origin_code`, `destination_code`, `route_code`.
- PATCH Bill context writes role foreign keys only when `applyPartyRoles` is true.
- SQLite tests do not run the copy SQL (`EnsureCreated`).

## Rejected

- More `master_catalog` JSON for places and commodities.
- Hard-coded shipper + consignee + customer on every Bill.
- Mutable snapshot rows updated in place when the party master changes.
