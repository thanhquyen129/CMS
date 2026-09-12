# PROMPT — Pass 2 / Sprint 12 FULL Hardening & UAT

Bạn đang làm **Pass 2 — Sprint 12 FULL** trong `c:\A1\git\cms`. Pass 1 S12 + Pass 2 S0–S11 đã trên `main`. **Đây là sprint cuối Pass 2.**

## Mục tiêu TD6 E14–E16 đủ hơn thin
Audit/outbox/integration recovery; NFR hardening; AC gate smoke; Vietnamese UX acceptance FULL.

## Must ship
1. **Audit FULL:** richer before/after JSON on money mutations; cover settlement finalize, document accept/match, AP/AR recognize, write-off; filterable `GET /api/audit-events` (action/object/correlation/date range).
2. **Integration recovery stub:** `integration_errors` + mark-retried / dead-letter status; duplicate still 409 C-002; optional outbox table stub (enqueue + process-once) — no broker required.
3. **NFR:** rate-limit config (per-path or stricter money paths); security headers remain; document SLO smoke (latency not a full soak — add one timed API smoke test or benchmark stub note).
4. **AC gate smoke tests:** assert AC-008 close immutability, AC-007 settlement integrity (finalize-only outstanding), AC-009 idempotency (retry finalize/integration) still hold — thin focused tests.
5. **Vietnamese UX:** terminology gaps from S0–S11 FULL filled; checklist in DoD.
6. Tests + `SPRINT-12-FULL-DOD.md` stating **Pass 2 COMPLETE** + residual backlog + handoff + PR. Suite xanh.

## Non-goals
Redis distributed rate-limit; full load/soak farm; Next.js UAT UI polish (Pass UI track); inventing new money SoT.

## Ràng buộc
Never secrets/alogex. JWT already on main (Pass 2 S0) — do not regress. After merge, Pass 2 board = all Done.
