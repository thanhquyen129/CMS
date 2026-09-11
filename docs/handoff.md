# Handoff

## 2026-09-12 — Sprint 2 Operational Reference (E03/E14)

### User
Implement Sprint 2: orders/shipments + N:N links to Bill; idempotent order upsert; bill graph; isolation/idempotency/graph tests; DoD + ship via PR.

### Done
- Domain: `Order`, `Shipment`, `OrderBillLink`, `BillShipmentLink` (UUIDv7, tenant_id, soft-delete, row_version).
- Migration `Sprint2_OperationalReference` (`orders`, `shipments`, `order_bill_links`, `bill_shipment_links`).
- CQRS + API: order upsert/list/get; shipment upsert; link Order↔Bill / Bill↔Shipment; `GET /api/bills/{id}/graph`.
- Idempotent upsert by `(tenant_id, source_system, external_id)`; VI validation; tenant filter + `X-Tenant-Id`.
- Tests: 16 passed (4 new Sprint 2 — isolation, idempotency, graph, graph cross-tenant).
- DoD: `docs/sprint/SPRINT-2-DOD.md`.

### Files / API
- APIs: `/api/orders`, `/api/shipments`, `/api/orders/{orderId}/bills/{billId}`, `/api/bills/{billId}/shipments/{shipmentId}`, `/api/bills/{id}/graph`
- Migration: `20260911182340_Sprint2_OperationalReference`
- Endpoints: `OperationalReferenceEndpoints`

### Verify
- `dotnet test Cms.sln -c Release` → 16 passed

### Deferred / Next
- transport_legs / movements (deferred)
- Cost Expected on Bill (Phase 2)
- JWT/OIDC replace header bootstrap

---

## 2026-09-12 — Sprint 1 Identity + Master Data (E01/E02)


### User
Implement Sprint 1: Tenant/user/access; Organization/BusinessParty/Currency; permission skeleton; isolation tests; ship.

### Done
- Domain: `Role`, `Permission`, `RolePermission`, `UserRole`, `Currency`; User polished.
- Migration `Sprint1_IdentityMaster` (roles, permissions, role_permissions, user_roles, currencies).
- CQRS + API: users, roles (+ assign), organizations CRUD, business-parties CRUD, currencies list/upsert.
- `CreateTenant` seeds Admin + core Action permissions; `IPermissionService` gates Bill create.
- Bootstrap: `X-Tenant-Id` + optional `X-User-Id` (JWT deferred). Soft allow when no user header.
- Tests: 12 passed (cross-tenant User/Org/Party; permission 403 VI + correlation id).
- DoD: `docs/sprint/SPRINT-1-DOD.md`.

### Files / API
- APIs: `/api/users`, `/api/roles`, `/api/users/{id}/roles/{roleId}`, `/api/organizations`, `/api/business-parties`, `/api/currencies`
- Identity: `PermissionService`, `TenantAccessSeeder`, `PermissionCodes`
- Migration: `20260911181636_Sprint1_IdentityMaster`

### Verify
- `dotnet test Cms.sln -c Release` → 12 passed
- Actions: https://github.com/thanhquyen129/CMS/actions
- Health: http://194.233.89.26/health (post-deploy)

### Next
- JWT/OIDC replace header bootstrap
- Cost Expected on Bill
- Full Data Scope matrix / Approval (deferred)

---

## 2026-09-12 — Fix CI Health smoke race (exit 7)

### User
Screenshot: Actions CI failed on `test` (exit 7); `deploy` skipped.

### Cause
`curl` hit `/health` ~1s before `dotnet run` finished migrate+listen (`curl: (7) Failed to connect`).

### Done
- CI Health smoke: poll `/health` up to 60s, `--no-build`, dump smoke log on failure; `ASPNETCORE_ENVIRONMENT=Production`.

### Files
- `.github/workflows/ci.yml`

### Verify
- Re-run after push: https://github.com/thanhquyen129/CMS/actions

### Next
- Confirm `CMS_DEPLOY_SSH_KEY` so `deploy` can run after green `test`

---

## 2026-09-12 — Close Sprint 0 (TD6 Foundation)

### User
Close Sprint 0: observability skeleton (E14), terminology contract (E16), real Actions deploy (E15), env example, DoD checklist; ship.

### Done
- Serilog JSON console + `CorrelationIdMiddleware` + `RequestLoggingMiddleware`; `/metrics` process placeholder.
- `VietnameseUiTerms` (CP6.5) + `GET /api/terminology`.
- CI `deploy` job: SSH rsync → `/opt/cms`, compose up (excludes `infra/.env`); docs for `CMS_DEPLOY_SSH_KEY`.
- `infra/.env.example`, `docs/sprint/SPRINT-0-DOD.md`.
- Tests: 6 passed (terminology + correlation ID coverage).

### Files / API
- Middleware: `CorrelationIdMiddleware`, `RequestLoggingMiddleware`
- Domain: `src/LCMS.Domain/Terminology/VietnameseUiTerms.cs`
- Endpoints: `/api/terminology`, `/metrics`
- Ops: `.github/workflows/ci.yml`, `docs/ops/github-actions.md`, `docs/ops/vps-bootstrap.md`

### Verify
- `dotnet test Cms.sln -c Release` → 6 passed
- Operator fallback deploy (tar/scp + compose) OK: `/health`, `/ready`, `/api/terminology`, `/metrics`, correlation header
- Actions: https://github.com/thanhquyen129/CMS/actions — require secret `CMS_DEPLOY_SSH_KEY` for automated deploy job

### Next
- Confirm GitHub secret `CMS_DEPLOY_SSH_KEY` so Actions `deploy` succeeds without operator fallback
- Sprint 1: User/Permission/JWT (replace `X-Tenant-Id`)
- Cost Expected on Bill

---

## 2026-09-12 — Bill thật trên DB (migration + API Tenant/Bill)

### User
Làm bước 1–2: EF migration InitialTd1 + API Tenant/Bill thật + test cô lập tenant.

### Done
- Migration `InitialTd1` (`src/LCMS.Infrastructure/Persistence/Migrations/`).
- Compose: `infra/docker-compose.dev.yml` (Postgres local), host compose thêm `db` + migrate on startup.
- CQRS: `CreateTenant` / `GetTenantById` / `CreateBill` / `GetBillById` → `ILcmsDbContext`.
- API: `POST/GET /api/tenants`, `POST/GET /api/bills` (Bill cần `X-Tenant-Id`).
- `/ready` kiểm tra kết nối DB; `Database:MigrateOnStartup`.
- Tests: `tests/LCMS.Api.Tests` — 4 passed (cross-tenant 404, thiếu tenant 401, duplicate 409).
- CI: Postgres service + `dotnet test` + health smoke với migrate.

### Verify
- `dotnet test Cms.sln -c Release` → 4 passed
- Local DB: `docker compose -f infra/docker-compose.dev.yml up -d` rồi chạy API
- Actions: https://github.com/thanhquyen129/CMS/actions

### Next
- Wire deploy job → `/opt/cms` (compose có Postgres)
- JWT claims thay `X-Tenant-Id`
- Cost Expected trên Bill (Phase 2)

---

## 2026-09-12 — TD1 Clean Architecture scaffold (Tenant + Bill)

### User
Khởi tạo Solution .NET Clean Architecture 4 project theo TD1; mẫu Entity Tenant/Bill + DbContext PostgreSQL; CQRS/MediatR; FluentValidation tiếng Việt; EF global tenant filter; exception middleware + Correlation ID.

### Done
- Solution `Cms.sln`: `LCMS.Domain`, `LCMS.Application`, `LCMS.Infrastructure`, `LCMS.Api` (.NET 8). Removed bootstrap `Cms.Api`.
- Domain: `Tenant`, `Bill` (TD1 baseline fields), stubs `Organization`/`User`/`BusinessParty`/`Cost`/`Revenue`; `UuidV7`, `row_version`, soft-delete (C-013), no hard delete.
- Infrastructure: `LcmsDbContext` + Fluent configs + snake_case + PostgreSQL; global query filter C-001 + soft-delete.
- Application: MediatR + FluentValidation (VI messages) + sample `CreateBillCommand`.
- API: DI, `ExceptionHandlingMiddleware` (JSON VI + correlationId), `TenantResolutionMiddleware` (`X-Tenant-Id`), `/health` `/ready`.
- ADR: `docs/adr/ADR-0001-td1-identity-tenancy-postgres.md`
- Docker/CI updated to `LCMS.Api`.

### Files / schema
- Tables mapped: `tenants`, `bills`, `organizations`, `users`, `business_parties`, `costs`, `revenues`
- Conn string: `ConnectionStrings:LcmsDb`
- Migration chưa tạo — follow-up: `dotnet ef migrations add InitialTd1`

### Verify
- `dotnet build Cms.sln -c Release` (local OK)
- After deploy: http://194.233.89.26/health , `/ready`
- Actions: https://github.com/thanhquyen129/CMS/actions

### Next
- EF migration + Postgres on host
- Wire CreateBill handler to DbContext
- Identity/JWT claims → replace header tenant bootstrap

---

## 2026-09-12 — Wipe VPS + deploy CMS bootstrap

### User
Wipe `194.233.89.26`; update all IPs in rules to `194.233.89.26`.

### Done
- Rules: removed other IPv4 (`217.216…`); CMS production IP consistently `194.233.89.26` (IPv6 NIC kept).
- VPS wipe: stopped/removed `a1-api`/`a1-postgres`, volume `infra_a1_pgdata`, deleted `/opt/sanlogistics`, created `/opt/cms`.
- Deployed CMS API bootstrap via Docker Compose on host; verified `/health` and `/ready`.

### Verify
- http://194.233.89.26/health
- http://194.233.89.26/ready
- Actions: https://github.com/thanhquyen129/CMS/actions

### Next
- Phase 1: Identity/Tenant/Master + Bill anchor
- Wire self-hosted runner / Actions deploy for `/opt/cms`

---

## 2026-09-12 — Kickoff CMS

### User
Kickoff: đặt tên Cost Management System (CMS); update rules từ alogex → CMS; đọc BA docs; lập kế hoạch; tạo repo GitHub CMS; clear + deploy lên VPS theo hailybato.

### Done
- Rules: renamed product to CMS; host `194.233.89.26` → `/opt/cms`; repo `thanhquyen129/CMS`; expert stance → financial control (Bill-centric).
- BA indexed; implementation plan written; GitHub private repo created.
- VPS wipe + deploy completed in follow-up turn after PO confirm.
