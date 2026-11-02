# Ma trận kiểm thử DEV — delta, fast-track, hồi quy, CI

`PO UAT` của các dòng dưới đây là **NOT RUN**. File Excel cùng thư mục đã ghi `DEV Test = PASS` và giữ `PO UAT = NOT RUN`.

- `LCMS_DEV_UAT_Followup_Matrix_v1.0_20261026.xlsx`: sheet `04_UAT_32`, 32 dòng, cột `DEV Test` mới.
- `LCMS_DEV_UAT_Correction_Matrix_v1.0_20261027.xlsx`: sheet `04_UAT`, 37 dòng `FU27-TC-*`.

## TC-01 … TC-25

| TC | Kết quả DEV | Ghi chú |
|---|---|---|
| TC-01 | PASS | Bill context lưu mode, service, kiện, trọng lượng qua PATCH context + cargo store. `BillCreateTotalsTests` PASS |
| TC-02 | PASS | Bỏ fallback `billType` và `transportMode` vào ô Loại dịch vụ |
| TC-03 | PASS | Dropdown mode từ catalog active. API từ chối mode ngoài catalog |
| TC-04 | PASS | Compare dùng cùng `BillRateSuggestion` với Rating Bill; có `serviceTypeCode` |
| TC-05 | PASS | `ConsolidatedUat20261102Tests.Sell_compare_does_not_price_the_reverse_route` |
| TC-06 | PASS | Guard BUY/SELL hiện có, suite rating PASS, không đổi số R-01 |
| TC-07 | PASS | Nhãn base/surcharge theo `rating_detail.source_type`. Bỏ mã bịa khi thiếu card |
| TC-08 | PASS | UI FIXED ghi số tiền/khoản; đơn giá/kg là "Đơn giá quy đổi tham khảo" |
| TC-09 | PASS | Ngày form so sánh/công nợ/doanh thu dùng `DateInputVi` DD/MM/YYYY, API ISO |
| TC-10 | PASS | Typeahead doanh thu lọc `roleCode=customer` (customer, payer, bill-to, shipper, consignee). Vendor cùng tên không vào danh sách |
| TC-11 | PASS | Audit `revenue.confirm` / `revenue.actualize` và panel lịch sử |
| TC-12 | PASS | Nhãn KPI "Số dư … (trạng thái hiện hành)" |
| TC-13 | PASS | Không đổi phiên 240.000. Maker không bấm được Phê duyệt |
| TC-14 | PASS | `finalizedAllocations` dùng chung hồ sơ tài chính và hồ sơ 3 nhóm |
| TC-15 | PASS | Tab Chờ duyệt / Đã duyệt / Từ chối / Tất cả |
| TC-16 | PASS | Ba chiều nhận/chấp nhận/khớp giữ độc lập. Có câu dẫn sang quyền thu dự kiến |
| TC-17 | PASS | API từ chối exposure không có Bill, khoản, chứng từ hoặc đối tác |
| TC-18 | PASS | Recognize vẫn đi từ exposure. Không tự sinh AR khi accept chứng từ |
| TC-19 | PASS | Trần AR theo doanh thu nguồn; idempotency key sẵn có trên recognize |
| TC-20 | PASS | JSON tenant `revenueMaturityModel` two/three. Mặc định three |
| TC-21 | PASS | Nhãn Việt hóa và hai bảng P&L / công nợ |
| TC-22 | NOT RUN | Thu 200.000 chưa có dữ liệu UAT. Không bịa số |
| TC-23 | NOT RUN | Thu phần còn 300.000 chưa UAT |
| TC-24 | PASS | Chia doanh thu giữ invariant ở test mapping hiện có trong suite. Không đổi công thức |
| TC-25 | PASS | Báo cáo tuyến lấy `bill.route_code` hoặc origin-destination khi chưa có `route_id` |

## LCMS-FT-01 … LCMS-FT-08

| ID | DEV |
|---|---|
| FT-01 | PASS — service và mode tách trên GET/overview/rating readiness |
| FT-02 | PASS — `SameMode(AIR, air)` không tạo stale mới. Không xóa cờ cũ |
| FT-03 | PASS — trace theo detail, không điền sẵn TDNT/QT0001 |
| FT-04 | PASS — FIXED không thành đơn giá tính tiền |
| FT-05 | PASS — surcharge/new và các form tạo lấy `catalogModeOptions` |
| FT-06 | PASS trên máy DEV trừ 2 test Postgres SKIP vì không có server. CI bật chạy thật |
| FT-07 | PASS — 37 TC sheet 27/10 cột DEV Test |
| FT-08 | No Change — không gộp/xóa master trùng. Ngoài phạm vi code đợt này |

## Log local 2026-11-02

`dotnet test Cms.sln -c Release` trước khi nới căn cứ exposure nhận **đối tác**:

`Failed: 1, Passed: 588, Skipped: 2, Total: 591`

FAIL: `BusinessPartyDirectoryFullTests.CreditBlock_RoleGate_SoftDeleteOpenAr_And_Export` vì exposure chỉ có `counterpartyId`. Đã tính đối tác là căn cứ hợp lệ (đúng case hạn mức tín dụng). Chạy lại test đó: **PASS**.

SKIP: `RateScopeMigrationPostgresTests` (không có Postgres local). Không skip vì `if: false`.

`npm test` trong `apps/web`: **47 PASS**.

`npx tsc --noEmit`: PASS sau sửa form exposure.

Ba test mới `ConsolidatedUat20261102Tests`: PASS (chiều tuyến, canonical route, AIR/air).

## Data remediation

Không có script sửa dữ liệu. Đề xuất nếu PO muốn xử lý cờ stale lịch sử `AIR → air` trên `TESTLSS002`: dry-run `SELECT` rating id, `stale_at`, `stale_reason`; không `UPDATE` cho đến khi PO duyệt; rollback là không chạy script. Snapshot published và số tiền không nằm trong phạm vi.
