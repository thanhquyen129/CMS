# BÁO CÁO ĐIỀU TRA CĂN NGUYÊN THỰC TẾ & 10 HẠNG MỤC SỬA LỖI
## MÃ HẠNG MỤC: `RATE-VERSION-UI-01` (BA Correction v1.1 — 24/10/2026)

---

### PHẦN 1: KẾT QUẢ THỰC HIỆN 5 BƯỚC ĐIỀU TRA TÌM CĂN NGUYÊN (MỤC 5 BA SPEC)

Tuân thủ nghiêm ngặt chỉ thị của PO về việc truy tìm đúng nguyên nhân thực tế trên mã nguồn, không suy đoán lý thuyết, đội ngũ Kỹ thuật đã rà soát toàn bộ code Frontend và Backend:

#### Bước 1: Điều tra trang chi tiết `/rate-cards/[id]` (Gốc rễ `GAP-01`)
- **Tập tin kiểm tra:** [`apps/web/app/rate-cards/[id]/page.tsx`](file:///c:/A1/git/cms/apps/web/app/rate-cards/[id]/page.tsx) và [`apps/web/components/CreateRateVersionForm.tsx`](file:///c:/A1/git/cms/apps/web/components/CreateRateVersionForm.tsx).
- **Căn nguyên thực tế phát hiện:**
  - Trong codebase trước đó, component `CreateRateVersionForm.tsx` đã được viết nhưng **chưa từng được import hay mount** vào trong `apps/web/app/rate-cards/[id]/page.tsx`.
  - Trên giao diện chi tiết, thanh công cụ chỉ kiểm tra điều kiện `draftVersion ? (Chỉnh sửa bản nháp) : null`. Đối với một bảng giá đã phát hành (`Published`) và chưa có bản nháp nào như `TDNT_SGN_HAN`, hệ thống **hoàn toàn không render bất kỳ nút bấm nào** để người dùng khởi tạo phiên bản mới.
  - Do đó, PO nhìn thấy trên màn hình chỉ có 2 nút *"Tính giá trên Bill"* và *"Ngừng sử dụng"*.

#### Bước 2: Trace nút trên màn hình *"Quản lý phụ lục giá"* (Gốc rễ `GAP-02`, `GAP-03` & Dòng nháp `-v1`)
- **Tập tin kiểm tra:** [`apps/web/app/rate-cards/appendices/page.tsx`](file:///c:/A1/git/cms/apps/web/app/rate-cards/appendices/page.tsx) và [`src/LCMS.Application/RateCards/Queries/RateCatalogQueries.cs`](file:///c:/A1/git/cms/src/LCMS.Application/RateCards/Queries/RateCatalogQueries.cs).
- **Căn nguyên thực tế phát hiện:**
  - Nút CTA góc trên màn hình mang nhãn gây hiểu lầm: `＋ Tạo bảng giá / phiên bản mới` và được gắn cứng thẻ link: `<Link href="/rate-cards/new">`. Khi PO bấm vào đây, hệ thống điều hướng trực tiếp sang route `/rate-cards/new` (màn hình tạo mới Rate Card trắng) thay vì nhân bản phiên bản cho Rate Card đang chọn.
  - Bảng danh sách phụ lục giá hoàn toàn thiếu cột hành động (*Thao tác*); các dòng bảng giá đã phát hành không có nút tạo version mới.
  - Về hiện tượng xuất hiện dòng nháp `-v1` trống mã: Query handler backend `ListRateAppendicesQueryHandler` trước đây truy vấn toàn bộ các bản ghi `RateVersions` rồi dùng Dictionary để lookup `RateCards`. Khi một bảng giá bị xóa mềm (`DeletedAt != null`), bản nháp của nó vẫn còn lưu trong cơ sở dữ liệu và phép lookup trả về `null`, dẫn đến việc format `${card?.Code ?? ""}-v1` hiển thị thành dòng mồ côi `-v1`.

#### Bước 3: Kiểm tra API tạo phiên bản trên code và endpoint thực tế
- **Tập tin kiểm tra:**
  - Backend Handler: [`src/LCMS.Application/RateVersions/Commands/RateVersionCommands.cs`](file:///c:/A1/git/cms/src/LCMS.Application/RateVersions/Commands/RateVersionCommands.cs) (`CreateRateVersionCommandHandler`).
  - BFF Route: [`apps/web/app/bff/rate-cards/[id]/versions/route.ts`](file:///c:/A1/git/cms/apps/web/app/bff/rate-cards/[id]/versions/route.ts).
- **Căn nguyên thực tế phát hiện:**
  - Logic clone quy tắc định giá (`PricingRules`, `PricingRuleComponents`, `RateBreaks`, `MatrixConditions`) ở tầng backend hoạt động đúng về mặt thuật toán, nhưng `CreateRateVersionCommandHandler` **chưa thực hiện kiểm tra phân quyền** (`RateBuyWrite` / `RateSellWrite`) trước khi tạo bản nháp mới.
  - Chưa có cơ chế bảo vệ double-click (Double-click Guard) ở cả Backend và Frontend: nếu người dùng bấm 2 lần liên tiếp, hệ thống sẽ cố tạo ra nhiều bản nháp trùng lặp.

#### Bước 4: Kiểm tra tính đồng bộ môi trường Build/Commit
- Đã xác nhận toàn bộ mã Frontend Next.js và Backend .NET 8 đồng bộ 100% trên cùng workspace git.
- Loại bỏ hoàn toàn việc chụp ảnh component đơn lẻ (standalone component) trong môi trường mock; thiết lập quy trình kiểm thử End-to-End trên đúng bản build sản phẩm với khung AppShell hoàn chỉnh.

#### Bước 5: Thiết lập E2E Browser Test Suite
- Xây dựng bộ kiểm thử tự động hóa trên trình duyệt thực tế (sử dụng Chrome headless CDP và Web standard) đi qua toàn bộ 6 bước nghiệp vụ trên bảng giá `TDNT_SGN_HAN`: Mở chi tiết $\rightarrow$ Bấm tạo phiên bản mới $\rightarrow$ Chỉnh sửa nấc trọng lượng $\rightarrow$ Lưu nháp $\rightarrow$ Phát hành v2 $\rightarrow$ Xác minh lịch sử phiên bản $\rightarrow$ Chạy rating CW = 120 kg.

---

### PHẦN 2: CHI TIẾT 10 HẠNG MỤC SỬA LỖI ĐÃ HOÀN TẤT (`FIX-01` → `FIX-10`)

| Mã FIX | Hạng mục công việc | Chi tiết triển khai kỹ thuật & Vị trí code | Trạng thái |
| :---: | :--- | :--- | :---: |
| **`FIX-01`** | Thêm nút *"Tạo phiên bản mới"* trên trang chi tiết | Tạo client component [`apps/web/components/CreateRateVersionButton.tsx`](file:///c:/A1/git/cms/apps/web/components/CreateRateVersionButton.tsx) và nhúng vào [`apps/web/app/rate-cards/[id]/page.tsx`](file:///c:/A1/git/cms/apps/web/app/rate-cards/[id]/page.tsx). Hiển thị nổi bật cạnh nút *"Tính giá trên Bill"* khi có bản `Published` và chưa có bản nháp. | **`DONE`** |
| **`FIX-02`** | Tách bạch CTA Quản lý phụ lục giá | Sửa [`apps/web/app/rate-cards/appendices/page.tsx`](file:///c:/A1/git/cms/apps/web/app/rate-cards/appendices/page.tsx): đổi nút trên cùng thành **"＋ Tạo bảng giá mới"**; bổ sung cột **Thao tác** trên bảng với nút **"Tạo phiên bản mới"** cho dòng đã phát hành và **"Tiếp tục sửa bản nháp"** cho dòng nháp. | **`DONE`** |
| **`FIX-03`** | Truyền chuẩn `RateCardId` & `SourceVersionId` | [`CreateRateVersionButton.tsx`](file:///c:/A1/git/cms/apps/web/components/CreateRateVersionButton.tsx) gọi `POST /bff/rate-cards/{rateCardId}/versions` kèm `CloneFromVersionId`. Giữ nguyên `rate_card_id` và mã `TDNT_SGN_HAN`, chỉ sinh `rate_version_id` mới (`v2`), sau đó redirect tới `/rate-cards/{rateCardId}/edit`. | **`DONE`** |
| **`FIX-04`** | Nhân bản an toàn quy tắc BASIC/ADVANCED | [`RateVersionCommands.cs`](file:///c:/A1/git/cms/src/LCMS.Application/RateVersions/Commands/RateVersionCommands.cs) sao chép toàn bộ thông tin nấc trọng lượng, đơn giá, tuyến, carrier, method, matrix và VAT 8%. Được bảo chứng bởi test `RateVersionUi01FixTests.UI04_Clone_PreservesAllBasicFieldsAndRules`. | **`DONE`** |
| **`FIX-05`** | Mở form sửa v2 prefill đầy đủ ngữ cảnh | Cập nhật [`RateCardDeclarationForm.tsx`](file:///c:/A1/git/cms/apps/web/components/RateCardDeclarationForm.tsx) và [`app/rate-cards/[id]/edit/page.tsx`](file:///c:/A1/git/cms/apps/web/app/rate-cards/[id]/edit/page.tsx): Tiêu đề hiển thị: **`{code} — Phiên bản v{versionNo} (Nháp)`**, dòng callout: **`Nhân bản từ v1`**, khóa ô mã bảng giá (`read-only`), prefill toàn bộ nấc giá. | **`DONE`** |
| **`FIX-06`** | Vòng đời Lưu nháp / Kiểm tra / Phát hành | Hỗ trợ lưu nháp an toàn, nút **"Kiểm tra & Phát hành v{versionNo}"** kích hoạt kiểm tra conflict thông qua `RateRuleConflicts.cs`, chặn phát hành nếu nấc trùng, phát hành thành công và điều hướng về trang chi tiết. Bảo vệ bản `v1` bất biến tuyệt đối. | **`DONE`** |
| **`FIX-07`** | Bảng lịch sử phiên bản (`#versions`) | [`apps/web/app/rate-cards/[id]/page.tsx`](file:///c:/A1/git/cms/apps/web/app/rate-cards/[id]/page.tsx) hiển thị chi tiết danh sách tất cả các phiên bản của bảng giá: thấy rõ `v1` và `v2`, trạng thái, ngày hiệu lực và liên kết xem lại bản cũ. | **`DONE`** |
| **`FIX-08`** | Guard chống bấm lặp & Resume bản nháp | - Frontend: Trạng thái `busy` disable nút ngay khi bấm, chống double-click.<br>- Backend: [`RateVersionCommands.cs`](file:///c:/A1/git/cms/src/LCMS.Application/RateVersions/Commands/RateVersionCommands.cs) kiểm tra nếu bảng giá đã có sẵn bản nháp `DRAFT`, hệ thống trả về ngay ID của bản nháp đó thay vì tạo bản ghi mới (Idempotent). | **`DONE`** |
| **`FIX-09`** | Chụp 5 ảnh Live Browser E2E AppShell | Xuất đủ 5 ảnh chụp chuẩn độ phân giải 1366x768 có đầy đủ navigation sidebar và topbar của LCMS trên bảng giá `TDNT_SGN_HAN` vào thư mục `docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots/`. | **`DONE`** |
| **`FIX-10`** | Kiểm thử Rating CW = 120 kg bất biến | Bộ test [`RateVersionUi01FixTests.cs`](file:///c:/A1/git/cms/tests/LCMS.Api.Tests/RateVersionUi01FixTests.cs) (`UI10_To_UI12`) kiểm chứng: Rating với CW = 120 kg dưới v1 thất bại (do khoảng nửa mở `[1, 120)` loại trừ 120), mở rộng nấc trên v2 thành `[1, 200)` tính giá thành công; snapshot tài chính cũ của v1 giữ nguyên bất biến. | **`DONE`** |

---

### PHẦN 3: ĐỐI SOÁT KIẾN TRÚC & NGUYÊN TẮC AN TOÀN PHIÊN BẢN

1. **Giữ nguyên Identity của Rate Card**:
   - `rate_card_id` và mã nghiệp vụ `TDNT_SGN_HAN` không thay đổi.
   - Chỉ tạo bản ghi mới trong bảng `rate_versions` với `version_no = 2`, trạng thái ban đầu là `draft`.
   - Tuyệt đối không gọi lệnh `CreateRateCardCommand` hoặc tạo bảng giá mới.
2. **Tính bất biến (Immutability)**:
   - Bản ghi `v1` (`status = 'published'`) hoàn toàn không bị cập nhật in-place.
   - Các bản ghi `Rating` và `RatingDetail` đã lưu của `v1` giữ nguyên 100% giá trị và dấu vết kiểm toán (`snapshot`), không bị ghi đè khi `v2` ra đời.
3. **Kiểm soát phân quyền & Tenancy**:
   - Mọi thao tác đều được lọc theo `TenantId` của phiên làm việc.
   - Server-side kiểm tra quyền chặt chẽ thông qua `IPermissionService`: chỉ user có quyền `rate_buy:write` (hoặc `rate_sell:write` đối với bảng giá bán) mới được phép tạo phiên bản nháp. User không có quyền sẽ nhận lỗi `403 Forbidden` kèm thông báo tiếng Việt minh bạch chuẩn `CP6.5`.
4. **Loại bỏ triệt để dòng rỗng `-v1`**:
   - `ListRateAppendicesQueryHandler` đã được sửa đổi với INNER JOIN giữa `RateVersions` và `RateCards` đang hoạt động, đảm bảo nếu Rate Card bị xóa mềm thì bản nháp mồ côi sẽ không bao giờ xuất hiện trên giao diện người dùng.

