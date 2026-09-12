# ADR-0002: JWT Bearer auth with tenant_id + sub claims

- Status: Accepted
- Date: 2026-09-11
- Relates: TD1 C-001; Sprint 0 FULL (Pass 2); supersedes header-only bootstrap for Production

## Context
Pass 1 used `X-Tenant-Id` / `X-User-Id` header bootstrap so finance APIs could ship without an IdP.
Production cannot trust client-supplied tenant headers. Identity must carry `tenant_id` and actor (`sub`) in a verified token.

## Decision
1. **JWT Bearer** (symmetric HMAC for Sprint 0 FULL; OIDC IdP may replace signing later without changing claim names).
2. **Required claims:** `tenant_id` (Guid) + standard `sub` (user Guid).
3. **Production defaults:** `Auth:RequireJwt=true`, `Auth:AllowHeaderBootstrap=false`.
4. **Development defaults:** header bootstrap allowed; JWT optional; `POST /api/dev/token` issues short-lived tokens (Development only).
5. **Public (anonymous):** `/`, `/health`, `/ready`, `/metrics`, `GET /api/terminology`, `POST /api/auth/login`, `POST /api/dev/token` (Dev only).
6. **Signing key:** from config/env (`Auth:Jwt:SigningKey` / `Auth__Jwt__SigningKey`). Never commit production secrets.

## Consequences
- Protected `/api/*` without a valid Bearer token return **401** in Production (Vietnamese JSON challenge).
- Tenant resolution prefers JWT claims; headers apply only when `AllowHeaderBootstrap` is true.
- Existing Pass 1 API tests remain on Development + header bootstrap.
- Replacing HMAC with OIDC is a follow-up ADR; claim contract stays `tenant_id` + `sub`.

## Alternatives rejected
- Header-only auth in Production — spoofable tenant context.
- Cookie sessions — not a fit for API-first modular monolith + future mobile.
- Per-endpoint optional auth without a Production fallback policy — easy to miss a money path.
