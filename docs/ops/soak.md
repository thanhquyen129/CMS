# Soak / load notes (P25)

## Audit range pushdown
`ListAuditEventsQuery` applies `From`/`To` in SQL when provider is Npgsql.
SQLite tests keep in-memory date filter (EF translate limitation).

## Script
`scripts/soak/money-path-soak.ps1` — timed loop against `/health`, `/ready`, `/api/terminology`.

```powershell
.\scripts\soak\money-path-soak.ps1 -BaseUrl http://194.233.89.26 -Iterations 60
```

Not a CI gate yet; optional nightly.
