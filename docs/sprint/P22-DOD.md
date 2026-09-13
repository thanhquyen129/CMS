# P22 — Refresh + revoke (OIDC deferred) DoD

**Ngày:** 2026-09-13 · ADR-0015 / ADR-0002

## Done
1. `refresh_tokens` table; login returns refresh; `POST /api/auth/refresh` rotate; `POST /api/auth/logout` revoke.
2. BFF cookies `lcms_at` + `lcms_rt`; `/bff/auth/refresh`.
3. Claim contract vẫn `tenant_id` + `sub`.

## Verify
- `Auth_Refresh_Rotates_And_Logout_Revokes` passed.

## Non-goals
- External IdP / OIDC (follow-up ADR).
- Access JWT jti denylist.
