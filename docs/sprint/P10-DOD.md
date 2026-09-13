# P10 — Reverse recognize AP/AR + sổ điều chỉnh DoD

**Ngày:** 2026-09-13  
**PO:** E09; C-013/C-015 · Checklist `PO-GAP-CHECKLIST.md`

## Done
1. Ledger `accounts_payable_adjustments` / `accounts_receivable_adjustments` (adjustment | write_off | reverse_recognize).
2. `POST …/reverse-recognize` — soft reverse; restore exposure open; block if settled > 0.
3. Adjust + write-off ghi ledger; `GET …/adjustments`.
4. UI **Đảo ghi nhận** trên `/ap-ar`.

## Verify
- `dotnet test` filter SprintP10 — 2 passed.

## Non-goals
- Approval gate cho reverse lớn (P03 pattern deferred).
- Dated adjustment asOf (P17).
