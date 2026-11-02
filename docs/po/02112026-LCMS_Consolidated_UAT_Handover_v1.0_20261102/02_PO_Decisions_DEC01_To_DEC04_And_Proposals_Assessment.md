# DEC-01 … DEC-04 và đánh giá PROP-01 / PROP-02

## DEC-01 — Revenue maturity 2 hoặc 3 lớp theo tenant

Kiến trúc dữ liệu giữ một dòng `revenues` với `ExpectedAmount`, `ConfirmedAmount`, `ActualAmount`. Không thêm bảng Confirmed giả.

Cấu hình nằm trong `tenant_settings.financial_json` (không migration):

- `revenueMaturityModel`: `three` (mặc định, kể cả khi trống) hoặc `two`
- `revenueMaturityPolicyVersion`
- `revenueMaturityEffectiveFrom`

Mặc định **không** chuyển sang 2 lớp. Đề xuất 2 lớp vẫn chờ PO chọn từng tenant trên màn Cài đặt tài chính.

Khi `two`: API `confirm` trả lỗi và không ghi `ConfirmedAmount`. API `actualize` cho phép `Expected → Actual` và vẫn cho bản ghi lịch sử đang `Confirmed` đi tiếp tới `Actual` mà không xóa số Confirmed đã lưu. Audit `revenue.confirm` / `revenue.actualize` ghi actor, maturity cũ/mới, nguồn.

## DEC-02 — Việt hóa Exposure / AP / AR

Từ điển `VietnameseUiTerms` và nhãn cứng trên menu, tab, dashboard, form:

| Mã | Nhãn |
|---|---|
| Exposure | Công nợ dự kiến |
| Receivable Exposure | Quyền thu dự kiến |
| Payable Exposure | Nghĩa vụ phải trả |
| Due Date | Ngày đến hạn thanh toán |
| Aging | Tuổi nợ |
| AP | Công nợ phải trả đã ghi nhận |
| AR | Công nợ phải thu đã ghi nhận |

Exposure không bị gọi là deadline công nợ. API domain `ReceivableExposure` / `AccountsReceivable` giữ nguyên.

## DEC-03 — Tách P&L và AR trên hồ sơ tài chính Bill

Màn Bill, khối tiền tệ báo cáo, tách hai bảng:

1. Doanh thu & chi phí — tham gia lợi nhuận
2. Công nợ phải thu / phải trả đã ghi nhận — không cộng vào lợi nhuận

Tiêu đề đổi thành **Chi tiết quy đổi tiền tệ**. Nguồn tỷ giá `identity` hiển thị **Không quy đổi (cùng tiền tệ)**. Giá trị lưu trong DB vẫn là `identity`.

Hồ sơ 3 nhóm lấy `finalizedAllocations` từ cùng query với hồ sơ tài chính (`CostAllocationDetails` của phiên `Finalized`). Bill UAT 500.000 doanh thu và 120.000 phân bổ đã chốt ra lợi nhuận 380.000 ở cả hai chỗ. Số gốc 240.000 không bị sửa.

## DEC-04 — Lịch sử phê duyệt

`GET /api/queues/approvals?status=` nhận `pending` (mặc định), `approved`, `rejected`, `all`.

UI `/queues/approvals` có tab Chờ duyệt / Đã duyệt / Từ chối / Tất cả, cột quyết định lúc và lý do, link đối tượng. Nếu `requestedBy` là user hiện tại, nút Phê duyệt disable kèm tooltip *Người tạo đề xuất không được tự phê duyệt (PC-21)*. Backend chặn tự duyệt giữ nguyên.

## PROP-01 — Phạm vi module công nợ theo tenant (không code)

Hiện mọi tenant dùng chung Cost, Revenue, Exposure, AP/AR, Payment. Tắt cả khối công nợ đụng:

- API `/api/receivable-exposures`, `/api/payable-exposures`, recognize, payment, collection
- Hồ sơ Bill (dòng AR/AP), báo cáo tuổi nợ, close eligibility
- Quyền `revenue.read` / `cost.read` đang tách khỏi quyền ghi nhận
- Hợp đồng mobile và BFF

Phương án khi PO chốt: một khóa trong `financial_json`, ví dụ `debtModule=off|on`, chỉ ẩn navigation và trả 403 trên API ghi công nợ. Đọc lịch sử AP/AR của tenant đã có số phải vẫn được phép. Không xóa dữ liệu. Chưa triển khai.

## PROP-02 — Bắt buộc loại hàng / loại doanh thu (không ép validation)

`commodity_types` theo tenant. Form Bill/sửa tham chiếu báo rõ khi danh mục trống và không chặn lưu.

Loại doanh thu trên form tạo doanh thu là dropdown `revenue_type` active, có lựa chọn **Chưa phân loại** (để trống mã). Đổi mã sau này là sửa phân loại, không tạo dòng doanh thu mới. Không thêm rule bắt buộc cho mọi tenant.
