# PROMPT — Pass 2 / Sprint 8 FULL Settlement

Bạn đang làm **Pass 2 — Sprint 8 FULL** trong `c:\A1\git\cms`. Pass 1 S8 + Pass 2 S0–S7 đã trên `main`.

## Mục tiêu TD6 E10 đủ hơn thin
Settlement production-grade: unapplied cash, multi-allocation, FX on settlement, write-off stub, idempotent finalize.

## Must ship
1. Unapplied amount on payment/collection; allocate remaining in subsequent allocations until fully applied.
2. Multi-AP/AR allocation from one payment/collection; reject over-allocate (C-008); partial OK.
3. FX stub on settlement amounts → base_amount (same pattern Cost/Revenue ADR-0004 style).
4. Write-off stub: small remainder write-off with reason (creates adjustment note, not silent outstanding wipe).
5. Idempotent finalize (repeat finalize same allocation → safe no-op or 409 clear).
6. Tests: unapplied flow; multi allocate; over reject; write-off; tenant isolation; outstanding after settle.
7. `SPRINT-8-FULL-DOD.md` + handoff + PR. Suite xanh (không regress).

## Non-goals
Bank feed reconciliation (Sprint 9), Next.js.

## Ràng buộc
Never secrets/alogex. Vietnamese errors. Read `SPRINT-8-DOD.md` deferred.
