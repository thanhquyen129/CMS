# Screenshots (DEV verify trên UAT)

Bill: `LSSTEST031126001` (`https://cms.lsslogistics.vn`, admin@cms.local) — tương đương `LSSTEST03112026001` trên cms-db.

| File | Bằng chứng |
|------|------------|
| `01_BUY_TryCalculate_Success.png` | Tab **Tính giá & Tài chính** — **Tính thử giá mua** thành công: Giá mua FREIGHT Net **1.440.000 ₫**, VAT 8% **115.200 ₫**, Gross **1.555.200 ₫** (không HTTP 500). |
| `02_SELL_TryCalculate_Success.png` | **Tính thử giá bán** thành công: Giá bán FREIGHT Net **2.040.000 ₫**, VAT **163.200 ₫**, Gross **2.203.200 ₫**. |
| `03_Apply_NoDoubleCount.png` | Sau Apply BUY/SELL (API smoke lặp ×2): **Best Available Cost 1.440.000 ₫ · Revenue 2.040.000 ₫ · Profit 600.000 ₫** — không nhân đôi tổng khi Apply lại cùng rating (`createdCount:0`, `existingCount:1`). |

Bằng chứng API kèm Mục 4–5 trong `00_README_…md`. Schema EntityBase: không còn cột thiếu trên `service_selections` / audits / `surcharge_activation_migration_audits`.
