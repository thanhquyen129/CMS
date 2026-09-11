# VPS bootstrap — CMS

Target host: `194.233.89.26` (alias `cms-sg-01` / SSH config may still say `a1-sg-01`).
App dir: `/opt/cms`.
User: `deploy` (key-only).

## Warning
As of 2026-09-12 this host still runs live **sanlogistics** (`/opt/sanlogistics`, containers `a1-api`, `a1-postgres`).
Do **not** clear until PO explicitly confirms wipe.

## After PO confirms clear
1. Stop and remove sanlogistics compose stack.
2. Create `/opt/cms` owned by `deploy`.
3. Copy release artifacts (Actions or fallback scp) — never overwrite `infra/.env`.
4. `docker compose -f infra/docker-compose.host.yml up -d --build`
5. Verify `http://194.233.89.26/health` and `/ready`.

## Never
- Deploy CMS onto `a1logex-sg-01` / `/opt/alogex`
- Commit secrets / overwrite host `.env` blindly
