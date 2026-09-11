# ADR-0001: Identity keys, tenancy isolation, PostgreSQL physical mapping (TD1)

- Status: Accepted
- Date: 2026-09-12
- Relates: TD1 C-001, C-012, C-013; H-003 Bill Financial Anchor

## Context
TD1 giữ ID type abstract (UUIDv7/ULID/bigint). Stack đã khóa ASP.NET Core + PostgreSQL.
Cần khóa PK, concurrency, soft-delete, và cơ chế tenant isolation trước khi migrate schema.

## Decision
1. **PK**: `uuid` PostgreSQL, generated as **UUIDv7** (`LCMS.Domain.Common.UuidV7`) — time-ordered, no ULID string type.
2. **Naming**: snake_case tables/columns via `EFCore.NamingConventions` + Fluent `ToTable`.
3. **Concurrency (C-012)**: `row_version bytea` as EF concurrency token; stamped on save.
4. **No hard delete (C-013)**: `deleted_at` / `deleted_by`; DbContext rejects `EntityState.Deleted`.
5. **Tenant isolation (C-001)**:
   - `tenants.id` is the tenant root (no separate `tenant_id` on `tenants`).
   - All other business/financial entities inherit `TenantEntityBase.TenantId`.
   - EF Core global query filter: `DeletedAt == null` AND (`!HasTenant` OR `TenantId == current`).
6. **Bill** is Financial Anchor table `bills` with TD1 baseline fields (no derived money totals as SoT).

## Consequences
- Migrations and repositories must never call physical delete on financial rows.
- Requests without tenant context see unfiltered-by-tenant queries only when `ITenantContext.HasTenant` is false (bootstrap/admin paths must be explicit and locked down later).
- Switching to ULID would require a new ADR and data migration.

## Alternatives rejected
- ULID string PK — less native for PostgreSQL `uuid` indexes.
- SQL Server `rowversion` / PG `xmin` only — kept explicit `bytea row_version` to match TD1 field name.
- Hard delete + audit only — breaks TD1-DB-005 / C-013.
