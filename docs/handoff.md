# Handoff

## 2026-09-12 — Wipe VPS + deploy CMS bootstrap

### User
Wipe `194.233.89.26`; update all IPs in rules to `194.233.89.26`.

### Done
- Rules: removed other IPv4 (`217.216…`); CMS production IP consistently `194.233.89.26` (IPv6 NIC kept).
- VPS wipe: stopped/removed `a1-api`/`a1-postgres`, volume `infra_a1_pgdata`, deleted `/opt/sanlogistics`, created `/opt/cms`.
- Deployed CMS API bootstrap via Docker Compose on host; verified `/health` and `/ready`.

### Verify
- http://194.233.89.26/health
- http://194.233.89.26/ready
- Actions: https://github.com/thanhquyen129/CMS/actions

### Next
- Phase 1: Identity/Tenant/Master + Bill anchor
- Wire self-hosted runner / Actions deploy for `/opt/cms`

---

## 2026-09-12 — Kickoff CMS

### User
Kickoff: đặt tên Cost Management System (CMS); update rules từ alogex → CMS; đọc BA docs; lập kế hoạch; tạo repo GitHub CMS; clear + deploy lên VPS theo hailybato.

### Done
- Rules: renamed product to CMS; host `194.233.89.26` → `/opt/cms`; repo `thanhquyen129/CMS`; expert stance → financial control (Bill-centric).
- BA indexed; implementation plan written; GitHub private repo created.
- VPS wipe + deploy completed in follow-up turn after PO confirm.
