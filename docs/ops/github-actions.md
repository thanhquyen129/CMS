# GitHub Actions — CMS

Repo: https://github.com/thanhquyen129/CMS/actions

## Current state
`CI` workflow builds/tests on GitHub-hosted runners. Deploy job is a placeholder until:
1. VPS is cleared for CMS
2. Self-hosted runner or SSH deploy secrets are configured for `/opt/cms`

## Fallback
If Actions deploy is down, follow `docs/ops/vps-bootstrap.md` (tar/scp as last resort). Never `git pull` on the host as the primary path.
