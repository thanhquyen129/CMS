# Cost Management System (CMS)

Private repo for **Cost Management System** (LCMS when logistics-focused): Bill-centric financial control — cost, revenue, documents, AP/AR, settlement, reconciliation, close, profitability.

## Stack
- Backend: ASP.NET Core (.NET 8) Clean Architecture + PostgreSQL
  - `LCMS.Domain` / `LCMS.Application` / `LCMS.Infrastructure` / `LCMS.Api`
- Web/admin: Next.js
- Host: Contabo VPS `194.233.89.26` → `/opt/cms`

## Docs
- BA/TD package index: `LCMS_BA_docs/`
- ADR: `docs/adr/` (see ADR-0001 for UUIDv7 + tenant isolation)
- Implementation plan: `docs/implementation-plan.md`
- Handoff: `docs/handoff.md`

## Local secrets
Keep `hailybato.txt` gitignored. Never commit passwords or keys.
