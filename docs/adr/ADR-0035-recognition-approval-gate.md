# ADR-0035: Approval threshold blocks recognition and cost-allocation finalize

- Status: Accepted
- Date: 2026-09-24
- Relates: ADR-0004 (confirm threshold), ADR-0026 (allocation `pending_approval`)

## Context
`ConfirmApprovalThresholdBase` already blocks Expected→Confirmed when `BaseAmount` exceeds it and `ApprovalStatus` is not `approved`. Recognition of AP/AR and finalize of a shared-cost allocation did not consult that gate, so a controller could book a payable or lock an allocation while the line was still pending.

## Decision
1. Null threshold: recognition and allocation finalize stay as they are.
2. Payable exposure linked to a cost uses the cost confirm gate (tenant override, else `Cost:ConfirmApprovalThresholdBase`). Receivable exposure linked to a revenue uses the revenue threshold when set, otherwise the same tenant/cost threshold.
3. An exposure with no source line is judged on its ceiling (`Amount`), not the slice. Over threshold requires an approved Approval on `payable_exposure` or `receivable_exposure` before recognize.
4. A blocked recognize or cost-allocation finalize writes an audit row in the same save that marks the source `pending`, then returns 409. No AP/AR row and no finalized allocation are created.
5. Submit of a calculated cost allocation creates a `cost_allocation` Approval. Finalize from `pending_approval` requires that Approval to be `approved`. Finalize from draft/calculated still runs, and is blocked only by the cost threshold.

## Consequences
- Approving a cost or revenue does not by itself recognize AP/AR. The operator recognizes again after approval.
- Cash allocation finalize is unchanged: outstanding moves only after an AP/AR row exists, and that row cannot be created over the threshold without approval.
- Dev/test with a null threshold keep the previous recognize path.

## Alternatives rejected
- Auto-apply recognition when the approval is decided (write-off pattern). Recognition amount, due date, and partial slices stay an explicit operator act.
- Blocking every finalize, including amounts under the threshold. Day-to-day settlement under the policy must stay one step.
