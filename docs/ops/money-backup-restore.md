# Money backup and restore — CMS

Host: `cms-sg-01` (`194.233.89.26`), app dir `/opt/cms`, Compose file `infra/docker-compose.host.yml`.
Postgres 16 container `cms-db`, database `lcms`, volume `cms_pgdata`. Port 5432 is not published.

Two different recoveries. Do not mix them.

## 1. Tenant button — catalog and settings only

What it covers: company profile, catalog, currencies, dated FX rows, organizations, notification prefs, license module flags.
What it does not cover: bills, cost, revenue, documents, AP/AR, payments, collections, financial closes, snapshots, passwords, sessions.

| | |
| --- | --- |
| RPO | Time since the last backup the operator saved on Sao lưu & Khôi phục. There is no schedule. |
| RTO | Minutes. `POST /api/tenant-backups/{id}/restore` with phrase `RESTORE {tenantCode}`. |
| Close | A locked close and its snapshot hash are not rewritten. Test: `BackupRestore_IsTenantIsolated_AndDoesNotTouchMoney`. |

Use this when a catalog name or company profile was edited by mistake. Do not use it to undo a payment, a recognition, or a close.

## 2. Database file — the money ledger

Compose does not set `wal_level` or `archive_command`. Continuous point-in-time recovery is not in place. The money RPO is the age of the last dump an operator copied off the host. Until a dump exists, a lost volume is not recoverable.

| | |
| --- | --- |
| RPO | Age of the newest dump stored off the host. No dump means no money recovery. |
| RTO | Hours: restore into a side database, compare closes, then decide. Do not overwrite `lcms` while the API is writing. |

Dump (password is `LCMS_DB_PASSWORD` in `infra/.env` on the host; do not print it):

```bash
docker exec cms-db pg_dump -U lcms -d lcms -Fc -f /tmp/lcms.dump
docker cp cms-db:/tmp/lcms.dump /opt/cms/lcms-$(date -u +%Y%m%dT%H%M%SZ).dump
docker exec cms-db rm -f /tmp/lcms.dump
```

Copy the file off the VPS. Do not leave the only copy on `cms_pgdata`.

Drill on a side database. Do not restore into `lcms`:

```bash
docker exec cms-db dropdb -U lcms --if-exists lcms_drill
docker exec cms-db createdb -U lcms lcms_drill
docker cp /opt/cms/lcms-YYYYMMDDTHHMMSSZ.dump cms-db:/tmp/lcms.dump
docker exec cms-db pg_restore -U lcms -d lcms_drill --no-owner --no-privileges /tmp/lcms.dump
docker exec cms-db rm -f /tmp/lcms.dump
```

Before any cutover, compare locked snapshots. A hash that exists in production and is missing from the drill was created after the dump. Replacing production deletes that close. Stop and record the loss, or replay the work. Do not `pg_restore` over `lcms` to “catch up”.

```bash
docker exec cms-db psql -U lcms -d lcms -c \
  "select id, financial_close_id, snapshot_version, immutable_hash, closed_at from financial_close_snapshots where deleted_at is null order by closed_at;"
docker exec cms-db psql -U lcms -d lcms_drill -c \
  "select id, financial_close_id, snapshot_version, immutable_hash, closed_at from financial_close_snapshots where deleted_at is null order by closed_at;"
```

Drop the drill database when the comparison is written down:

```bash
docker exec cms-db dropdb -U lcms lcms_drill
```

## When something is wrong

1. Wrong catalog or company profile: tenant restore in section 1.
2. Wrong money that is still in the database: reverse, reopen, or adjust through the product. Do not edit rows and do not restore yesterday’s database over today.
3. Volume lost or corrupted: section 2, side database first. Cut over only after the snapshot comparison is accepted.
