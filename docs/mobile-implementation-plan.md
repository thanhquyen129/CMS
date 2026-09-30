# Kế hoạch Triển khai Mobile App cho Hệ thống LCMS (Cost Management System)

## 1. Kiến trúc & Lựa chọn Công nghệ
- **Mobile Framework:** React Native (Expo SDK 52+, New Architecture, Expo Router) đặt tại `apps/mobile`.
- **Shared Core:** `packages/shared` chia sẻ TypeScript DTOs, `TerminologyMap`, bộ kiểm soát tiền (`If-Match`, `Idempotency-Key`, `Before -> After`) và bộ phân giải điều hướng theo vai trò (`Role-Adaptive Navigation`) giữa Web và Mobile.
- **Kết nối Backend:** Kết nối trực tiếp tới `LCMS.Api` (.NET 8) qua HTTPS + `Authorization: Bearer <JWT>`, xoay vòng `RefreshToken` lưu trong `expo-secure-store`.
- **Lưu trữ file chứng từ (`attachments`):** Lưu trực tiếp trên ổ đĩa VPS Contabo (`/opt/cms/attachments`, cấu hình qua `Attachments:StoragePath`), phân tách thư mục theo `tenant_id/yyyy/MM/`, kiểm tra mã băm `SHA-256`, cô lập tuyệt đối giữa các thuê bao và áp dụng quy tắc tách biệt nhiệm vụ `Cost ≠ Revenue` (SoD).

## 2. Giao diện Thích ứng theo 7 Vai trò (Role-Adaptive Workspace)
Ứng dụng tự động cấu hình 4 Tab chính dưới đáy + Tab thứ 5 (`Phân hệ` — truy cập toàn bộ 14 module được cấp phép) dựa trên `GET /api/mobile/bootstrap`:
1. **Quản trị (`Admin`) & Kiểm soát tài chính (`FinancialController`):** Tổng quan · Phê duyệt · Kiểm soát · Bill & Công nợ · Phân hệ.
2. **Kế toán Chi phí (`CostAccountant`):** Tổng quan CP · Chi phí · Công nợ AP · Chứng từ & Chi · Phân hệ (Ẩn tuyệt đối Doanh thu, AR, Lợi nhuận).
3. **Kế toán Doanh thu (`RevenueAccountant`):** Tổng quan DT · Doanh thu · Công nợ AR · Chứng từ & Thu · Phân hệ (Ẩn tuyệt đối Chi phí, AP, Lợi nhuận).
4. **Điều vận (`Ops`):** Vận hành · Quét & Chụp · Chi phí dự kiến · Đồng bộ Offline · Phân hệ.
5. **Quản trị danh mục (`MasterData`) & Chỉ xem (`Viewer`):** Tra cứu Bill · Đối tác · Tuyến & Địa điểm · Tiền tệ & Danh mục · Phân hệ.

## 3. Bốn Tính năng Native Trọng tâm
1. **Push Notifications (`expo-notifications`):** Đăng ký thiết bị qua `POST /api/notifications/devices`, đẩy thông báo qua `OutboxMessage` (`notification.push`) khi có yêu cầu phê duyệt, ngoại lệ kiểm soát hoặc cảnh báo tỷ giá/tính giá lại.
2. **Camera Chụp Chứng từ & Quét Barcode/QR (`expo-camera`):** Quét nhanh mã `HAWB`/`MAWB`/Bill để mở hồ sơ hoặc gắn chứng từ; chụp và nén ảnh chứng từ tại hiện trường tải thẳng lên `/api/attachments`.
3. **Bảo mật Sinh trắc học (`expo-local-authentication`):** Mở khóa nhanh bằng FaceID/Vân tay và bắt buộc xác thực sinh trắc học (Step-up Auth) trước khi thực hiện lệnh tiền (Duyệt, Xác nhận/Thực tế hóa, Phân bổ, Xóa nợ, Hoàn tác xóa nợ, Chốt kỳ).
4. **Hàng đợi Offline (`expo-sqlite` Outbox):** Lưu nháp cập nhật vận hành, chi phí dự kiến và ảnh chứng từ khi mất sóng tại kho/cảng; tự động sinh `Idempotency-Key` tại thời điểm thao tác và gửi kèm `If-Match` (`rowVersion`) khi có mạng trở lại.

