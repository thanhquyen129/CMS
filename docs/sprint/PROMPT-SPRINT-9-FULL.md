# PROMPT — Pass 2 / Sprint 9 FULL Financial Control

Bạn đang làm **Pass 2 — Sprint 9 FULL** trong `c:\A1\git\cms`. Pass 1 S9 + Pass 2 S0–S8 đã trên `main`.

## Mục tiêu TD6 E11 đủ hơn thin
Reconciliation/exception/approval vận hành đủ: rule-based variance severity, SLA due dates, approval chains, link exception → object.

## Must ship
1. Reconciliation: multi-detail batch; auto-create Variance when unmatched; severity from amount threshold config.
2. Exceptions: severity/owner/due_at SLA; escalate stub (status); link to object_type/object_id; inbox filter by severity/status.
3. Approvals: multi-step stub (level 1/2) OR sequential approvers list; reject with reason; Permission still independent of Approval.
4. Block money confirm when open critical exception on same object (config flag, default on for Cost/Revenue confirm).
5. Tests: variance≠exception; approval≠permission; SLA fields; block-on-critical; tenant isolation.
6. `SPRINT-9-FULL-DOD.md` + handoff + PR. Suite xanh.

## Non-goals
Financial close changes (Sprint 10); Next.js inbox UI.

## Ràng buộc
Never secrets/alogex. Vietnamese errors. Read `SPRINT-9-DOD.md` deferred.
