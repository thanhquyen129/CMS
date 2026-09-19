# ADR-0018 — Bill waybill profile (not a TMS)

**Status:** Accepted (Architecture Agreed — 2026-09-19)  
**Date:** 2026-09-19  
**Relates:** H-002, H-003, ADR-0017 (SCP-003)

## Context

A real Vietnam Post *vận đơn* (sender, consignee, parcel, postage, COD, accepting office) is the document finance/ops staff actually hold. PO asked to implement a “full TMS module” from that paper. Locked guardrails already say LCMS is the financial-control layer and **must not become a TMS** (no dispatch, vehicle planning, GPS, e-POD workflow). ADR-0017 already put D03 Manual Reference Entry in baseline **without** TMS surface.

## Decision

1. Treat the paper waybill as the **Bill Financial Anchor capture form** (H-003), not a transportation execution product.
2. Persist a 1:1 **`bill_waybills`** profile on Bill: party snapshots, parcel/weight, postage breakdown, COD collect (fiduciary — not automatic revenue), accepting/POD evidence datetimes.
3. Postage component amounts seed **Expected Direct Cost** (default) or **Expected Revenue** when the tenant marks the economic role as carrier-side. Totals on the paper are documentary; they must not create a second economic line (no double count).
4. Optionally upsert a thin D03 **Shipment** + `bill_shipment_links` with the tracking/waybill number. Still not dispatch/tracking.
5. **Out of scope (new ADR required):** GPS, routing, driver/vehicle assignment, e-POD app, rate shopping, VNPost connector, official form reprint with carrier branding.

## Consequences

- Standalone tenant can enter a VNPost-class waybill and continue cost → document → AP/AR on the same Bill.
- Charge figures on GET are gated: CostRead when economic role is cost; RevenueRead when revenue. COD collect is labelled “thu hộ — chưa phải doanh thu”.
- H-002 remains: Operational System is SoT when integrated; this profile is LCMS manual/import capture.

## Alternatives rejected

- New TMS module (dispatch/tracking) — breaks H-002 / ADR-0017 SCP-003.
- Putting postage only on Shipment — mixes operational reference with the financial envelope.
- Auto-creating Revenue from COD — COD is collection/fiduciary, not Single Economic Revenue.
