# PROMPT — Sprint 11 Financial Profile & Reporting (paste vào New Chat / Cloud Agent)

Bạn đang làm **Sprint 11 — Financial Profile & Reporting** trong repo `c:\A1\git\cms` (base `main` đã có Sprint 0–10).

## Mục tiêu TD6 Sprint 11
"Bill financial profile; dashboard; control queues; reporting projections" — Epic **E13**.

## DoD mỏng nhưng đủ (Pass 1)
1. Mở rộng read model (không SoT trên Bill):
   - Enhance `GET /api/bills/{id}/financial-profile` (đã có từ Sprint 5): thêm maturity breakdown, allocated cost, settlement outstanding liên quan Bill nếu có FK/link mỏng; as-of timestamp.
2. Dashboard API mỏng (tenant-scoped):
   - `GET /api/dashboard/summary` — counts: bills, open exceptions, pending approvals, open closes; totals Best Available cost/revenue/profit (theo currency hoặc base stub).
3. Control queues:
   - `GET /api/queues/exceptions` (open)
   - `GET /api/queues/approvals` (pending)
4. Projection stub: `GET /api/bills/{id}/financial-profile?asOf=` optional filter (nếu không đủ data thì document deferred partial).
5. Tests: profile + dashboard tenant isolation; queues filter status.
6. `docs/sprint/SPRINT-11-DOD.md`, handoff, PR → main. `dotnet test` xanh. Vietnamese labels via terminology where exposed.

## Non-goals
Full Next.js UI, Sprint 12 hardening/NFR load tests, JWT.

## Ràng buộc
Derived read only — không ghi derived totals vào Bill. Never secrets/alogex.
