# BÁO CÁO PHẢN HỒI DEV & HƯỚNG DẪN NGHIỆM THU CHO PO
## GÓI KHẮC PHỤC KHẨN CẤP: `RATE-VERSION-UI-01` (BA Correction v1.1 — 24/10/2026)

**Kính gửi:** Product Owner (PO) dự án LCMS  
**Người thực hiện:** Principal Fullstack Engineer & QA Automation Lead  
**Mã hạng mục:** `RATE-VERSION-UI-01` | **Mã đợt UAT:** `LCMS-UAT-CR-20261023-01`  
**Trạng thái bàn giao mới:** `READY FOR PO RE-UAT (DEV VERIFIED 100%)`  
**Tiêu chuẩn chất lượng:** COMMERCIAL-READY PRODUCT (100% Live Browser E2E Verified trên đúng LCMS App Layout)

---

### 1. TỔNG QUAN GIẢI QUYẾT 5 ĐIỂM GAP NGHIÊM TRỌNG

Đội ngũ Kỹ thuật đã điều tra trực tiếp trên mã nguồn, tìm đúng căn nguyên thực tế và hoàn thành dứt điểm toàn bộ 5 điểm GAP do PO ghi nhận tại đợt kiểm thử với bảng giá **`TDNT_SGN_HAN`**:

| Mã GAP | Phân loại | Hiện trạng UAT của PO | Giải pháp Kỹ thuật & Kết quả khắc phục |
| :--- | :---: | :--- | :--- |
| **`GAP-01`** | P0 | Ảnh PO #1: Màn hình chi tiết `TDNT_SGN_HAN` chỉ có nút *"Tính giá trên Bill"* và *"Ngừng sử dụng"*; hoàn toàn thiếu nút *"Tạo phiên bản mới"*. | **Đã sửa:** Tích hợp `CreateRateVersionButton` ("Tạo phiên bản mới") tại [`apps/web/app/rate-cards/[id]/page.tsx`](file:///c:/A1/git/cms/apps/web/app/rate-cards/[id]/page.tsx) đặt trang trọng cạnh *"Tính giá trên Bill"*. Chỉ hiển thị khi bảng giá đã phát hành và chưa có nháp. Khi có sẵn nháp, hiển thị nút *"Tiếp tục sửa bản nháp"*. |
| **`GAP-02`** | P0 | Ảnh PO #2: Nút CTA góc trên màn hình *"Quản lý phụ lục giá"* mang hai nghĩa mơ hồ (*"+ Tạo bảng giá / phiên bản mới"*); bảng không có nút tạo version trên từng dòng; xuất hiện dòng nháp `-v1` rỗng mã. | **Đã sửa:** Tách bạch CTA góc trên thành **"＋ Tạo bảng giá mới"** (dẫn tới `/rate-cards/new`). Bổ sung cột **Thao tác** trên từng dòng bảng giá với nút **"Tạo phiên bản mới"** (cho bản đã phát hành) hoặc **"Tiếp tục sửa bản nháp"** (cho bản nháp). Sửa query handler backend loại bỏ triệt để dòng rỗng `-v1`. |
| **`GAP-03`** | P0 | Ảnh PO #3: Nhấn nút tạo phiên bản bị điều hướng sang trang tạo bảng giá trắng (form Create Rate Card rỗng mã). | **Đã sửa:** Route và handler gắn chặt với `rateCardId` hiện tại (`TDNT_SGN_HAN`) và `CloneFromVersionId` (`v1`). Hệ thống gọi API `POST /bff/rate-cards/{id}/versions`, sinh bản nháp `v2` và mở form sửa với title ngữ cảnh: **"TDNT_SGN_HAN — Phiên bản v2 (Nháp)"**, callout **"Nhân bản từ v1"**, mã bảng giá read-only. |
| **`GAP-04`** | P0 | Bằng chứng DEV trước đây chỉ là test lệnh backend, không chứng minh được nút bấm và route trên UI chạy thông suốt. | **Đã sửa:** Bổ sung trọn bộ kiểm thử UI Component ([`CreateRateVersionButton.test.tsx`](file:///c:/A1/git/cms/apps/web/components/CreateRateVersionButton.test.tsx)), tích hợp API backend ([`RateVersionUi01FixTests.cs`](file:///c:/A1/git/cms/tests/LCMS.Api.Tests/RateVersionUi01FixTests.cs) 8/8 test PASS), và bộ kiểm thử End-to-End trên trình duyệt thực tế. |
| **`GAP-05`** | P1 | Ảnh DEV đợt trước chụp mã `RC-BUY-HCM-BD` trên màn hình cô lập thiếu App Navigation (header/sidebar) của LCMS. | **Đã sửa:** Toàn bộ bằng chứng được chụp TRỰC TIẾP từ ứng dụng Next.js (`apps/web` trên port 3000) kết nối backend .NET 8 thật trên đúng bảng giá **`TDNT_SGN_HAN`** với đầy đủ khung AppShell LCMS chuẩn thương mại (topbar trắng với search Ctrl+K, sidebar 14 module, breadcrumb, thông tin tenant và user). Tuyệt đối không dùng mockup HTML tĩnh. |

---

### 2. TỔNG KẾT KẾT QUẢ DEV & QUY TẮC MA TRẬN UAT

1. **10/10 Hạng mục sửa lỗi kỹ thuật (`FIX-01` → `FIX-10`)**: **`DEV status = DONE`**.
2. **14/14 Kịch bản kiểm thử giao diện (`UI-01` → `UI-14`)**: **`DEV Test = PASS`**.
3. **Bảo toàn quy tắc nghiệm thu PO**: Cột **`PO UAT = NOT RUN`** trong toàn bộ 14 dòng của Sheet `03_UAT_UI` (file Excel đính kèm) **được giữ nguyên 100%**, sẵn sàng để PO trực tiếp đánh giá và chuyển trạng thái nghiệm thu.
4. **Kiểm tra hồi quy (Zero Regression)**:
   - Backend Integration Suite: 8/8 tests mới trong [`RateVersionUi01FixTests.cs`](file:///c:/A1/git/cms/tests/LCMS.Api.Tests/RateVersionUi01FixTests.cs) PASS; toàn bộ 54/54 targeted tests (Pricing, Surcharge, VAT, Commercial Ready) PASS 100%.
   - Frontend Unit Suite: 7/7 test files (47/47 unit tests) trong `apps/web` PASS 100%.
   - Tổng cộng: **101/101 automated test cases PASS 100%**.
   - Next.js Production Build: Thành công không lỗi biên dịch.

---

### 3. DANH MỤC BẰNG CHỨNG LIVE BROWSER E2E SCREENSHOTS (RENDER BỞI CHROME CDP TRÊN ỨNG DỤNG NEXT.JS THỰC TẾ)

Toàn bộ ảnh chụp được render trực tiếp từ server Next.js (`apps/web` trên port 3000) kết nối ASP.NET Core API (`src/LCMS.Api` trên port 8080) và lưu trữ tại thư mục:  
[`docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots/`](file:///c:/A1/git/cms/docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots/)

1. **`01_rate_card_detail_published_v1_with_new_version_cta.png`** (190,056 bytes):
   - Màn hình chi tiết bảng giá `TDNT_SGN_HAN` với đầy đủ AppShell LCMS (topbar trắng, search `Ctrl K`, sidebar 14 module).
   - Nút **"Tạo phiên bản mới"** (nền xanh thương hiệu `#1677e8`, icon dấu cộng) đặt nổi bật bên cạnh nút *"Tính giá trên Bill"* và *"Ngừng sử dụng"*.
2. **`02_appendices_list_separated_cta_and_row_actions.png`** (228,422 bytes):
   - Màn hình *"Quản lý phụ lục giá"* với nút CTA góc trên tách biệt rõ ràng: **"＋ Tạo bảng giá mới"**.
   - 4 thẻ KPI chuẩn LCMS hiển thị đầy đủ (`Tổng phiên bản: 125`, `Đã Published: 124`, `Nháp / phụ lục: 1`, `Cách tạo phụ lục: Phiên bản mới`). Bảng phụ lục giá có cột **Thao tác** với nút **"Tạo phiên bản mới"** trên dòng `TDNT_SGN_HAN` (v1 Đã phát hành). Hoàn toàn không còn dòng rỗng `-v1`.
3. **`03_rate_card_v2_draft_edit_form_prefilled.png`** (Chiều cao viewport full-page: 1397px, 280,621 bytes):
   - Màn hình form khai báo bản nháp v2 tại route `/rate-cards/[id]/edit` với đầy đủ 3 khối: `1. Thông tin chung`, `2. Hình thức khai báo giá`, `3. Bảng giá cơ bản`.
   - Tiêu đề ngữ cảnh: **"TDNT_SGN_HAN — Phiên bản v2 (Nháp)"**. Callout: **"Nhân bản từ v1"**.
   - Ô Mã bảng giá bị khóa (`read-only`); kế thừa trọn vẹn hình thức BASIC, dropdown tuyến chọn đúng `SGN → HAN (SGN-HAN)`, cột Lọc hiển thị rõ `SGN-HAN · Trọng lượng tính cước · 1 – < 120 · FREIGHT · FREIGHT · Chi phí · 12.000 ₫`.
4. **`04_rate_card_v2_draft_edit_band_cw120_save_draft.png`** (Chiều cao viewport full-page: 1461px, 285,603 bytes):
   - Nấc trọng lượng được cập nhật mở rộng thành `[1, 200)` kg (bao trọn điểm CW = 120 kg theo quy tắc nửa mở).
   - Nhấn **"Lưu nháp"** hiển thị banner thông báo xanh: *"Đã lưu bản nháp v2 thành công!"* ngay trên trang chỉnh sửa mà không bị redirect mất ngữ cảnh.
5. **`05_rate_card_v2_publish_conflict_blocked.png`** (Chiều cao viewport full-page: 1574px, 312,317 bytes):
   - Kiểm tra an toàn nghiệp vụ: Khi tồn tại quy tắc xung đột nấc trọng lượng chồng lấn (`QT0001` [1, 200) và `QT0002` [50, 150) cùng xuất hiện trên bảng quy tắc), nút **"Kiểm tra & Phát hành v2"** chặn phát hành ngay lập tức.
   - Banner cảnh báo đỏ xuất hiện: *"RATE_CONFLICT: Hai quy tắc cùng khoản mục, cùng điều kiện và nấc trọng lượng chồng nhau. Không phát hành khi chưa có thứ tự ưu tiên."*. Bản `v1` và dữ liệu hệ thống được bảo vệ an toàn tuyệt đối.
6. **`06_rate_card_v2_published_and_version_history.png`** (Chiều cao viewport full-page: 1300px, 268,434 bytes):
   - Sau khi giải tỏa xung đột, bản nháp v2 được phát hành thành công.
   - Hiển thị trọn vẹn cả khối `v2 · Đã phát hành` với quy tắc `[1, 200)` nấc giá `12.000 ₫` và khối bảng `Phiên bản` (`#versions`) hiển thị `v2` (Đã phát hành, Đang áp dụng) cùng `v1` (Đã phát hành, Bất biến tuyệt đối). Có nút *"Xem bản này"* cho phép kiểm tra lại dữ liệu `v1` ở chế độ read-only.
7. **`07a_bill_rating_cw120_v1_no_match.png`** (Chiều cao viewport full-page: 1300px, 239,402 bytes):
   - Màn hình Tính giá (`/rate-cards/rate`) chọn Bill `DEMO-01-HUB`, phiên bản `v1` (`TDNT_SGN_HAN · v1`), trọng lượng tính cước `CW = 120 kg`.
   - Hệ thống chặn tính giá theo chính sách nửa mở `[1, 120)` với banner cảnh báo:  
     `Chưa đủ dữ liệu: Nấc trọng lượng — Trọng lượng (120 kg) không thuộc nấc nào (các nấc hiện hành: [1.0, 120.0)). Khoảng nửa mở [từ, đến) không bao gồm cận trên. Hãy tạo phiên bản mới có nấc phù hợp.`
8. **`07b_bill_rating_cw120_v2_matched.png`** (Chiều cao viewport full-page: 1300px, 250,889 bytes):
   - Màn hình Tính giá chọn Bill `DEMO-01-HUB`, chuyển sang phiên bản `v2` (`TDNT_SGN_HAN · v2`), trọng lượng tính cước `CW = 120 kg`.
   - Hệ thống khớp chính xác nấc `[1, 200)` của `v2`, tính ra tổng cước phí chuẩn xác **1.440.000 ₫** (120 kg $\times$ 12.000 ₫/kg) với:
     - Giá cơ bản: **1.440.000 ₫**
     - Tổng sau phụ phí: **1.440.000 ₫**
     - Cơ sở tính cước: **Nhập khi tính giá · 120 kg**
     - Tỷ giá: **1 (Cùng tiền tệ)**
9. **`07_bill_rating_cw120_before_and_after_v2.png`** (250,889 bytes):
   - Bản sao của `07b` để đối chiếu kết quả so sánh trước/sau khi phát hành v2.

---

### 4. HƯỚNG DẪN 6 BƯỚC DÀNH CHO PO THỰC HIỆN NGHIỆM THU TRỰC TIẾP

Để PO nghiệm thu nhanh chóng và chính xác trên môi trường UAT, vui lòng thực hiện luồng TO-BE 6 bước:

- **Bước 1 — Mở chi tiết bảng giá**:
  Truy cập menu *Bảng giá & Định mức* $\rightarrow$ *Bảng giá mua/bán* $\rightarrow$ Chọn bảng giá **`TDNT_SGN_HAN`** (hiện đang ở phiên bản `v1` Đã phát hành).
  *Quan sát*: Nút màu xanh **"Tạo phiên bản mới"** xuất hiện ngay cạnh nút *"Tính giá trên Bill"*.
- **Bước 2 — Nhấn "Tạo phiên bản mới"**:
  Nhấn nút **"Tạo phiên bản mới"**.
  *Quan sát*: Hệ thống tự động gọi API nhân bản và điều hướng sang trang `/rate-cards/{id}/edit`. Tiêu đề hiển thị rõ: **`TDNT_SGN_HAN — Phiên bản v2 (Nháp)`**, có dòng ghi chú **"Nhân bản từ v1"**, ô Mã bảng giá bị khóa mờ không cho sửa. Tuyệt đối không còn chuyển sang form tạo Rate Card trắng.
- **Bước 3 — Sửa nấc giá và Lưu nháp**:
  Tại bảng quy tắc cước, đổi cận trên nấc trọng lượng từ `120` thành `200` (để nấc cước trở thành `[1, 200) kg`, bao gồm CW = 120 kg). Nhấn nút **"Lưu nháp"**.
  *Quan sát*: Banner màu xanh thông báo *"Đã lưu bản nháp v2 thành công!"*; reload lại trang vẫn bảo toàn giá trị nấc vừa sửa.
- **Bước 4 — Kiểm tra & Phát hành v2**:
  Nhấn nút **"Kiểm tra & Phát hành v2"**.
  *Quan sát*: Hệ thống kiểm tra conflict và phát hành v2 thành công. Trang tự động điều hướng về màn hình chi tiết với badge trạng thái *"Đã phát hành (Published)"*.
- **Bước 5 — Kiểm tra Lịch sử phiên bản**:
  Cuộn xuống phần *Phiên bản* (`#versions`).
  *Quan sát*: Thấy rõ 2 dòng phiên bản: dòng `v2` (mới phát hành, đang áp dụng) và dòng `v1` (phiên bản gốc). Dữ liệu bản `v1` được bảo vệ bất biến.
- **Bước 6 — Kiểm thử Rating với CW = 120 kg**:
  Truy cập menu *Tính giá* (`/rate-cards/rate`), chọn Bill **`DEMO-01-HUB`**, chọn `TDNT_SGN_HAN · v1`, nhập trọng lượng tính cước `CW = 120 kg`.
  *Quan sát*:
  - Trước khi có v2 (tính theo v1): Bị chặn cảnh báo `Chưa đủ dữ liệu: Nấc trọng lượng — Trọng lượng (120 kg) không thuộc nấc nào (các nấc hiện hành: [1.0, 120.0)). Khoảng nửa mở [từ, đến) không bao gồm cận trên. Hãy tạo phiên bản mới có nấc phù hợp.`.
  - Chuyển sang phiên bản `v2` (`TDNT_SGN_HAN · v2`): Tính giá thành công ngay lập tức với đơn giá `12.000 VND/kg` $\times$ `120 kg` = `1.440.000 VND`, hiển thị đầy đủ bảng kết quả Rating và tỷ giá. Các snapshot rating cũ của v1 giữ nguyên không bị ghi đè.

---
*Tài liệu đính kèm:*
- Báo cáo truy vết nguyên nhân & giải pháp kỹ thuật: [`01_Root_Cause_Trace_And_Fix_Actions_Report.md`](file:///c:/A1/git/cms/docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/01_Root_Cause_Trace_And_Fix_Actions_Report.md)
- Ma trận nghiệm thu 14 kịch bản UI & Bằng chứng: [`02_UAT_UI_01_To_14_And_Traceability_Evidence.md`](file:///c:/A1/git/cms/docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/02_UAT_UI_01_To_14_And_Traceability_Evidence.md)
- File Ma trận Excel cập nhật: [`LCMS_RATE_VERSION_UI_01_DEV_UAT_Matrix_v1.1_20261024.xlsx`](file:///c:/A1/git/cms/docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/LCMS_RATE_VERSION_UI_01_DEV_UAT_Matrix_v1.1_20261024.xlsx)
