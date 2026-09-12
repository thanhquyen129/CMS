# UI-0 DoD — Scaffold + shell + login

**Status:** Done  
**Date:** 2026-09-12  
**Plan:** `docs/sprint/PLAN-UI.md` · Prompt: `PROMPT-UI-0.md`

## Outcome
`http://194.233.89.26/` serves Vietnamese Next.js shell (not API JSON). Login issues JWT (ADR-0002) via BFF httpOnly cookie (ADR-0007). nginx proxies `/api|/health|/ready|/metrics` → API.

## Checklist

| # | Criterion | Status |
|---|-----------|--------|
| 1 | `apps/web` App Router + TS; `/login`, `/` shell; Bill/Dashboard “sắp có” | Done |
| 2 | Load `GET /api/terminology`; VI labels in shell | Done |
| 3 | `POST /api/auth/login`; `users.password_hash`; env bootstrap; ADR-0007 | Done |
| 4 | `Dockerfile.web`; compose `web` + `proxy`; API not on host `:80` | Done |
| 5 | CI deploy smoke: `/` HTML, `/health`, `/api/terminology` | Done |
| 6 | Login empty/loading/error; VI B2B copy | Done |
| 7 | This DoD + handoff + README Pass UI | Done |

## Auth
- API: email/password → Bearer JWT (`tenant_id` + `sub`).
- Browser: `/bff/auth/login` sets httpOnly cookie `lcms_at` (no localStorage).
- Host bootstrap: `Auth__Bootstrap__Email` / `Password` (never commit).

## Verify
- `dotnet test` green (includes `Ui0AuthLoginTests`).
- After deploy: `/` HTML, `/health` OK, `/api/terminology` OK.

## Non-goals (deferred)
U1 Bill hub, U2 Cost confirm, OIDC, design system, dark mode.
