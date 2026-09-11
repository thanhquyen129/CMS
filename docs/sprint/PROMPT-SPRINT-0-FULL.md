# PROMPT — Pass 2 / Sprint 0 FULL Foundation

Bạn đang làm **Pass 2 — Sprint 0 FULL** (không còn thin stub) trong repo `c:\A1\git\cms`. Pass 1 Sprint 0–12 đã COMPLETE.

## Mục tiêu
Đóng đủ Foundation theo TD6 E14/E15/E16 + Sprint 0 gaps: auth production-ready skeleton, observability đủ vận hành, secrets/env, deploy verified.

## Must ship (Pass 2 depth)
1. **JWT auth skeleton (replace header-only bootstrap path optionally):**
   - Add JWT Bearer auth (symmetric key from config/env — no secrets in git).
   - Claims: `tenant_id`, `sub` (user id); middleware sets `ITenantContext` + user from claims when present.
   - Keep `X-Tenant-Id` as **dev-only** fallback when `Authentication:AllowHeaderBootstrap=true` (Development default); Production default false.
   - Document in `docs/ops/` how to issue a test token (script or endpoint `POST /api/dev/token` **Development only**).
2. **Observability:** Serilog already exists — add request enrichment (tenant, user); optional OpenTelemetry traces stub OR Prometheus `/metrics` scrape format (minimal counters: http_requests_total). Prefer boring Prometheus counters if OTel heavy.
3. **Rate-limit:** if still in-process, document single-node limit; optionally Redis backing if Redis already in compose — else document Pass 2 defer redis.
4. **Audit completeness:** ensure correlation + actor always populated on JWT path.
5. Tests: JWT happy path creates bill; Production-mode without token → 401 on protected API; header bootstrap disabled in Production test.
6. `docs/sprint/SPRINT-0-FULL-DOD.md` + handoff + PR → main. `dotnet test` xanh.

## Non-goals
Full OIDC IdP integration (Azure AD/Keycloak) — JWT local is enough this slice; Next.js login UI.

## Ràng buộc
Never commit secrets. Never alogex. One sprint chat only. Read Pass 1 `SPRINT-0-DOD` / `SPRINT-12-DOD` deferred lists.
