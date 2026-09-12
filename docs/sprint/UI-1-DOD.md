# UI-1 DoD — Bill hub

**Status:** Done  
**Date:** 2026-09-12  
**Plan:** `docs/sprint/PLAN-UI.md` · Prompt: `PROMPT-UI-1.md`

## Outcome
Ops/Finance login → `/bills` search → open Bill **Financial Profile** (Expected / Confirmed / Actual) + profitability on VPS. No fake totals.

## Checklist

| # | Criterion | Status |
|---|-----------|--------|
| 1 | `/bills` list + search via `GET /api/bills?q=` (BFF cookie → Bearer) | Done |
| 2 | `/bills/[id]` financial profile + profitability; maturity scannable; money `vi-VN` | Done |
| 3 | Labels from `GET /api/terminology`; empty/error/loading; list → detail; one primary path | Done |
| 4 | Bill nav enabled in shell; Dashboard still “sắp có” | Done |
| 5 | This DoD + handoff + README Pass UI + orchestration | Done |
| 6 | No backend invent unless blocked; `dotnet test` green | Done (API already existed) |

## APIs used
- `GET /api/bills?q=` — list/search
- `GET /api/bills/{id}` — header meta
- `GET /api/bills/{id}/financial-profile` — maturity + best-available buckets
- `GET /api/bills/{id}/profitability?view=best` — profitability by currency
- `GET /api/terminology` — CP6.5 labels

## UI files
- `apps/web/app/bills/page.tsx`, `apps/web/app/bills/[id]/page.tsx`
- `apps/web/lib/bills.ts`, `apps/web/lib/money.ts`
- `apps/web/components/AppShell.tsx` (shared shell; Bill link live)

## Non-goals (deferred)
U2 Cost/Revenue mutate, documents, queues, charts, OIDC.

## Verify
- Login still works; `/bills` lists/searches; detail shows maturity layers; `/health` OK.
