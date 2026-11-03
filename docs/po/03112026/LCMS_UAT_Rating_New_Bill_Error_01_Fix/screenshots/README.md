# Screenshots

UAT schema hotfix (`service_selections.deleted_by`) đã apply trực tiếp trên `cms-db` lúc điều tra (2026-11-03 evening +07).

PO vui lòng chụp lại sau khi retest Bill `LSSTEST03112026001`:

1. `01_BUY_TryCalculate_Success.png` — Tính thử MUA HTTP 200, có Rating snapshot, chưa sinh Cost khi chưa Apply  
2. `02_SELL_TryCalculate_Success.png` — Tính thử BÁN HTTP 200  
3. `03_Apply_NoDoubleCount.png` — Sau Apply BUY/SELL (lặp), Chi phí/Doanh thu không cộng trùng  

Bằng chứng kỹ thuật kèm theo: log `42703 deleted_by` trong `00_README_…md`, migration `20261103180000`, test `NewBillRatingBuySellE2ETests` 2/2 PASS.
