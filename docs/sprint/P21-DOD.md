# P21 — Data Scope Revenue / Documents / AP·AR DoD

**Ngày:** 2026-09-13 · ADR-0003

## Done
1. List/get Revenue, Financial Documents, AP, AR dùng `EnsureAndResolveDataScopeAsync`.
2. `own` = `CreatedBy`; `organization` = Bill org subtree.
3. Get ngoài scope → 404.

## Verify
- Build + SprintP21P25 tests; Sprint1 data-scope regression.

## Non-goals
- Aging bucket scope filter (action permission only).
