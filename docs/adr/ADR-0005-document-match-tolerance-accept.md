# ADR-0005 — Document match methods, tolerance policy, accept-before-match

- Status: Accepted
- Date: 2026-09-12
- Relates: TD1 D07 / C-007 / C-003 / C-004 / AC-005 / IDX-006 / BR-FIN-023; Sprint 6 FULL (Pass 2)

## Context

Pass 1 Document Matching used a single `manual` method and tolerance stub = 0. Production matching needs explicit link shapes (line↔line / line↔Cost / line↔Revenue), configurable tolerance, accept-before-match, duplicate control, and reversible match details without inventing Cost/Revenue.

## Decision

1. **Match methods** are explicit API values: `line_to_line` | `line_to_cost` | `line_to_revenue`. Detail targets must match the method; Cost/Revenue links are link-only (C-003/C-004).
2. **Tolerance policy** via `Documents` config: `DefaultToleranceAbsolute`, `DefaultTolerancePercent` (effective = max(absolute, amount×%/100)). Per-match override allowed on start. C-007 still rejects beyond effective tolerance.
3. **Accept-before-match** (`RequireAcceptBeforeMatch`, default true): Received ≠ Accepted; matching requires Accepted on involved documents.
4. **Duplicate control** (`EnforceDuplicateControl`, default true): reject active duplicate `(document_type, document_no, counterparty_id)` within tenant (IDX-006 / BR-FIN-023). Cancelled/voided docs do not block re-receive.
5. **Reverse/cancel** are soft statuses on match detail / match session / document (`detail_status`, `match_status`, `record_status`) — no hard delete (C-013 / RV-003).
6. **Confirm session** (P09): `draft` → `confirmed` when ≥1 active detail; freezes add-detail; reverse+cancel still allowed for remediation. **Suggest-within-tolerance** is read-only candidates (|Δ| ≤ effective tolerance); operator adds details manually — no auto-apply / no invent Cost/Revenue.

## Consequences

- Pass 1 clients using `matchMethod=manual` must migrate to one of the three methods.
- Default tolerance remains 0 (safe); ops can raise absolute/percent deliberately.
- AP/AR recognition remains Sprint 7 FULL (non-goal here).
- Confirm does not equal Recognized; P08 exposure create remains a separate step.
