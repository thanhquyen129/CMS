# LCMS – GÓI BA SỬA CƠ CHẾ ÁP DỤNG PHỤ PHÍ TOÀN HỆ THỐNG (P0)
Phiên bản: v1.0 | Ngày: 02/11/2026 | Mã: LCMS-CR-SURCHARGE-APPLICATION-01

## Nội dung
1. LCMS_BA_CR_SURCHARGE_APPLICATION_GLOBAL_P0_v1.0_20261102.docx
   - Tài liệu BA nguồn: phạm vi, 24 quy tắc, 18 tiêu chí nghiệm thu, 32 test UAT, migration và DoD.
2. LCMS_Surcharge_BR_AC_Traceability_v1.0_20261102.xlsx
   - 24 BR, 18 AC, bản đồ truy vết BR–AC, chính sách kích hoạt.
3. LCMS_Surcharge_UAT_Matrix_v1.0_20261102.xlsx
   - 32 test cases, dữ liệu thử đề xuất, mẫu nhật ký lỗi. DEV QA và PO UAT đều NOT RUN.
4. LCMS_Surcharge_Migration_Impact_v1.0_20261102.xlsx
   - Mẫu inventory cần DEV xuất từ DB, mapping dữ liệu logic, 18 bước cutover và rủi ro.

## Cách dùng
- DEV đọc Word trước và giữ đây là nguồn yêu cầu nghiệp vụ thống nhất.
- Excel BR/AC dùng cho thiết kế và liên kết PR/commit.
- Excel UAT dùng nhập kết quả thử nghiệm trên Staging, tuyệt đối không coi là PASS trước khi thực thi.
- Excel Migration/Impact bắt buộc DEV điền inventory thực tế và báo cáo ảnh hưởng trước khi cutover.
- KHÔNG thay published surcharge version, rating snapshot hoặc Cost/Revenue lịch sử.
- Mọi phân loại Mandatory/Auto/Optional phải có căn cứ/phê duyệt; không suy đoán từ dữ liệu trống.
- Chưa duyệt Production; cần PO UAT và phê duyệt phát hành riêng.

## Về trạng thái dữ liệu
- Một số ví dụ dùng DONGGOI và TESTLSS002 là dữ kiện UAT lịch sử hoặc trường hợp mô phỏng; không phải dữ liệu DEV đã xác nhận tại Staging mới.
- Cột trạng thái trong ma trận chưa được DEV/PO thực thi, giá trị NOT RUN/CHƯA LÀM đúng ở thời điểm lập hồ sơ.
