# ADR-0015: Refresh tokens + revoke before external OIDC

- Status: Accepted
- Date: 2026-09-13
- Relates: ADR-0002, ADR-0007, P22

## Context
JWT access tokens alone cannot be revoked server-side before expiry. External OIDC is deferred; operators still need logout/revoke and longer sessions without stuffing long-lived access JWTs into cookies.

## Decision
1. Keep claim contract `tenant_id` + `sub` (ADR-0002).
2. Issue **opaque refresh tokens** stored as SHA-256 hashes in `refresh_tokens`.
3. `POST /api/auth/login` returns access + refresh; `POST /api/auth/refresh` rotates; `POST /api/auth/logout` revokes.
4. BFF stores refresh in httpOnly cookie `lcms_rt` (access remains `lcms_at`).
5. External OIDC IdP may replace HMAC signing later without changing claim names or refresh table shape.

## Consequences
- Access JWT remains short-lived; refresh revoke ends session at next refresh.
- Access-token denylist by `jti` is deferred (optional follow-up).
- Production compose may enable Redis rate-limit independently (P23).
