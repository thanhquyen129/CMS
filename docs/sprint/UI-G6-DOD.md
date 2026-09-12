# UAT G6 — Settlement list hiện `billNo`

**Status:** Done (FULL)  
**Date:** 2026-09-12  
**Source:** `docs/sprint/UAT-VPS-ONE-ROUND.md` gap G6

## Outcome
Operator đọc sổ thanh toán/thu tiền và nhận ra **số Bill** ngay trên cột Bill — không chỉ link «Mở Bill».

## Checklist

| # | Criterion | Status |
|---|-----------|--------|
| 1 | `PaymentDto` / `CollectionDto` trả `billNo` (join Bill) | Done |
| 2 | List + get payment/collection đều có `billNo` | Done |
| 3 | Không gắn Bill → `billNo` null | Done |
| 4 | `/settlements` cột Bill = `billNo` (link) | Done |
| 5 | Chi tiết payment/collection hiện `billNo` | Done |
| 6 | Test `SettlementBillNoTests` | Done |

## Non-goals
Write-off UI · reverse allocation UI · bank feed · đổi schema Payment

## Verify
- `dotnet test --filter SettlementBillNo`
- `npm run build` apps/web
- VPS: `/settlements` cột Bill hiện số Bill (vd. `UAT-20260912-…`) thay vì «Mở Bill»
