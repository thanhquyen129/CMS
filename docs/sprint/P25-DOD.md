# P25 — Soak + audit PG pushdown DoD

**Ngày:** 2026-09-13 · E16

## Done
1. `ListAuditEventsQuery`: From/To pushdown on Npgsql; SQLite keeps in-memory filter.
2. `scripts/soak/money-path-soak.ps1` + `docs/ops/soak.md`.

## Verify
- Build; soak script against local/VPS health.

## Non-goals
- CI soak gate / k6 suite.
