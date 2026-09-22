# ADR-0026 — Allocation bases, remainder, scope, and session status

**Status:** Accepted  
**Date:** 2026-09-22  
**Relates:** ADR-0012, MASTER 04, CT-03…CT-09, CT-18, CT-19, AC-OR-13, C-003, C-005

## Context

Sprint 4 allocates a shared cost by equal, quantity, or manual ratio, then finalizes. Package D adds measurement bases, a scoped Bill set, a remainder that keeps the sum equal to the source amount, and an open session before finalize. Separation of duties (W-F5) is not built yet.

## Decision

1. Bases stay `equal`, `quantity`, `manual_ratio`, and add `gross_kg`, `chargeable`, `cbm`, `package_count`, `teu`, `manual_percent`, `manual_amount`. Measurement bases read confirmed operational measurements. The client does not send those weights. A line with basis 0 receives 0 when the total is greater than 0.
2. A basis total of 0 or less is `ZERO_ALLOCATION_BASIS`. The engine does not fall back to an equal split.
3. `manual_percent` must sum to 100 (±0.01). `manual_amount` must sum to the allocatable amount.
4. Lines are ordered by Bill id. Each non-last positive line is rounded to 4 decimal places. The last positive line absorbs the remainder so the sum equals the source amount. `RoundingAdjustment` is allocated minus the proportional amount. The same ordering is used every time. VND stays at 4 decimal places so 50/50 and 25/75 remain exact.
5. Applicability is `explicit`, `leg`, `movement`, or `condition`. An empty detail list on a leg or movement uses exactly the linked Bills. A provided list may not add a Bill outside that set and may not pull every other linked Bill. Condition mode matches `Bill.ServiceTypeCode`. The tenant Bill list is not the default set.
6. A manual amount on an automatic basis needs a reason and `cost.allocation.override`. `OverrideBeforeAmount` stores the proportional amount before the override. A request with no user still follows the existing bootstrap allowance.
7. Statuses are `draft`, `calculated`, `pending_approval`, `finalized`, `cancelled`, `superseded`. Calculate does not supersede. Submit moves calculated to pending approval. Finalize still accepts draft, calculated, or pending approval, then locks the row. A second open session on the same cost is rejected. Re-allocation still supersedes the prior finalized row. Allocation does not create a new Cost. The creator can still finalize until package F adds separation of duties.

## Consequences

- Migration `AllocationBasesScopeRounding` adds `scope_id`, `condition_code`, and `override_before_amount`.
- Shared-cost detail shows Đối tượng, Mã, Số tiền, Tỷ lệ, Dư làm tròn, and the actions Tính phân bổ, Gửi duyệt, Hủy phiên, Chốt.
- Direct cost still cannot be allocated. Conservation still fails the request instead of saving a partial sum.
