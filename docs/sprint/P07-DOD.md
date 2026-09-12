# P07 — Aging summary + dashboard tách quyền tài chính DoD

**Ngày:** 2026-09-12  
**PO:** E09; ADR-0006; H View Cost ≠ Revenue · Checklist `PO-GAP-CHECKLIST.md`

## Done
1. Permission `revenue.read` in core catalog (independent of `cost.read`).
2. `IPermissionService.HasPermissionAsync` — soft check for dashboard.
3. Dashboard summary: `financialVisibility`; Cost/Revenue/Margin/AP·AR·settlement money sides null/hidden when unauthorized (server omits amounts).
4. Aging: `GET /api/aging/summary`, `GET /api/aging/export` (CSV); AP aging ⇒ `cost.read`, AR ⇒ `revenue.read`.
5. UI `/ap-ar/aging` (bucket table + Xuất CSV); link từ AP/AR + dashboard.
6. Tests `SprintP07AgingDashboardPermissionTests`.

## Verify
- `dotnet test` filter SprintP07 (+ aging existing) xanh.
- `npm run build` apps/web (khi ship).

## Non-goals
- Full Data Scope on AP/AR lists (P21).
- Fine-grained `cost.confirm` catalog (P24).
- Multi-currency roll-up on aging buckets (per-currency later).
