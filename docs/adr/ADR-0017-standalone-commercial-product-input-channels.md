# ADR-0017 — Standalone commercial product & input channels

**Status:** Accepted (PO Confirmed / Architecture Agreed — 2026-09-18)  
**Date:** 2026-09-18  
**Sources:** `docs/po/Baseline_fix_bo_sung/LCMS_Standalone_Commercial_Product_Architecture_Addendum_v1.0.docx`,  
`docs/po/Baseline_fix_bo_sung/LCMS_Standalone_Input_Source_Implementation_Fix_Matrix_v1.0.xlsx`

## Context

BA/TD locked Operational System as Operational SoT (H-002) and LCMS ≠ TMS. That left an operable gap: a new Tenant without TMS/ERP/CRM/Rate/Bank connectors could not run core LCMS unless external systems existed. PO issued a baseline **addendum/correction** (not a TD rewrite): LCMS must ship as a **standalone commercial product**.

## Decision

1. **SCP-001:** LCMS is a standalone commercial product. Absence of an external connector must not lock core flows.
2. **SCP-002:** Every in-scope Business Object uses **Manual Entry / Import / API-Integration** as input channels according to object class, Tenant capability, and SoT policy — same canonical application commands/validation (no DB shortcuts).
3. **SCP-003:** D03 Operational References support **Manual Reference Entry** (+ Import/API) for Order/Bill/Shipment/Leg/Movement + relationships, with LIST/SEARCH/DETAIL/RELATIONSHIP/CROSS-NAV — **without** becoming a TMS (no dispatch/planning/tracking workflows).
4. **SCP-004:** Derived/control/system objects (allocation, recognition, variance, close snapshot, rating result, audit/integration records, …) are **not** free CRUD.
5. **SCP-005:** An external system is SoT **only when** the Tenant configures/integrates it; conflict/ownership policy must enforce no silent overwrite. Without that source, LCMS manual/import remains valid.

Object class rules (A reference/master, B economic, C derived/control, D snapshot/audit) and domain adjustments D01–D12 follow the Addendum tables. Acceptance: **AC-SCP-01…10**.

**P0 fix domains:** D02, D03, D04, D09 + shared input-source framework.  
**Out of scope:** expanding into TMS/ERP product surface (no new ADR for that expansion).

## Consequences

- Team must complete Manual/Import/API + provenance (`source_type` / `source_system` / external identity / import batch) for P0 objects; connectors remain optional adapters.
- Prior “Order/Shipment CRUD in CMS out of scope (H-002)” is **narrowed**: H-002 still forbids TMS behavior; **Manual Reference Entry** for D03 is now **in baseline**.
- Financial go-live for Tenants without Ops integration becomes an explicit product AC (AC-SCP-01/02/06).
- Integration extract/webhook/orchestrator stay **Tenant-optional**; D12 records remain system-generated, not a gate for core UI.

## Alternatives rejected

- Require live Operational System before D03 UI/API completeness.
- Fork business model by Small/Medium/Large instead of Tenant capability flags.
- Free CRUD for derived/control facts to “speed up” standalone demos.
