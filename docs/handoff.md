# Handoff

## 2026-09-12 — Kickoff CMS

### User
Kickoff: đặt tên Cost Management System (CMS); update rules từ alogex → CMS; đọc BA docs; lập kế hoạch; tạo repo GitHub CMS; clear + deploy lên VPS theo hailybato.

### Done
- Rules: renamed product to CMS; host `194.233.89.26` → `/opt/cms`; repo `thanhquyen129/CMS`; expert stance → financial control (Bill-centric); removed A1LogEx marketplace/beta clone standing orders.
- BA: local `LCMS_BA_docs/` only had a short note; full package found on Google Drive (CP1–CP7, TD1–TD6, Final Handover). Indexed + implementation plan written from Final Handover / CP7 / TD3 / CP1.
- GitHub private repo created: https://github.com/thanhquyen129/CMS
- VPS inspected: `a1-sg-01` (`194.233.89.26`) still runs live sanlogistics (`a1-api`, `a1-postgres`). **Clear/deploy blocked pending PO confirm** (destructive). `a1logex-sg-01` left untouched.

### Files
- `.cursor/rules/05-product-cms.mdc` (replaces `05-product-a1.mdc`)
- `.cursor/rules/06-ship-after-task.mdc`, `07-expert-stance.mdc`, `ux-ui.mdc`, `00-core-product.mdc`, `01-ai-collaboration.mdc`, `monitoring.mdc`
- `docs/implementation-plan.md`, `LCMS_BA_docs/README.md`, scaffold under `src/`, `infra/`, `.github/workflows/`

### Next
- PO confirms VPS wipe of `/opt/sanlogistics` → then deploy CMS stack to `/opt/cms`
- Sync Drive originals; start Phase 1 (Identity/Tenant/Bill)
