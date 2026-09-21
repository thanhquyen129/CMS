# ADR-0021: Tenant admin — company profile, license/capability, notifications, logical backup

- Status: Accepted
- Date: 2026-09-21
- Relates: UI-13, UI-14; H-001 capability không fork Core Model; ADR-0001 tenancy; ADR-0016 roles; backup-rollback.mdc

## Context
PO mockup UI-13/UI-14 lists master-data tabs (route/mode/port) and a full System menu (company, users, license, audit, notifications, backup). The product is a self-hosted commercial LCMS: operators must onboard users with passwords, keep a legal company profile, see audit, and manage license seats/modules. Restore-yesterday’s-ledger from the UI would erase good money and break audit.

## Decision
1. **Company profile** lives on `tenants` (legal name, tax id, address, timezone IANA, default currency, optional logo). Display timezone is not a substitute for UTC storage.
2. **Users**: admin create/update/deactivate + set password (ASP.NET Identity hasher). Password change revokes all refresh tokens. Seat limit is enforced from the active license. Last Admin cannot be deactivated.
3. **Master catalog kinds** add `transport_route`, `transport_mode`, `location` (port/airport/border via `attributes_json`). These are rating/master data, not TMS tracking (ADR-0019).
4. **License** is tenant-scoped (`tenant_licenses` + modules). Capability flags hide UI modules only — they do **not** drop tables or fork the Core Model (H-001). Admin may disable an included module; Admin may not enable a module the plan does not include.
5. **Notifications**: tenant preferences per event (in-app + email). In-app inbox is persisted. Email is enqueued on outbox topic `notification.email`. If SMTP is not configured, the outbox row is marked processed with an honest “chưa gửi” note — no fake sent status.
6. **Backup**: tenant **logical** snapshot of master/config only (profile, catalog, currencies, FX, orgs, notification prefs, license modules). Never password hashes, refresh tokens, or money ledgers. Restore is master/config upsert with typed confirm (`RESTORE <tenant code>`). Postgres PITR remains ops, not a tenant button.

## Consequences
- New permissions: `audit.read`, `license.manage`, `backup.manage`, `notification.manage` (Admin gets all; FinancialController gets audit + notifications).
- UI-14 screens are real commands, not decoration.
- Money recovery after disaster stays PITR-to-side-instance (backup-rollback.mdc).

## Alternatives rejected
- Store company profile only as JSON blob — query/report fields (MST, timezone, currency) need columns.
- Tenant “restore full database” button — violates backup ≠ compensation and would silently overwrite settled money.
- License as a client-only badge — seats and modules must be server-enforced.
- Fork schema per disabled module — forbidden by H-001.
