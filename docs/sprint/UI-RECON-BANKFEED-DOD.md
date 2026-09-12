# UI Reconciliation + Bank feed — Definition of Done

**Status:** Done  
**Date:** 2026-09-12  
**ADR:** `docs/adr/ADR-0013-bank-feed-lines.md`

## Outcome
Controller mở / làm việc phiên **Đối soát** trên UI; nhập tay **sao kê ngân hàng** rồi khớp qua đối soát (`bank_line`). Không còn chỉ API.

## Checklist

| # | Criterion | Status |
|---|-----------|--------|
| 1 | `/queues/reconciliations`, `/reconciliations`, `/reconciliations/new`, `/reconciliations/{id}` | Done |
| 2 | BFF create / add detail / complete | Done |
| 3 | `POST /api/reconciliations/{id}/complete` | Done |
| 4 | Bank feed: entity + migration + create/list/ignore APIs | Done |
| 5 | `/bank-feed` UI + BFF; `sourceType=bank_line` khi khớp đủ → matched | Done |
| 6 | Nav + dashboard links; copy VI CP6.5; empty/error thật | Done |
| 7 | Test `BankFeedAndCompleteReconciliationTests` | Done |

## APIs
- Existing: `/api/reconciliations`, `/api/queues/reconciliations`
- New: `POST .../complete`; `/api/bank-feed/lines` (+ ignore)

## Non-goals
CSV/open-banking sync · auto-match engine · write-off approval gate · variance inbox riêng

## Verify
```bash
dotnet test --filter BankFeedAndComplete
npm run build   # apps/web
```
