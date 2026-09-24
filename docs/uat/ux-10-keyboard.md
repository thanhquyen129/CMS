# UX-10 — đường bàn phím

Chưa chạy trên host. Vòng focus đã có trong CSS: nút và liên kết dùng viền trắng 3px kèm bóng `#0b3a75`; ô nhập dùng viền `#0b3a75` 2px. `#0b3a75` trên nền trắng đủ tương phản cho vòng focus.

Thứ tự khi có phiên:

1. `/login` — Tab: Email, Mật khẩu, Đăng nhập. Enter gửi form. Sai mật khẩu thì focus vào thông báo lỗi.
2. `/bills/new` — Tab tới «Lưu Bill» (`type="submit"`). Enter lưu.
3. Tạo chi phí trên Bill — `/bills/{id}/costs/new`, nút submit của form chi phí.
4. `/financial-closes` — form mở kỳ chốt, submit. Trên phiên đã đủ điều kiện, nút tạo bản chốt là `button` nhận Tab.

Không dùng chuột ở bốn bước trên. Nếu vòng focus mất trên một control, ghi route vào Evidence của UX-10 trong `docs/uat/UAT-PIXEL-PERFECT-WAVE2.md` và để trạng thái Not run.
