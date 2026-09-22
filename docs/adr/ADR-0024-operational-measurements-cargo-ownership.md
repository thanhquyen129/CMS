# ADR-0024 — Operational measurements, cargo children, and field ownership

**Status:** Accepted  
**Date:** 2026-09-22  
**Relates:** H-003, ADR-0022, ADR-0023, MASTER 02 / FR-004…009, AC-OR-05…15

## Context

Order, Bill, and Shipment create screens already collect service, incoterm, cargo totals, and rating context. Those values lived only in `context_json`. Confirmed chargeable weight could be overwritten by a later save. A field written by an external system could be changed from LCMS with no reason. Import of a mixed file could leave a partial set of rows.

## Decision

1. Rating and identity fields that the create screens already send are copied onto typed columns (service, incoterm, dates, currency, commodity, carrier, contact, pickup/delivery). `context_json` stays for the remaining notes and flags.
2. Cargo totals are rows in `operational_measurements` (type, quantity, UOM, source channel, confirmed flag). A confirmed chargeable weight is unchanged unless `chargeableOverrideReason` is present and the quantity differs.
3. Packages and containers are child rows (`cargo_packages`, `cargo_containers`) for rating and allocation. The create screens keep the mockup totals; they do not show a line grid.
4. `field_ownerships` records the external system that first wrote `origin_code`. A later LCMS edit of that field is rejected unless an override reason is sent. A resave that does not touch the field is allowed. Manual LCMS creates do not claim ownership.
5. `POST /api/operational-import/preview` returns per-row field errors and `canCommit`. `POST /api/operational-import/commit` writes nothing when any row is invalid. A valid file is one `SaveChanges`. Existing external ids are updated, not duplicated.
6. A new Order–Bill, Bill–Shipment, Bill–leg, Bill–movement, or leg–movement link appends `link.create`. Repeating the same link does not append a second audit row.

## Consequences

- Bill, Order, Shipment, transport leg, and transport movement gain the columns in migration `OperationalCargoAndFieldOwnership`.
- Shipment keeps the unique `(tenant, source, external id)` index.
- Tests use `EnsureCreated`, so the migration SQL is for PostgreSQL only.
