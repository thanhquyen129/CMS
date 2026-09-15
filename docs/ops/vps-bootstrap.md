# VPS bootstrap — CMS

Target host: `194.233.89.26` (alias `cms-sg-01` / SSH config may still say `a1-sg-01`).
App dir: `/opt/cms`.
User: `deploy` (key-only).

## Normal path (preferred)
Push to `origin/main` → GitHub Actions `CI` job `deploy` syncs and restarts Compose.
See `docs/ops/github-actions.md` for secrets (`CMS_DEPLOY_SSH_KEY`).

## Bootstrap / fallback (operator machine)
1. Ensure `/opt/cms` exists and is owned by `deploy`.
2. On host, create `infra/.env` from `infra/.env.example` (set `LCMS_DB_PASSWORD`) — **once**; do not overwrite on later deploys.
3. Sync release (exclude secrets). On Linux/macOS use `rsync`; on Windows operator without rsync use tar+scp:

```bash
# Linux/macOS
rsync -az --delete \
  --exclude '.git/' \
  --exclude 'infra/.env' \
  --exclude 'hailybato.txt' \
  --exclude 'docs/hailybato.txt' \
  --exclude '.secrets/' \
  -e "ssh -i ~/.ssh/id_ed25519_a1" \
  ./ deploy@194.233.89.26:/opt/cms/
```

```powershell
# Windows PowerShell fallback
tar -czf cms-release.tgz --exclude=.git --exclude=infra/.env --exclude=hailybato.txt --exclude=.secrets .
scp -i $env:USERPROFILE\.ssh\id_ed25519_a1 cms-release.tgz deploy@194.233.89.26:/tmp/
ssh -i $env:USERPROFILE\.ssh\id_ed25519_a1 deploy@194.233.89.26 "tar -xzf /tmp/cms-release.tgz -C /opt/cms && rm /tmp/cms-release.tgz"
```
4. Restart stack:

```bash
ssh -i ~/.ssh/id_ed25519_a1 deploy@194.233.89.26 \
  'cd /opt/cms && docker compose -f infra/docker-compose.host.yml --env-file infra/.env up -d --build'
```

5. Verify `http://194.233.89.26/health` and `/ready`.

## Hardening checklist (host)
- Public host ports: **80/443 only** (+ SSH 22). Never publish Postgres/Redis/Ollama.
- Every Compose service: `mem_limit` (prefer `cpus` / `pids_limit`).
- Nginx: rate-limit + block known Next.js bypass header `x-middleware-subrequest`.
- `/metrics` must not be public without auth/allowlist.
- Details: `.cursor/rules/08-vps-selfhost-hardening.mdc`.

## Never
- Deploy CMS onto `a1logex-sg-01` / `/opt/alogex`
- Commit secrets or overwrite host `infra/.env` blindly
- `git pull` on the VPS as the primary release path (VPS is not a git clone)
