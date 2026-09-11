# PROMPT — Sprint 9 Financial Control (paste vào New Chat / Cloud Agent)

Bạn đang làm **Sprint 9 — Financial Control** trong repo `c:\A1\git\cms` (base `main` đã có Sprint 0–8).

## Mục tiêu TD6 Sprint 9
"Reconciliation; variance; exception; approval" — Epic **E11**.

## DoD mỏng nhưng đủ (Pass 1)
1. Entities + migration TD1 D10 (tối thiểu): `reconciliations`, `reconciliation_details`, `variances`, `exceptions`, `approvals`.
2. Tách **Variance ≠ Exception**; **Permission ≠ Approval** (approval workflow độc lập).
3. API tenant-scoped:
   - Create/start reconciliation session; add details (source/target/matched/variance)
   - Open exception (severity/owner/status); resolve/close stub
   - Request/approve/reject approval on a financial object ref (cost/revenue/document/settlement id + type) — không thay permission check
4. Vietnamese errors; soft-delete; row_version; tenant_id.
5. Tests: variance vs exception separate; approval independent of permission; cross-tenant isolation.
6. `docs/sprint/SPRINT-9-DOD.md`, handoff, PR → main. `dotnet test` xanh.

## Non-goals
Financial close snapshots (Sprint 10), full SLA inbox UI, JWT, Next.js.

## Ràng buộc
UUIDv7, không hard delete. Never secrets/alogex.
