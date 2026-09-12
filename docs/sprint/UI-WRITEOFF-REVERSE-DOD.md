# UI Write-off + Đảo phân bổ (follow-up ngoài G1–G6)

**Status:** Done  
**Date:** 2026-09-12  
**Source:** `docs/handoff.md` / `UAT-VPS-ONE-ROUND.md` follow-ups (ngoài UAT G1–G6)

## Outcome
Operator **xóa nợ** phần dư nhỏ AP/AR (điều chỉnh + lý do, không giả tiền mặt) và **đảo phân bổ** thanh toán/thu tiền (nháp hoặc đã chốt) từ UI — API Pass 2 S8 sẵn.

## Checklist

| # | Criterion | Status |
|---|-----------|--------|
| 1 | BFF `POST …/payment-allocations\|collection-allocations/{id}/reverse` `{ reason }` | Done |
| 2 | BFF `POST …/accounts-payable\|accounts-receivable/{id}/write-off` `{ amount, reason }` | Done |
| 3 | Chi tiết payment/collection: nút Đảo phân bổ (draft + finalized); lý do bắt buộc | Done |
| 4 | `/ap-ar` cột Thao tác: Xóa nợ khi outstanding > 0; trần stub 1000 (ADR-0008) | Done |
| 5 | Copy CP6.5: xóa nợ ≠ Payment/Collection; đảo trả outstanding (finalized) | Done |
| 6 | Empty/error thật; không invent money API | Done |

## Non-goals
Bank feed · write-off approval queue · reverse recognize · đổi MaxWriteOffAmount qua UI

## Verify
- `npm run build` trong `apps/web`
- VPS (sau deploy): `/ap-ar` xóa nợ; `/settlements/payments|collections/{id}` đảo phân bổ
