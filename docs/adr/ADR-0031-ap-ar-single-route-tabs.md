# ADR-0031 — AP/AR single route with tabs

**Status:** Accepted  
**Date:** 2026-09-22  
**Relates:** ADR-0006, UI-07, UI-08, W-L3, H-003

## Context

PO mockups UI-07 (Công nợ phải trả / AP) and UI-08 (Công nợ phải thu / AR) are separate full-page PNGs. The shipped product uses one desk at `/ap-ar` with `?tab=ap|ar` (plus exposure / aging sub-routes). Wave 2 item W-L3 asked whether to split into full-page routes matching the PNGs or keep the tabbed desk.

## Decision

Keep **`/ap-ar?tab=ap|ar`** as the primary AP/AR surface. Do **not** add separate top-level `/ap` and `/ar` full-page routes solely for PNG parity.

Rationale:

1. **One financial desk** — AP and AR share aging, exposure, recognition, and settlement drill-downs; one shell reduces duplicate chrome and conflicting filters.
2. **Shared aging / exposure** — buckets and open exposure are the same read models (ADR-0006); tabs switch kind without reloading a second module tree.
3. **Honest UI** — tab state is visible in the URL; no fake “two products” when the domain is one AP/AR control surface. PNG UI-07/08 remain layout references, not mandatory route topology.

## Consequences

- Pixel UAT compares layout/copy against UI-07/08 **within** the tabbed desk, not separate routes.
- Deep links and Bill panels continue to use `/ap-ar?…` and `/ap-ar/exposures/…`.
- Changing to split routes later would be an Architecture Deviation (nav, bookmarks, permission menus).

## Alternatives rejected

- Separate `/ap` and `/ar` pages cloning list chrome — doubles maintenance for shared aging/exposure without new domain capability.
- Hiding the other tab to “look like” a single PNG — dishonest; operators often need both sides of the desk.
