# ADR-0014 — Client UI theme & layout preferences

## Status
Accepted

## Context
CMS web needs multiple visual shells (Invoika-inspired horizontal/teal, Soft Purple, Classic) and an admin Settings screen to pick a default theme. A commercial Themesbrand Invoika clone (assets/CSS) is out of scope for copyright. Tenant DB settings do not exist yet.

## Decision
- Ship **inspired** themes as CSS variable packs on `html[data-theme]` + layout via `html[data-layout]`.
- Persist preferences in **cookie `lcms_ui` + localStorage** (browser-scoped). Apply before paint with a tiny boot script.
- Settings page `/settings`: basic (layout, density, language locked VI) + default theme picker.
- Do **not** copy Themesbrand proprietary assets or markup.

## Consequences
- Preferences do not sync across devices until a tenant/user settings API exists.
- UI hide of Settings is not authz; follow-up: `settings.manage` + Admin seed when server defaults are added.
- Default preset: `invoika` + `horizontal`.

## Alternatives
- Tenant DB `tenant_settings` for org-wide forced theme — deferred.
- Embed licensed Invoika build — rejected (license + coupling).
