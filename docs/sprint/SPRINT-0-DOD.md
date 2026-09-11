# Sprint 0 — Definition of Done (TD6 Foundation)

Close criteria from TD6: *Repo/CI/CD baseline; environments; coding standards; migration framework; observability skeleton; terminology contract* — Epics **E01 / E14 / E15 / E16** at foundation depth only.

## Done (this sprint)

| Item | Status | Notes |
|------|--------|-------|
| Repo + Clean Architecture (`LCMS.*`) | Done | Modular monolith .NET 8 |
| Migration framework (EF Core + InitialTd1) | Done | Postgres; migrate-on-startup configurable |
| Tenant / Bill APIs + tenant filter | Done | `X-Tenant-Id` bootstrap |
| Vietnamese API errors | Done | Exception middleware |
| `/health` + `/ready` | Done | Ready checks DB |
| Environments (compose + `.env.example`) | Done | `infra/docker-compose.*.yml`, `infra/.env.example` |
| CI test + Postgres smoke | Done | GitHub Actions `test` job |
| Deploy job (SSH → `/opt/cms`) | Done | Real `deploy` job; needs `CMS_DEPLOY_SSH_KEY` |
| Observability skeleton (E14) | Done | Serilog JSON console, correlation ID, request log, `/metrics` placeholder |
| Terminology contract (E16) | Done | `VietnameseUiTerms` + `GET /api/terminology` |
| Coding standards baseline | Done | Cursor SoftRules + project conventions |
| Sprint 0 DoD doc | Done | This file |

## Deferred (later sprints)

| Item | Target | Reason |
|------|--------|--------|
| Full User / Permission / JWT | Sprint 1 | Identity depth beyond header tenant |
| Full audit trail + outbox | Later | E14 depth beyond skeleton |
| Prometheus / OpenTelemetry exporters | Later | `/metrics` is process placeholder only |
| Full CP6.5 dictionary coverage | Later | Core entities + maturity + TD6 examples only |
| Cost Expected on Bill (finance path) | Phase 2 / later sprint | Beyond foundation |
| Approval workflows, AP/AR, close | Later sprints | Per TD6 backlog |

## Verify locally

```bash
dotnet test Cms.sln -c Release
curl -fsS http://194.233.89.26/health
curl -fsS http://194.233.89.26/ready
curl -fsS http://194.233.89.26/api/terminology
```
