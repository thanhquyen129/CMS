# ADR-0028 — Ambiguous match, tolerance, FX snapshot, approval, replay, idempotency

**Status:** Accepted  
**Date:** 2026-09-22  
**Relates:** ADR-0027, MASTER 06, MASTER 07, FA-06, FA-21, FA-23, PC-09, PC-12, PC-16, PC-17, PC-19…PC-23, PC-26, C-008, C-014

## Context

Matching, settlement, exceptions, and approvals already exist as separate queues. Package F closes the control rules that those screens were missing: do not pick a tied candidate, remember the tolerance that was used, settle another currency only with a stored rate, waive a critical exception only after approval, and do not create a second row when the same API event is sent again.

## Decision

1. `POST /api/document-matches/{id}/resolve` auto-picks only when one candidate has the smallest delta. Two candidates at that delta return `MATCH_AMBIGUOUS` and save nothing. The suggestion list and a manual detail stay available so an operator can still choose.
2. Each match detail stores `outcome_code` (`matched` or `matched_with_tolerance`) and `applied_tolerance`. Detail status stays `active` or `reversed`. The full policy registry remains package H. The threshold used is the session tolerance already stored on the match.
3. A payment or collection in another currency than the AP/AR is rejected with `Không phân bổ khác tiền tệ (C-014).` when no dated FX rate exists. When a rate exists, the allocation stores original amount, settled amount, rate, source, and rate date. `Amount` stays in the payment or collection currency so the cash ceiling is unchanged. AP/AR settled amount uses the converted figure. Cost and revenue rows are not rewritten for the FX difference.
4. A critical exception waiver sets status `waiting` and opens an approval. Other severities become `waived` immediately. A final approval of a waiting exception sets `waived`. Nothing is hard-deleted. Variance stays a separate object.
5. The creator cannot approve their own request when a user is present (`PC-21`). Header-only calls with no user are unchanged. The approval stores the object amount. If that amount changes before the decision, status becomes `needs_rereview` and the decision is rejected. An explicit required level is kept. When it is omitted, the existing amount matrix applies. Approval does not grant permissions.
6. Replay copies type, rule, bill, and notes into a new draft and increments the version. A completed session is not rewritten.
7. `idempotency_records` remembers an optional `Idempotency-Key` for financial-document receive and payment create. The same key returns the first id. A missing key keeps today’s create behavior. The wider ledger remains package H.

## Consequences

- Migration `ControlMatchFxApprovalIdempotency` adds match outcome columns, allocation FX snapshot columns, `approvals.object_fingerprint`, and `idempotency_records`.
- Control nav keeps only screens that already work. Work list and control report are not links. Variance queue stays beside exceptions.
