# LCMS Master Data Standardization — Fast-Track Handover

Ngày: 2026-10-28. Nhánh: `main`.

## Commit

SHA đầy đủ được ghi ở commit cuối của gói này (xem `git log -1 --format=%H` trên `main` sau khi đẩy). File này được cập nhật trong commit bàn giao.

## Phạm vi đã ship

- Phương thức vận chuyển ghi mới dùng mã `UPPERCASE` (`AIR`, `SEA`, `ROAD`, `RAIL`, `MULTIMODAL`).
- Đọc và so khớp dữ liệu cũ (`air`, `AIR`, khoảng trắng) trong bộ nhớ. Không có migration lịch sử.
- Rating stale chỉ khi ngữ cảnh nghiệp vụ đổi. `AIR` và `air` không tạo stale. Đổi `AIR` → `SEA` hoặc đổi trọng lượng vẫn stale.
- Snapshot `ratings.context_json`, số tiền, cờ `stale_at` cũ, bảng giá đã phát hành (khi chỉ khác hoa/thường) không bị ghi đè.

## Kiểm thử

| Suite | Kết quả |
|---|---|
| `dotnet test` (trừ `RateScopeMigrationPostgresTests`) | 579/579 PASS |
| `vitest run` (`apps/web`) | 47/47 PASS |
| `dotnet build` API tests | 0 error |
| `RateScopeMigrationPostgresTests` | Không chạy: không kết nối được `127.0.0.1:5433` |

Môi trường kiểm thử: SQLite tạm của `LcmsApiFactory`. Không đụng `infra/.env`. Không deploy tay. CI trên `main` là đường deploy.

## Năm giới hạn PO

| Giới hạn | Kết quả |
|---|---|
| Migration / bulk update lịch sử | 0 lần chạy |
| Sửa Rating Snapshot, Rate Card/Version đã phát hành, Cost, Revenue, AP/AR | 0 |
| Xóa `stale_at` / `stale_reason` cũ | 0 |
| Bật/tắt hàng loạt `is_active` | 0 |
| Đổi quyết định WATER / EXPRESS / MULTIMODAL hoặc trộn Service Type với Transport Mode | 0 |

WATER không bị gộp thành SEA. EXPRESS không bị gộp thành AIR. MULTIMODAL chỉ là phân loại Shipment. Service Type `AIR` vẫn bị chặn cho giao dịch mới; `FREIGHT` vẫn là loại dịch vụ.
