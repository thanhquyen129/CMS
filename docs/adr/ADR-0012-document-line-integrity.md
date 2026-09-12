# ADR-0012 — Document line integrity (sum vs header, edit/delete, counterparty)

- Status: Accepted
- Date: 2026-09-12
- Relates: TD1 D07 / C-014 / C-013 / AC-005 / ADR-0005; UAT G3 FULL extension

## Context

G3 shipped add-line UI with soft warn when Σ lines ≠ header. Operators need edit/delete, party picker on receive, and a hard money control so Accept/Match do not proceed on incomplete line cover. BA does not name “sum=header”; operable finance practice does for invoice control. Lines have no `CounterpartyId` (header only).

## Decision

1. **Draft (not_accepted):** Add/Update amounts must keep `Σ line.Amount ≤ document.TotalAmount` (same currency, C-014). Delete allowed when `MatchedAmount = 0` and no match-detail rows reference the line.
2. **Accept gate:** `Σ line.Amount == document.TotalAmount` (rounded 4 dp). If `TotalAmount > 0`, at least one line required. Message VI rõ số lệch.
3. **After Accepted:** No add / delete / amount change. Description and cost/revenue type codes may update only while unmatched (`MatchedAmount = 0`).
4. **Matched line:** No edit/delete (reverse match detail first).
5. **Counterparty:** Header `CounterpartyId` only. Receive validates party exists + `IsActive` when provided. UI party picker on receive (list `/api/business-parties`).
6. **Delete:** Soft-delete unmatched draft lines (C-013 — no physical DELETE). Soft cancel remains on document header (ADR-0005).
7. **Audit:** `financial_document_line.add|update|delete` with before/after JSON.

## Consequences

- Clients that Accepted then added lines must **add lines before Accept**.
- Partial line entry is fine until Accept; overshoot rejected earlier.
- Party optional on receive (nullable); duplicate control still uses counterparty when set (IDX-006).

## Alternatives rejected

- Enforce Σ == Total on every mutate → blocks incremental entry.
- Soft warn forever → weak go-live control.
- Party on each line → not in D07; header sufficient.
