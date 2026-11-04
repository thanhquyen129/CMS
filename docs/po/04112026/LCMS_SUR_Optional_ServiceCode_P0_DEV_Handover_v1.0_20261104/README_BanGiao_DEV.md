# LCMS | GÓI BỔ SUNG P0 – PHỤ PHÍ TÙY CHỌN (04/11/2026)

Mã issue: LCMS-UAT-SUR-OPTIONAL-SERVICE-CODE-01
Gói BA cha: LCMS-CR-SURCHARGE-APPLICATION-01 (02/11/2026)
Trạng thái: PO đồng ý chuyển DEV xử lý, chưa có DEV Test/PO UAT PASS.

## Hướng dẫn đọc
1. Đọc `01_PhuLuc_20261104/LCMS_BA_Delta_SUR_Optional_ServiceCode_P0_v1.0_20261104.docx` và workbook cùng thư mục. Đây là DELTA cần xử lý ưu tiên.
2. `00_TaiLieuGoc_20261102/` là bộ BA và ma trận gốc để tra cứu/hồi quy, nội dung không bị sửa.
3. `02_BangChung_UAT/` chứa ảnh khi phát hành DONGGOI_SGN_HAN_MUA v3 OPTIONAL_OPT_IN báo thiếu service_code.

## Nội dung fix
- Thêm trường lookup Dịch vụ kích hoạt `service_code` khi activation_policy=OPTIONAL_OPT_IN; map danh mục chuẩn (không phát sinh mã giả).
- Quản lý scope, quantity, selection revision ở Bill/Shipment/Chặng; đồng bộ với khu dịch vụ tùy chọn trong Tính giá Bill.
- Form hiển thị lỗi ngay trường; API chặn publish thiếu/không hợp lệ service_code.
- Giữ bất biến Published v1/v2 và Rating lịch sử; BUY/SELL độc lập; Rating thử không tự tạo tài chính; Apply chống trùng.
- Cung cấp bằng chứng trực tiếp từ ứng dụng và hoàn thành 18 ca UAT; PO nghiệm thu riêng.

Lưu ý: Không tự chuyển tất cả phụ phí legacy thành OPTIONAL; cần phân loại được quản trị chấp thuận.
