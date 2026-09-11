# PROMPT — Pass 2 / Sprint 4 FULL Cost

Bạn đang làm **Pass 2 — Sprint 4 FULL** trong `c:\A1\git\cms`. Pass 1 S4 + Pass 2 S0–S3 đã trên `main`.

## Mục tiêu TD6 E05/E06 đủ hơn thin
Cost lifecycle + allocation production-grade: multi-bill shared allocation rules, supersede reallocations, FX stub, approval gate optional.

## Must ship
1. **Allocation rules:** basis types `equal`, `quantity`, `manual_ratio`; reject finalize if basis missing/zero (C-006); conservation after rounding (C-005) with explicit rounding remainder line.
2. **Reallocation:** new `cost_allocations` version with `supersedes_allocation_id`; prior finalized stays history (no silent overwrite).
3. **Shared cost:** require ≥2 eligible bills; Direct cost must have bill_id.
4. **FX stub:** if currency ≠ tenant base (config `VND`), require `fx_rate` decimal on cost create/confirm; store `base_amount`.
5. **Approval hook:** if cost amount ≥ threshold (config), require Approval status `approved` before confirm/actualize (reuse Sprint 9 approvals) — or document skip if threshold=0 default off.
6. Tests: conservation; reallocation history; C-006 block; FX required; tenant isolation; Single Economic Cost.
7. `SPRINT-4-FULL-DOD.md` + handoff + PR. `dotnet test` xanh.

## Non-goals
Full multi-currency ledger, Next.js.

## Ràng buộc
Never secrets/alogex. Vietnamese errors. Read `SPRINT-4-DOD.md` deferred.
