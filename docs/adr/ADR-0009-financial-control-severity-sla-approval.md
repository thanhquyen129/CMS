# ADR-0009: Financial control severity, SLA, multi-step approval, confirm block

- Status: Accepted
- Date: 2026-09-12
- Relates: TD1 D10; IDX-011; H-009 Permission ≠ Approval; Variance ≠ Exception; Sprint 9 FULL (Pass 2)

## Context
Pass 1 shipped reconciliation/variance/exception/approval thin APIs. Ops still needed: batch recon lines, severity from amount, SLA due defaults, escalate stub, multi-step approve, and a safety gate that critical open exceptions block Cost/Revenue confirm.

## Decision
1. **Variance severity** from `FinancialControl:VarianceSeverityThresholds` (Medium/High/Critical absolute amounts). Auto-created on unmatched/residual delta; never opens Exception.
2. **Exception object link** (`object_type`/`object_id`) + escalate stub (`escalated` status, optional severity bump) + default SLA hours by severity when `due_at` omitted.
3. **Approval multi-step stub**: `required_level` 1|2; each approve advances `current_level`; final approve sets approved. Reject requires reason. Still never touches Permission tables.
4. **Confirm block**: `BlockConfirmOnCriticalException` default **true** — Cost/Revenue confirm 409 VI while open/in_progress/escalated **critical** exception links the same object.

## Consequences
- Inbox/queue include escalated; dashboard open-exception count includes escalated.
- Thresholds/SLA hours are config-level (not per-tenant DB yet).
- Auto-exception from variance remains deferred (Variance ≠ Exception).

## Alternatives rejected
- Auto-open Exception from variance severity — breaks Variance ≠ Exception.
- Full RBAC-gated approval matrix — conflates Permission with Approval.
- Silent confirm despite critical exception — unsafe for money path.
