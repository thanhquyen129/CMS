# Sprint 12 FULL — Definition of Done (Pass 2 Hardening & UAT)

TD6: *Security/NFR tests; integration recovery; performance; audit; Vietnamese UX acceptance; UAT fixes* — Epics **E14, E15, E16**.

## Pass 2 status: **COMPLETE**

This is the **final Pass 2 sprint**. After merge, Pass 2 board = all Done. Residual backlog below is post–Pass 2 (Pass UI / later ops), not blocking financial API go-live depth for Pass 2.

## Done (this sprint)

| Item | Status | Notes |
|------|--------|-------|
| Audit richer before/after JSON | Done | `AuditJson` camelCase snapshots on money mutations |
| Audit coverage: settlement finalize | Done | Payment + collection finalize (already + enriched) |
| Audit: document accept / match | Done | `financial_document.accept`, `document_match.detail_add` |
| Audit: AP/AR recognize + write-off | Done | recognize + write-off enriched |
| `GET /api/audit-events` filters | Done | action, objectType, objectId, correlationId, **from/to** date range |
| `integration_errors` recovery | Done | record + `mark-retried` + `dead-letter`; status on parent record |
| Duplicate integration still 409 C-002 | Done | Unchanged upsert stub |
| Outbox stub (no broker) | Done | `POST /api/outbox/enqueue`, `POST /api/outbox/process-once`, list |
| Rate-limit config (stricter money paths) | Done | `MoneyPathPermitLimit` + `MoneyPathPrefixes` |
| Security headers remain | Done | nosniff / DENY frame / etc. unchanged |
| Timed API smoke | Done | 20× `/api/terminology` &lt; 5s in-process (not soak) |
| AC-008 / AC-007 / AC-009 smoke | Done | Focused gate test |
| Vietnamese UX gaps | Done | Audit/integration/outbox/NFR keys + checklist |
| Tests + DoD + Pass 2 COMPLETE | Done | `Sprint12FullHardeningUatTests` (4); suite **93 passed** |

## Vietnamese UX acceptance checklist (Pass 2)

| Area | Covered |
|------|---------|
| Core entities + maturity | Yes (Pass 1 retained) |
| Documents Received≠Accepted≠Matched | Yes |
| Exposure / AP / AR / Settlement | Yes |
| Close / Control / Dashboard | Yes |
| Audit before/after + date filters | Yes (`AUDIT_BEFORE`, `AUDIT_AFTER`, `AUDIT_DATE_*`) |
| Integration retry / dead-letter | Yes |
| Outbox enqueue / process-once | Yes |
| Rate-limit money path | Yes (`RATE_LIMIT_MONEY_PATH`) |
| Pass 2 complete label | Yes (`PASS2_COMPLETE`) |

## APIs (new / extended)

| Method | Path | Notes |
|--------|------|-------|
| GET | `/api/audit-events` | + `from` / `to` (date range; in-memory on SQLite portability) |
| POST | `/api/integration-errors` | Record error against integration record |
| GET | `/api/integration-errors` | Filter `recoveryStatus` |
| POST | `/api/integration-errors/{id}/mark-retried` | Idempotent |
| POST | `/api/integration-errors/{id}/dead-letter` | Sets record `dead_letter` |
| POST | `/api/outbox/enqueue` | Local outbox stub |
| POST | `/api/outbox/process-once` | Process oldest pending once |
| GET | `/api/outbox` | List outbox messages |

## Config

```json
"RateLimiting": {
  "Enabled": true,
  "PermitLimit": 300,
  "MoneyPathPermitLimit": 60,
  "MoneyPathPrefixes": [ "/api/costs", "/api/revenues", "/api/payments", "…" ],
  "Window": "00:01:00"
}
```

## Migration

`Sprint12Full_HardeningRecovery` — `integration_errors.recovery_*`; `outbox_messages`

## Residual backlog (post–Pass 2)

| Item | Target | Reason |
|------|--------|--------|
| Full broker / distributed outbox | Later | Stub only (enqueue + process-once) |
| Load / soak / redis rate-limit store | Later | Timed smoke only |
| Next.js UAT UI | Pass UI | API Pass 2 complete; UI parallel track |
| Sprint 11 FULL profile merge if still open | Coordinator | This sprint bases on main @ S10 FULL + UI; rebase if needed |
| PG-native DateTimeOffset audit range pushdown | Later | In-memory filter for SQLite portability |

## Verify

```bash
dotnet test Cms.sln -c Release
```

Expected: **93 passed**. Sprint 12 FULL adds **4** tests.
