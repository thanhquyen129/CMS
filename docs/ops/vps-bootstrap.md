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
3. Sync release (exclude secrets):

```bash
rsync -az --delete \
  --exclude '.git/' \
  --exclude 'infra/.env' \
  --exclude 'hailybato.txt' \
  --exclude 'docs/hailybato.txt' \
  --exclude '.secrets/' \
  -e "ssh -i ~/.ssh/id_ed25519_a1" \
  ./ deploy@194.233.89.26:/opt/cms/
```

4. Restart stack:

```bash
ssh -i ~/.ssh/id_ed25519_a1 deploy@194.233.89.26 \
  'cd /opt/cms && docker compose -f infra/docker-compose.host.yml --env-file infra/.env up -d --build'
```

5. Verify `http://194.233.89.26/health` and `/ready`.

## Never
- Deploy CMS onto `a1logex-sg-01` / `/opt/alogex`
- Commit secrets or overwrite host `infra/.env` blindly
- `git pull` on the VPS as the primary release path (VPS is not a git clone)
