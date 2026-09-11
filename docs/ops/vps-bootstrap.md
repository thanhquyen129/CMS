# VPS bootstrap — CMS

Target host: `194.233.89.26` (alias `cms-sg-01` / SSH config may still say `a1-sg-01`).
App dir: `/opt/cms`.
User: `deploy` (key-only).

## Bootstrap (2026-09-12)
1. Create `/opt/cms` owned by `deploy`.
2. Copy release artifacts (Actions or fallback scp) — never overwrite `infra/.env`.
3. `docker compose -f infra/docker-compose.host.yml up -d --build`
4. Verify `http://194.233.89.26/health` and `/ready`.

## Never
- Deploy CMS onto `a1logex-sg-01` / `/opt/alogex`
- Commit secrets / overwrite host `.env` blindly
