# PROMPT — Sprint 12 Hardening & UAT (paste vào New Chat / Cloud Agent)

Bạn đang làm **Sprint 12 — Hardening & UAT** trong repo `c:\A1\git\cms` (base `main` đã có Sprint 0–11). **Đây là sprint cuối Pass 1.**

## Mục tiêu TD6 Sprint 12
"Security/NFR tests; integration recovery; performance; audit; Vietnamese UX acceptance; UAT fixes" — Epics **E14, E15, E16**.

## DoD mỏng nhưng đủ (Pass 1)
1. **Audit skeleton:** `audit_events` table + write on key money mutations (cost create/confirm, revenue create, payment finalize, close snapshot) — actor/action/object/correlation id; query `GET /api/audit-events`.
2. **Integration idempotency skeleton:** `integration_records` (+ optional errors) với unique C-002; upsert API stub that rejects duplicates safely.
3. **Security/NFR smoke:**
   - Rate-limit middleware mỏng trên `/api/*` (fixed window, config)
   - Security headers middleware (nosniff, frame options, etc.)
   - Confirm `/health` `/ready` + correlation id still work
4. **Vietnamese UX acceptance checklist:** ensure error messages + `GET /api/terminology` cover terms used in Sprints 1–11; add missing keys; document review in DoD.
5. **Tests:** audit written on mutation; integration duplicate rejected; rate-limit or security header present; tenant isolation still green. Full `dotnet test` xanh.
6. `docs/sprint/SPRINT-12-DOD.md` + handoff stating **Pass 1 COMPLETE**; list Pass 2 backlog pointers. PR → main.

## Non-goals (Pass 2)
Full outbox/retry topology, load/soak tests, JWT/OIDC production, Next.js UAT UI, complete AC-001… matrix hardening.

## Ràng buộc
Never secrets/alogex. UUIDv7, soft-delete, tenant_id. After merge, Pass 1 board = all Done.
