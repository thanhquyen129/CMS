# CMS Implementation Plan — Kickoff 2026-09-12

## Goal
Ship **Cost Management System (CMS)** as a Bill-centric financial control layer for Logistics first, with one Core Model + tenant capability flags for other verticals later.

## Non-goals (V1)
- Replace Operational System / TMS (GPS, e-POD, empty-truck matching)
- Marketplace / YCBG flows
- Microservices split (Modular Monolith per TD3)

## Architecture baseline (locked)
- Modular Monolith, modules M01–M15 (TD3)
- Layers: Presentation → Application → Domain → Infrastructure + Read Model
- Stack choice (Dev Manager): ASP.NET Core + PostgreSQL + Next.js (web/admin)
- Multi-tenant: `tenant_id` on every query; Action Permission × Data Scope independent

## Phased delivery

### Phase 0 — Platform bootstrap (this kickoff)
- Rules retargeted A1LogEx → CMS
- Private GitHub repo `thanhquyen129/CMS`
- Repo scaffold, CI stub, `/health` + `/ready`
- VPS target: `194.233.89.26` → `/opt/cms` (**clear old stack only after PO confirm**)
- Sync BA package index into `LCMS_BA_docs/`

### Phase 1 — Identity, tenant, master, Bill anchor
- M01 Identity/Tenant/Access
- M02 Master Data (Party, Currency, Cost Type taxonomy)
- M03 Operational Reference (Bill as Financial Anchor; Order/Shipment optional depth)
- AuthZ: Role × Action × Data Scope; Approval separate
- Vietnamese UX shell (CP6.5)

### Phase 2 — Cost & Rating (MVP financial slice)
- M04 Rate & Pricing → Expected Cost seed
- M05 Cost Management: Expected → Confirmed → Actual; Direct vs Shared→Allocation
- Allocation engine with conservation + block on missing/zero basis
- Audit trail; no silent overwrite

### Phase 3 — Revenue & Bill Financial Profile
- M06 Revenue (maturity layers; Bill-attributable or INCOMPLETE)
- M12 Bill Financial Profile / profitability read model
- Owner dashboard drill-down Company → Bill → Cost/Revenue

### Phase 4 — Documents, AP/AR, Settlement
- M07 Financial Documents (Received ≠ Accepted; N:N matching)
- M08 Exposure & Recognized AP/AR
- M09 Payment/Collection + Settlement Allocation

### Phase 5 — Control & Close
- M10 Reconciliation / Exception / Approval
- M11 Financial Close (immutable snapshots; Strict/Controlled policy)
- Exception dashboard for management

### Phase 6 — Integration & hardening
- M13 Configuration/Policy (versioned/effective-dated)
- M14 Integration (idempotent inbound; no guessed facts)
- M15 Audit completeness
- NFR, observability, backup/rollback (TD5)
- Load/security tests on money paths

## Success metrics
- Ops/Finance can confirm cost on a Bill and see Expected vs Actual variance
- Shared cost allocates only to Eligible Bills; totals conserved
- Close produces immutable snapshot; reopen does not rewrite history
- UI labels Vietnamese per CP6.5; no raw enum leak
- Tenant isolation verified by tests

## Immediate blockers needing PO answer
1. **VPS clear:** `194.233.89.26` currently runs live **A1 board** (`/opt/sanlogistics`, containers `a1-api` + `a1-postgres`). Confirm wipe to host CMS?
2. **Ops SoT for pilot:** which operational system API feeds Bill/Shipment for first tenant?
3. **First tenant vertical:** pure Logistics pilot only, or also a non-logistics tenant in V1 config?

## Follow-ups
- Download full Drive binaries into `LCMS_BA_docs/originals/` for offline use
- ADR-0001: tenancy + identity provider
- ADR-0002: money/FX rounding + base currency
- Wire GitHub Actions runner on CMS host after clear
