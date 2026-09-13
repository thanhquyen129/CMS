# P24 — Fine-grained permissions DoD

**Ngày:** 2026-09-13 · H-009

## Done
1. Catalog: `cost.confirm`, `cost.actualize`, `revenue.confirm`, `revenue.actualize`, `ap.write_off`, `ar.write_off`.
2. Enforce `EnsureAsync` trên confirm/actualize/write-off (Approval ≠ Permission).
3. Admin seed picks up via `CoreCatalog`.

## Verify
- `FineGrained_ConfirmCost_Denied_WithoutPermission` → 403.

## Non-goals
- UI hide buttons (server SoT).
