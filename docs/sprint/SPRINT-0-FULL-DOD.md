# Sprint 0 FULL — Definition of Done (Pass 2 Foundation)

Pass 1 Sprint 0 delivered skeleton (header bootstrap, Serilog, `/metrics` JSON placeholder).
Pass 2 closes TD6 foundation auth + observability for Production (see `PROMPT-SPRINT-0-FULL.md`).

## Done

| Item | Status | Notes |
|------|--------|-------|
| JWT Bearer auth | Done | `Auth` + `Microsoft.AspNetCore.Authentication.JwtBearer` |
| Claims `tenant_id` + `sub` | Done | Resolved in `TenantResolutionMiddleware` |
| Production disables header bootstrap | Done | `AllowHeaderBootstrap=false` default in Production |
| Dev may use `X-Tenant-Id` / `X-User-Id` | Done | `AllowHeaderBootstrap=true` in Development |
| Dev token helper | Done | `POST /api/dev/token` + `docs/ops/dev-jwt-token.md` |
| Observability enrichment | Done | LogContext TenantId/UserId; request log includes both + CorrelationId |
| Prometheus metrics | Done | `prometheus-net` `UseHttpMetrics` + `MapMetrics` at `/metrics` |
| Vietnamese 401 challenge | Done | JWT `OnChallenge` JSON (`unauthorized`) |
| Rate-limit note (single-node) | Done | In-process Sprint 12 limiter; Redis deferred — documented in ops |
| Audit actor on JWT path | Done | `sub` → `ICurrentUserContext`; correlation middleware unchanged |
| Tests JWT + Production 401 | Done | `Sprint0FullJwtAuthTests` |
| ADR-0002 | Done | `docs/adr/ADR-0002-jwt-bearer-tenant-claims.md` |
| DoD doc | Done | This file |

## Config (no secrets in git)

| Key | Production | Development |
|-----|------------|-------------|
| `Auth:RequireJwt` | `true` | `false` |
| `Auth:AllowHeaderBootstrap` | `false` | `true` |
| `Auth:Jwt:SigningKey` | **env** `Auth__Jwt__SigningKey` (≥32 chars) | Dev-only local key |
| `Auth:Jwt:Issuer` / `Audience` | `lcms-api` | `lcms-api` |

Host compose reads `Auth__Jwt__SigningKey` from `infra/.env` (never commit real value). See `infra/.env.example`.

## Public (anonymous) routes

`/`, `/health`, `/ready`, `/metrics`, `GET /api/terminology`, `POST /api/dev/token` (Dev only).

## Deferred / follow-ups

| Item | Target |
|------|--------|
| OIDC / external IdP | Later Pass 2 / Sprint 1 FULL |
| Refresh tokens / revocation list | Later |
| OpenTelemetry traces exporter | Later |
| Redis distributed rate-limit | Later |
| Next.js login UX | Later |

## Verify

```bash
dotnet test Cms.sln -c Release
curl -fsS http://127.0.0.1/health
curl -fsS http://127.0.0.1/metrics | head
```

Expected: **50 passed** (46 Pass 1 + 4 Sprint 0 FULL).
