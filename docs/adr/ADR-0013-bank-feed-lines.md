# ADR-0013 — Bank feed lines (manual cash feed stub)

## Status
Accepted — 2026-09-12

## Context
Settlement UI has Payment/Collection; reconciliation sessions exist. Controllers still need a place to park **bank statement lines** before matching them to cash txns / AP-AR via reconciliation. Full open-banking sync is out of scope.

## Decision
- Table `bank_feed_lines`: value date, amount, currency, direction (`credit`/`debit`), optional bank reference / counterparty / description, status `unmatched|matched|ignored`.
- Manual create + list + ignore APIs. No CSV/provider sync in this slice.
- Reconciliation `sourceType=bank_line` references a line; when detail is fully matched, line status → `matched`.
- Bank feed ≠ Payment/Collection (Cost ≠ Payment still holds); matching does not invent cash txns.

## Consequences
- Controllers can nhập sao kê tay rồi đối soát.
- Auto-import / bank API / CSV = follow-up.
- Migration required on deploy.
