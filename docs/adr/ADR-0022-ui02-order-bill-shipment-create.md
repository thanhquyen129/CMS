# ADR-0022 — Manual create screens for Order / Bill / Shipment (UI-02)

**Status:** Accepted  
**Date:** 2026-09-21  
**Relates:** H-002, H-003, ADR-0017 (SCP-003), ADR-0018, ADR-0019

## Context

PO UI-02 mockups specify list + create for Order, Bill, and Shipment under **Đơn hàng vận chuyển**. ADR-0017 already put Manual Reference Entry for D03 in baseline. The gap was operable create forms: identity-only upserts could not hold rating/financial context (customer, route, ETD/ETA, cargo notes) that later Cost/Revenue/Rating need.

## Decision

1. Keep Order/Shipment as **operational references** (C-002 identity). Do not add dispatch, GPS, e-POD, or vehicle assignment.
2. Persist a thin **operational context** on Order, Shipment, and Bill: transport mode, origin/destination/route, ETD/ETA, customer (Order/Bill), assignee, description, plus remaining create-form extras in `context_json`.
3. Creating these records **does not** create Cost, Revenue, AP/AR, or ledger facts. Status `draft` = nháp; `active` = dùng để liên kết.
4. TMS/API identity upserts that omit context fields **must not wipe** existing LCMS context (`ApplyContext` only when payload present).

## Consequences

- UI-02 create/list screens are in-product, not a TMS module.
- Rating Engine can later read the same context without a second capture form.
- Extra mockup fields (pickup flags, extra services) live in JSON — queryable columns stay small.

## Alternatives rejected

- Identity-only forms that ignore the mockup (inoperable for standalone tenants).
- A TMS booking/dispatch module (breaks H-002 / ADR-0019).
