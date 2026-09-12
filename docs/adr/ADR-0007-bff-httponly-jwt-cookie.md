# ADR-0007: Next.js BFF httpOnly cookie for browser JWT

- Status: Accepted
- Date: 2026-09-12
- Relates: ADR-0002; Pass UI / Sprint U0; PLAN-UI.md

## Context
Production API requires JWT Bearer (`tenant_id` + `sub`). Browser UI must authenticate without trusting client-supplied tenant headers.
Storing JWT in `localStorage` exposes the token to XSS. Cookie sessions on the API conflict with future mobile clients that prefer Bearer.

## Decision
1. **API remains Bearer JWT** (ADR-0002 unchanged). `POST /api/auth/login` returns `accessToken` for any client.
2. **Browser path (U0):** Next.js **BFF** routes under `/bff/auth/*` call the API server-side, then set an **httpOnly** same-origin cookie (`lcms_at`) with the JWT. Token is not written to `localStorage` / JS-readable storage.
3. **Edge routing:** nginx serves `/` → `web:3000` and `/api|/health|/ready|/metrics` → `api:8080`. BFF paths stay on Next (not under `/api`).
4. **Password login (thin):** `users.password_hash` + env bootstrap (`Auth:Bootstrap:*` on host). OIDC IdP is a later ADR.
5. **Cookie Secure:** off by default while the public edge is HTTP; set `AUTH_COOKIE_SECURE=true` when TLS terminates in front.

## Consequences
- Same-origin UI + proxy avoids browser CORS gymnastics.
- Mobile / Postman continue using raw Bearer from `/api/auth/login`.
- Middleware on Next redirects unauthenticated `/` → `/login`; API authz remains the security boundary (UI hide ≠ authz).

## Alternatives rejected
- localStorage Bearer — XSS-readable token.
- API-issued cookie session as primary — awkward for mobile / non-browser clients.
- Publishing API alone on host `:80` — blocks shipping Vietnamese UI shell.
