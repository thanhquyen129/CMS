# ADR-0019 — Freight tariff + party autocomplete (not a TMS)

**Status:** Proposed (awaiting PO confirmation)  
**Date:** 2026-09-19  
**Relates:** H-002, H-003, H-012, ADR-0017 (SCP-001/003), ADR-0018

## Context

Khách hàng chưa có Operational System; đang làm tay trên Word/Excel. PO yêu cầu “làm thêm phần TMS”: quản lý khách hàng (autocomplete khi tạo Bill) và quản lý cước (mode, loại hàng, chặng, giá Q-break, giá kg bước 0.5).

Locked guardrails already say LCMS is the financial-control layer and **must not become a TMS** (no dispatch, vehicle planning, GPS, e-POD). ADR-0017 already requires standalone Manual/Import for D02/D04. The listed needs are **master data + rating**, which that baseline already owns — they are the Excel the tenant lives in today, not a transportation execution product.

## Decision

1. **Do not open a TMS module.** Keep H-002 / ADR-0017 SCP-003 / ADR-0018. No dispatch, driver/vehicle assignment, GPS, e-POD, warehouse, empty-truck matching.
2. **Treat the ask as D02 + D04 completion** for standalone tenants:
   - **D02 Party:** canonical `BusinessParty` (already exists) + typeahead on Bill by name/phone/MST/code; snapshot onto Bill/waybill at capture; optional link sender/consignee/payer.
   - **D04 Tariff:** extend rating — do not invent a second price book. Add catalog dimensions (transport mode, cargo type, lane) and two freight calc methods used in VN logistics Excel: **Q-break** (Min/N/Q45/…) and **kg-step** (0.5 kg ladder).
3. **Chargeable weight is a financial input**, computed/rounded before rating: round-up 0.5 kg (configurable per rate card). Volumetric / sea W/M are P1.
4. **Buy ≠ Sell.** Rate card `partyType` customer = sell (Expected Revenue); vendor = buy (Expected Cost). Same Bill may rate both. CostRead ≠ RevenueRead remains.
5. **Rating result stays system-generated and immutable** (C-011). Re-rate supersedes; never silent overwrite. Published version remains immutable.
6. **Excel import of tariff is in-scope for go-live** (AC-SCP-07/09): that file *is* the tenant’s rate SoT today.

## Consequences

- UI copy: “Danh mục khách hàng” / “Bảng cước vận chuyển” — not “TMS”.
- Bill capture gains party typeahead + rate-from-tariff CTA; paper postage entry remains valid when no card matches.
- New catalog kinds: `transport_mode`, `cargo_type`, `lane` (origin–destination). `service_type` stays for freight vs surcharge vs handling.
- Calc methods added: `weight_break` (Q table + pivot-to-next-break), `weight_step` (lookup by rounded kg). Existing `fixed` / `unit_rate` / `percent_of_base` / `min_max_clamp` stay for AWB fee / FSC / min charge.
- Out of scope without a **new** ADR: GPS, routing, fleet, e-POD app, marketplace, official carrier reprint.

## Alternatives rejected

- New TMS product surface — breaks H-002 / SCP-003; ships ops tropes the PO package already excluded.
- Free-text-only RouteCode forever — unusable for autocomplete lanes (SGN-HAN) and Excel import.
- One price per kg with no Q-pivot — undercharges vs commercial air practice (40 kg at N can cost more than 45 kg at Q45).
