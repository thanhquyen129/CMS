# Sprint 12 — Definition of Done (Hardening & UAT)

TD6: *Security/NFR tests; integration recovery; performance; audit; Vietnamese UX acceptance; UAT fixes* — Epics **E14, E15, E16**.

## Pass 1 status: **COMPLETE**

This is the **final Pass 1 sprint**. After merge, Pass 1 board = all Done. Pass 2 (full PO depth) starts next.

## Done (this sprint)

| Item | Status | Notes |
|------|--------|-------|
| `audit_events` table (D12 / IDX-013) | Done | Actor/action/object/before-after/reason + correlation_id + occurred_at; soft-delete + tenant_id |
| Audit writes on key money mutations | Done | `cost.create`, `cost.confirm`, `revenue.create`, `payment_allocation.finalize`, `financial_close_snapshot.create` |
| `GET /api/audit-events` | Done | Filters: objectType, objectId, action, correlationId; tenant-scoped |
| `integration_records` (+ `integration_errors`) | Done | C-002 / IDX-012 unique `(tenant, source_system, external_object_type, external_id)` |
| Integration upsert stub | Done | `POST /api/integration-records` — first insert OK; duplicate → 409 Conflict (safe reject) |
| Rate-limit middleware | Done | Fixed window on `/api/*` only; config `RateLimiting`; health/ready exempt |
| Security headers middleware | Done | nosniff, DENY frame, referrer-policy, CSP stub, permissions-policy |
| `/health` `/ready` + correlation id | Done | Still work; correlation echoed; security headers present |
| Vietnamese terminology coverage | Done | Sprint 1–11 keys retained; added AUDIT_EVENT, INTEGRATION_RECORD, CORRELATION_ID, RATE_LIMIT, etc. |
| Tests | Done | 3 new Sprint 12; suite green (**46**) |
| Sprint 12 DoD + Pass 1 COMPLETE | Done | This file |

## Vietnamese UX acceptance checklist (Pass 1)

| Area | Covered in `VietnameseUiTerms` / `GET /api/terminology` |
|------|--------------------------------------------------------|
| Core entities (Bill/Cost/Revenue/…) | Yes |
| Maturity Expected/Confirmed/Actual | Yes |
| Documents Received≠Accepted≠Matched | Yes |
| Exposure / AP / AR / Settlement | Yes |
| Reconciliation / Exception / Approval | Yes |
| Financial close / snapshot | Yes |
| Dashboard / queues | Yes |
| Audit / Integration / NFR labels | Yes (Sprint 12) |
| Error messages on money paths | VI (existing + C-002 conflict) |

## Deferred (Pass 2 backlog pointers) — **closed by Sprint 12 FULL**

| Item | Target | Reason |
|------|--------|--------|
| Full outbox + retry topology | Pass 2 → Done (stub) | `integration_errors` recovery + outbox enqueue/process-once; no broker |
| Load / soak / perf benchmarks | Later | Timed smoke only (S12 FULL) |
| JWT / OIDC production auth | Pass 2 S0 Done | JWT Bearer shipped; bootstrap optional in tests |
| Next.js UAT UI | Pass UI | Parallel track |
| Complete AC-001… matrix hardening | Pass 2 → Done (gates) | AC-007/008/009 smoke in S12 FULL |
| Audit before/after full entity JSON | Pass 2 → Done | Richer `AuditJson` snapshots |
| Payment finalize beyond allocation | Pass 2 → Done | Audited finalize + write-off / recognize |
| Distributed/redis rate-limit store | Later | In-process fixed window + money-path limit |

See `SPRINT-12-FULL-DOD.md` for Pass 2 COMPLETE.

## Headers (bootstrap until JWT)

| Header | Purpose |
|--------|---------|
| `X-Tenant-Id` | Tenant context (C-001) |
| `X-User-Id` | Actor (audit actor_id) |
| `X-Correlation-Id` | Request correlation (audit + logs) |

## APIs

| Method | Path | Notes |
|--------|------|-------|
| GET | `/api/audit-events` | Tenant audit trail; optional filters |
| POST | `/api/integration-records` | Upsert stub; duplicate → 409 (C-002) |
| GET | `/api/integration-records` | List integration records |
| GET | `/api/terminology` | CP6.5 dictionary (extended) |

## Migration

`Sprint12_HardeningAudit` — `audit_events`, `integration_records`, `integration_errors`

## Verify

```bash
dotnet test Cms.sln -c Release
```

Expected: **46 passed**.
