# PROMPT — Pass 2 / Sprint 11 FULL Financial Profile & Reporting

Bạn đang làm **Pass 2 — Sprint 11 FULL** trong `c:\A1\git\cms`. Pass 1 S11 + Pass 2 S0–S10 đã trên `main`.

## Mục tiêu TD6 E13 đủ hơn thin
Bill financial profile + dashboard + queues vận hành đủ: asOf maturity reconstruction tốt hơn; FX/base roll-up stub; P&L từ close snapshots; queue filters đầy đủ hơn.

## Must ship
1. **Financial profile FULL:** asOf reconstruct maturity buckets from history where available (not only EffectiveDate filter); allocated cost + settlement outstanding; document residual limits in DoD.
2. **Dashboard:** Best Available totals + optional base-currency roll-up stub (reuse Settlement/Cost FX stub pattern; ADR note); counts include variances open + overdue exceptions.
3. **Queues:** exceptions (status/severity/overdue/objectType), approvals (status/objectType/requiredLevel), optional reconciliations-open stub.
4. **Close snapshot P&L stub:** `GET /api/financial-closes/{id}/pnl` or snapshot detail roll-up from immutable snapshot metrics — derived read only.
5. Derived read only — never write totals onto Bill. Vietnamese labels/errors.
6. Tests + `SPRINT-11-FULL-DOD.md` + handoff + ADR if FX/P&L shape irreversible + PR. Suite xanh.

## Non-goals
Next.js dashboard UI; Sprint 12 hardening/NFR load; inventing fake ledger rows.

## Ràng buộc
Never secrets/alogex. Read `SPRINT-11-DOD.md` deferred. Tenant isolation C-001.
