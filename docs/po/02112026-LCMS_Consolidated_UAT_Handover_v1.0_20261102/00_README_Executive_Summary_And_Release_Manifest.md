# LCMS-CONSOLIDATED-UAT-20261102-01 — Tóm tắt điều hành

Ngày bàn giao DEV: 2026-11-02. UAT của PO vẫn **tạm hoãn**. Cột `PO UAT` của hạng mục mới và retest giữ **NOT RUN**. `DEV PASS` không phải PO UAT PASS.

## Kết quả

Đợt này sửa các lệch đã chứng minh trên code: so sánh giá không khóa chiều tuyến, hồ sơ chi phí 3 nhóm không đọc phân bổ đã chốt, nhãn công nợ và tách P&L/AR, hàng đợi phê duyệt chỉ có tab chờ duyệt, form công nợ cho tạo khi không có Bill/khoản/chứng từ/đối tác, và CI tắt `dotnet test`.

Không có `UPDATE` hàng loạt `air`/`AIR`, không đổi `service_type` lịch sử sang `FREIGHT`, không xóa `staleAt`, không sửa Rating Snapshot, Rate Version `PUBLISHED`, Expected/Confirmed/Actual, phân bổ đã chốt, chứng từ đã duyệt, AP/AR hay Financial Close.

**Không deploy Production.** Push `main` sẽ kích hoạt GitHub Actions deploy `cms.lsslogistics.vn`. Đợt này chỉ commit trên nhánh local cho đến khi PO chấp nhận.

## Release

| Mục | Giá trị |
|---|---|
| Nhánh | `main` (chưa push) |
| Commit | Ghi ở cuối file sau khi commit |
| CI | `.github/workflows/ci.yml`: bỏ `if: false` trên `dotnet test`; thêm `npm test` |
| Postgres test | `LCMS_TEST_POSTGRES` hoặc cổng 5432 rồi 5433. DB tên `lcms_rate_ac30` / `lcms_rate_baseline_audit`, không đụng DB `lcms` của smoke |
| `/health` | Chưa gọi production. Job CI vẫn có health smoke local sau test |
| `dotnet test` local | 588 PASS, 1 FAIL (đã sửa), 2 SKIP. Test FAIL `CreditBlock_RoleGate_SoftDeleteOpenAr_And_Export` PASS lại sau khi Bill hoặc đối tác được tính là căn cứ hợp lệ |
| `npm test` | 47 PASS |

SKIP local là hai bài `RateScopeMigrationPostgresTests` vì máy DEV không mở được Postgres cô lập. Trên Actions, service Postgres `5432` và biến `LCMS_TEST_POSTGRES` được set để hai bài này chạy thật.

## R-01 … R-10

| Mã | Bảo vệ | Kết quả DEV |
|---|---|---|
| R-01 | BUY 120kg: 1.440.000 + 300.000, Net 1.740.000, VAT 139.200, Gross 1.879.200 | Không sửa số. Trace không còn bịa mã `TDNT_SGN_HAN` / `QT0001` khi thiếu liên kết |
| R-02 | BUY chỉ Cost, SELL chỉ Revenue, seed không trùng | Giữ guard hiện có. Suite rating liên quan PASS |
| R-03 | Bill lưu AIR, FREIGHT, kiện, 120kg | Context + measurement đã ghi ở `UpdateBillContext` / `OperationalCargoStore`. Màn hình không còn lấy phương thức thế cho loại dịch vụ |
| R-04 | Revenue 500.000 một khoản, không cộng 1.500.000 | KPI gắn nhãn số dư trạng thái hiện hành |
| R-05 | Shared 240.000 chia 120.000 x 2 | Không đổi số phiên đã chốt |
| R-06 | Profit 380.000 | Hồ sơ 3 nhóm đọc cùng `CostAllocationDetails` đã `Finalized` với hồ sơ tài chính |
| R-07 | Chứng từ Mode B 500.000, không tạo Revenue/Collection thứ hai | Không đổi workflow nhận/chấp nhận/khớp |
| R-08 | Exposure/AR 500.000, profit không đổi | Trần ghi nhận theo doanh thu nguồn |
| R-09 | Không phá lịch sử | Không migration dữ liệu tiền |
| R-10 | N:N và maker-checker | Nút Phê duyệt của người tạo bị disable, tooltip PC-21 |

## PROP

`PROP-01` và `PROP-02` chỉ có đánh giá tác động trong `02_PO_Decisions_DEC01_To_DEC04_And_Proposals_Assessment.md`. Không thêm cờ tắt module và không thêm validation bắt buộc mọi tenant.
