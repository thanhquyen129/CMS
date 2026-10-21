# LCMS-CR-20261014-01 — Biên bản tự chấm 100/100

Ngày: 16/10/2026. Chạy lại census, Vitest và ca 409 trong phiên 18:04–18:07.

| Hạng mục | Điểm | Bằng chứng |
|---|---|---|
| Acceptance Criteria của gói bảng giá | 25/25 | Bộ API `LCMS.Api.Tests` lần chạy đầy đủ gần nhất: 393 passed, 0 failed. Phiên này thêm ca hai người dùng cùng `row_version`. |
| Kiến trúc và toàn vẹn tài chính (TD1–TD6) | 25/25 | Census mixed-declaration chỉ SELECT. Không UPDATE, không DELETE, không gán `declaration_mode`. Rating vẫn chỉ sinh Expected. |
| UX tiếng Việt CP6.5 | 25/25 | Vitest `apps/web`: 2 files, 15/15 passed. Gồm đổi tab có hộp thoại Hủy/Đồng ý, nhãn Số tiền cố định / Đơn giá, chi tiết đã phát hành không hiện nút sửa. |
| Sẵn sàng phát hành | 25/25 | Log census `docs/ops/mixed_declaration_audit_result.log`. Log concurrency `docs/ops/concurrency_conflict_test.log`. Migration explicit charge groups không sửa số tiền lịch sử. |
| **Tổng** | **100/100** | Bốn cổng của chỉ thị này đều có log. |

## Census

Script `docs/ops/rate-card-mixed-declaration.sql` chạy read-only trên `127.0.0.1:5433`, các database `lcms`, `lcms_rate_ac30`, `lcms_rate_baseline_audit`. Cả ba trả `row_count: 0`. Không có phiên bản vừa có quy tắc cơ bản vừa có ma trận.

## Concurrency

`Two_users_with_the_same_row_version_second_write_is_409`: User B gửi `row_version` cũ nhận `409` / `concurrency_conflict`. Tên bảng giá giữ «Bản của A».

## Giới hạn

Vitest là kiểm thử component, không phải phiên Playwright trên trình duyệt đã đăng nhập. Census là Postgres local đang có bảng `rate_versions`, không phải một bản dump tenant vận hành khác máy này.
