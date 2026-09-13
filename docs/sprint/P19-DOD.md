# P19 — Tenant settings API DoD

**Ngày:** 2026-09-13 · ADR-0014

## Done
1. Entity `tenant_settings` (`UiJson`, `FinancialJson`) + migration `P14_P20_TenantSettingsBankFeedCsv`.
2. `GET/PUT /api/tenant-settings`; `TenantSettingsService`.
3. Permission `settings.manage` trong catalog + enforce PUT.
4. BFF `/bff/tenant-settings`; form trên `/settings`.

## Verify
- PUT tenant settings trong test P16; bootstrap GET không cần actor.

## Non-goals
- UI theme JSON editor (cookie prefs giữ nguyên).
