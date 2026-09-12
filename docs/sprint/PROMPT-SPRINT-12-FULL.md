# PROMPT — Pass 2 / Sprint 12 FULL Hardening & UAT

Bạn đang làm **Pass 2 — Sprint 12 FULL** trong `c:\A1\git\cms`. **Đây là sprint cuối Pass 2.** Pass 1 S12 + Pass 2 S0–S11 đã trên `main` (hoặc base sau S11 FULL merge).

## Mục tiêu TD6 E14 / E15 / E16 đủ hơn thin
Audit FULL; integration recovery stub; NFR rate-limit + smoke; AC gate smoke; Vietnamese UX gaps; UAT harden.

## Must ship
1. **Audit FULL:** richer before/after JSON on money mutations; cover settlement finalize, document accept/match, AP/AR recognize, write-off; filterable `GET /api/audit-events` (action/object/correlation/date range).
2. **Integration recovery stub:** `integration_errors` + mark-retried / dead-letter; duplicate still 409 C-002; optional outbox stub (enqueue + process-once) — no broker.
3. **NFR:** rate-limit config (stricter money paths ok); security headers remain; one timed API smoke or benchmark note (not full soak).
4. **AC gate smoke:** AC-008 immutability, AC-007 settlement integrity, AC-009 idempotency still hold — focused tests.
5. Vietnamese UX terminology gaps filled; checklist in DoD.
6. Tests + `SPRINT-12-FULL-DOD.md` stating **Pass 2 COMPLETE** + residual backlog + handoff + PR. Suite green.

## Non-goals
Full broker/outbox topology; load/soak; Next.js UAT UI; inventing fake ledger rows.

## Ràng buộc
Never secrets/alogex. Do not regress JWT (Pass 2 S0). After merge, Pass 2 board = all Done.
