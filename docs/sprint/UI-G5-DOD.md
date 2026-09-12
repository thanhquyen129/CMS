# UAT G5 — AP/AR tab Đã tất toán

**Status:** Done (FULL)  
**Date:** 2026-09-12  
**Source:** `docs/sprint/UAT-VPS-ONE-ROUND.md` gap G5

## Outcome
Operator audit lịch sử AP/AR **đã tất toán** trên desk `/ap-ar` và trên Bill — không mất sau khi settle.

## Checklist

| # | Criterion | Status |
|---|-----------|--------|
| 1 | `/ap-ar?status=settled` — filter Đã tất toán | Done |
| 2 | Filter Còn dư (default) / Đã tất toán / Tất cả giữ theo tab AP\|AR | Done |
| 3 | Empty còn dư → link sang sổ đã tất toán khi có data | Done |
| 4 | Cột Đã tất toán (`finalizedSettledAmount`) khi xem settled/all | Done |
| 5 | Bill panel hiện **cả** AP/AR còn dư + đã tất toán | Done |
| 6 | Label `partially_settled` → «Tất toán một phần» | Done |

## Non-goals
G6 settlement `billNo` · write-off UI · reverse allocation

## Verify
- `npm run build` apps/web
- VPS: settle hết → `/ap-ar` còn dư empty có link; `/ap-ar?status=settled` hiện hàng; Bill detail vẫn thấy AP/AR đã tất toán
