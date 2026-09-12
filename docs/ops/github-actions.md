# GitHub Actions — CMS

Repo: https://github.com/thanhquyen129/CMS/actions

## Pipeline
Workflow `CI` (`.github/workflows/ci.yml`):
1. **test** — restore, build, `dotnet test`, local health/ready smoke against Postgres service.
2. **deploy** (push to `main` only) — SSH sync to `deploy@194.233.89.26:/opt/cms`, then `docker compose -f infra/docker-compose.host.yml up -d --build`.

## Required secrets
| Secret | Required | Purpose |
|--------|----------|---------|
| `CMS_DEPLOY_SSH_KEY` | **Yes** | Private SSH key for user `deploy` on cms-sg-01 |
| `CMS_DEPLOY_HOST` | No | Defaults to `194.233.89.26` if unset |

Add secrets: GitHub → Settings → Secrets and variables → Actions.

## Deploy safety
- Target is **only** `/opt/cms` on cms-sg-01 — **never** `a1logex-sg-01` / `/opt/alogex`.
- `rsync` **excludes** `infra/.env` so host secrets are never overwritten.
- Host should keep `infra/.env` (from `infra/.env.example`) with `LCMS_DB_PASSWORD`, `Auth__Jwt__SigningKey`, and optional `Auth__Bootstrap__Email` / `Auth__Bootstrap__Password` for UI login.
- Compose brings up `db` + `api` + `web` + `proxy` (nginx). Host `:80` is the proxy — not the API alone.

## Fallback
If Actions deploy fails or the runner cannot reach the host, use operator SSH per `docs/ops/vps-bootstrap.md` (rsync/scp; never `git pull` on the VPS as the primary path).
