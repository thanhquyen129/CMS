# Sprint 10 — Definition of Done (Financial Close)

TD6: *Close eligibility; snapshot; lock; reopen/reclose* — Epic **E12**.

## Done (this sprint)

| Item | Status | Notes |
|------|--------|-------|
| `financial_closes` TD1 D11 | Done | Scope/period/version/status/policy |
| `financial_close_snapshots` | Done | As-closed; `snapshot_version` + `immutable_hash` |
| `financial_close_snapshot_details` | Done | Metric lines (cost/revenue/AP/AR) |
| Close immutability (C-010 / AC-008) | Done | Snapshots insert-only; DbContext rejects Modified/Deleted; reopen/reclose appends versions |
| Eligibility stub | Done | Block snapshot if open/in_progress **critical** exception in scope |
| Start → snapshot/lock → reopen → reclose | Done | Reopen keeps history; re-snapshot ↑ `SnapshotVersion`; supersede ↑ `VersionNo` |
| Tenant APIs + VI errors | Done | FluentValidation VI + AppException |
| Soft-delete / row_version / tenant_id | Done | Close mutable via `TenantEntityBase`; snapshots never hard-deleted / never business-mutated |
| Tenant isolation | Done | Global filter + `X-Tenant-Id` |
| Migration `Sprint10_FinancialClose` | Done | Three tables; does not alter Sprint 0–9 migrations |
| Tests | Done | 3 new; suite green (40) |
| Sprint 10 DoD doc | Done | This file |

## Deferred (later sprints / Pass 2)

| Item | Target | Reason |
|------|--------|--------|
| Reporting dashboard / P&L from snapshots | Sprint 11 | Non-goal |
| Hardening / UAT / Strict policy matrix expansion | Sprint 12 | Pass 2 S10 FULL ships Controlled vs Strict stub |
| Full eligibility checklist (docs/AP/AR/settlement gates) | **Done in Sprint 10 FULL** | See `SPRINT-10-FULL-DOD.md` |
| Period lock blocking mutations on live ledger | **Done in Sprint 10 FULL** | See `SPRINT-10-FULL-DOD.md` + ADR-0010 |
| JWT / OIDC | Later | Keep header bootstrap |
| Next.js UI | Later | API-only this sprint |

## Headers (bootstrap until JWT)

| Header | Purpose |
|--------|---------|
| `X-Tenant-Id` | Tenant context (C-001) |
| `X-User-Id` | Actor for close/snapshot audit |
| `X-Correlation-Id` | Request correlation |

## APIs

| Method | Path | Notes |
|--------|------|-------|
| POST | `/api/financial-closes` | Start open close (optional `supersedesCloseId` for reclose) |
| GET | `/api/financial-closes` | List (+ optional status/scopeType) |
| GET | `/api/financial-closes/{id}` | Get + snapshots (+ details) |
| POST | `/api/financial-closes/{id}/snapshot` | Eligibility → immutable snapshot → lock |
| POST | `/api/financial-closes/{id}/reopen` | Reopen locked close; **no** snapshot mutation |
| GET | `/api/financial-closes/{id}/snapshots` | List snapshot history |
| GET | `/api/financial-close-snapshots/{id}` | Get snapshot + details |

## Verify

```bash
dotnet test Cms.sln -c Release
```
