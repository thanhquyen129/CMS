# TÀI LIỆU KỸ THUẬT: MASTER DATA, API CONTRACTS, UI & CODE DIFF
**Mã Issue Delta:** `LCMS-UAT-SUR-OPTIONAL-SERVICE-CODE-01` | **Gói BA cha:** `LCMS-CR-SURCHARGE-APPLICATION-01`  
**Phiên bản:** `v1.0 (04/11/2026)` | **Phân loại:** `Technical Specification & Implementation Review`

---

## 1. KIẾN TRÚC DANH MỤC DỊCH VỤ KÍCH HOẠT (MASTER DATA)

### 1.1. Nguyên tắc cốt lõi (`BR-DELTA-01`, `BR-DELTA-03`)
Hệ thống LCMS nghiêm cấm tuyệt đối việc suy diễn hoặc đánh đồng `service_code` từ:
- Mã khoản mục chi phí kinh tế (`economic_charge_types`, ví dụ `DONGGO`).
- Mã hoặc tên biểu phụ phí (`surcharges.code`, ví dụ `DONGGOI`, `DONGGOI_SGN_HAN_MUA`).
- Loại hình dịch vụ vận tải chính (`service_type_code = FREIGHT`).

Thay vào đó, `service_code` được định nghĩa là một thực thể **Dịch vụ bổ sung / Dịch vụ kích hoạt (`Activation / Value-Added Service`)** độc lập, quản trị theo từng Tenant trong phân hệ `MasterCatalogItem` với `kind = 'optional_service'`.

### 1.2. Cấu trúc thực thể `MasterCatalogItem` cho `optional_service`
```csharp
// src/LCMS.Domain/Entities/MasterCatalogItem.cs
public static class MasterCatalogKinds
{
    // ...
    public const string OptionalService = "optional_service";
    // ...
}
```

Các thuộc tính mở rộng được lưu trữ trong `attributes_json`:
```json
{
  "allowedContextTypes": ["BILL", "SHIPMENT", "LEG"],
  "defaultUom": "PACKAGE"
}
```

### 1.3. Bộ danh mục nền tảng (Baseline Catalog)
Hệ thống tự động seed bộ danh mục chuẩn cho mọi Tenant mới và hiện có thông qua `OptionalServiceCatalogSeeder`:
1. `DONGGOI` — **Đóng gói hàng hóa**: Dịch vụ đóng gói và bao bọc hàng hóa bổ sung (`is_active = true`, ngữ cảnh: `BILL`, `SHIPMENT`, `LEG`, ĐVT: `PACKAGE`).
2. `KIEMDEM` — **Kiểm đếm tại kho**: Dịch vụ kiểm đếm số lượng, quy cách hàng hóa tại kho (`is_active = true`, ngữ cảnh: `BILL`, `SHIPMENT`, `LEG`, ĐVT: `BILL`).
3. `NANGHA` — **Nâng hạ bốc xếp**: Dịch vụ nâng hạ, bốc xếp kiện hàng nặng hoặc đặc biệt (`is_active = true`, ngữ cảnh: `BILL`, `SHIPMENT`, `LEG`, ĐVT: `SHIPMENT`).
4. `BAOHIEM` — **Bảo hiểm hàng hóa giá trị cao**: Dịch vụ bảo hiểm bổ sung theo giá trị khai báo (`is_active = false`, trạng thái vô hiệu để kiểm thử chặn kích hoạt).

---

## 2. THIẾT KẾ HỢP ĐỒNG API & ERROR CONTRACT

### 2.1. API Lấy danh mục dịch vụ kích hoạt
- **Endpoint:** `GET /api/surcharges/activation-services`
- **Query params:** `activeOnly=true|false` (mặc định: `true`)
- **Headers:** `X-Tenant-Id: <tenant_guid>`
- **Response HTTP 200 OK:**
```json
[
  {
    "serviceCode": "DONGGOI",
    "displayName": "Đóng gói hàng hóa",
    "description": "Dịch vụ đóng gói và bao bọc hàng hóa bổ sung",
    "isActive": true,
    "allowedContextTypes": ["BILL", "SHIPMENT", "LEG"],
    "defaultUom": "PACKAGE"
  },
  {
    "serviceCode": "KIEMDEM",
    "displayName": "Kiểm đếm tại kho",
    "description": "Dịch vụ kiểm đếm số lượng, quy cách hàng hóa tại kho",
    "isActive": true,
    "allowedContextTypes": ["BILL", "SHIPMENT", "LEG"],
    "defaultUom": "BILL"
  }
]
```

### 2.2. Phân định Lưu nháp (`Save Draft`) vs Phát hành (`Publish`) (`BR-DELTA-04`)
1. **Lưu nháp (`DRAFT`):**
   - Cho phép lưu bản nháp `v3` ngay cả khi chưa chọn `service_code`.
   - API trả HTTP 200 OK kèm danh sách cảnh báo (`warnings`):
   ```json
   {
     "versionId": "b21ee2d5-8517-401b-8000-1c27e3a332bc",
     "versionNo": 3,
     "status": "draft",
     "warnings": [
       "Phụ phí chọn chính sách OPTIONAL_OPT_IN nhưng chưa khai báo service_code liên kết. Bạn cần chọn Dịch vụ kích hoạt trước khi phát hành phiên bản này."
     ]
   }
   ```
2. **Phát hành (`PUBLISH`):**
   - Chặn tuyệt đối nếu `service_code` rỗng, không tồn tại trong danh mục, đang `inactive`, thuộc Tenant khác hoặc `selection_scope` không nằm trong `allowedContextTypes`.
   - Bắt buộc trả lỗi có cấu trúc chuẩn HTTP 400 Bad Request, gắn trực tiếp tên trường `field: "service_code"`. Tuyệt đối không trả HTTP 500:
   ```json
   {
     "correlationId": "80801872843a47839c5c776d27ee930c",
     "code": "REQUIRED_SERVICE_CODE",
     "errorCode": "REQUIRED_SERVICE_CODE",
     "error_code": "REQUIRED_SERVICE_CODE",
     "message": "Phụ phí tùy chọn cần mã dịch vụ liên kết (service_code).",
     "field": "service_code",
     "errors": {
       "service_code": [
         "Phụ phí tùy chọn cần mã dịch vụ liên kết (service_code)."
       ]
     }
   }
   ```

Các mã lỗi nghiệp vụ chuẩn hóa:
- `REQUIRED_SERVICE_CODE`: Khi `service_code` bị bỏ trống hoặc null.
- `UNKNOWN_SERVICE`: Khi `service_code` không tồn tại trong danh mục `optional_service` của Tenant.
- `INACTIVE_SERVICE`: Khi `service_code` đã bị vô hiệu hóa (`is_active = false`).
- `INVALID_SCOPE`: Khi phạm vi áp dụng (`selection_scope`, ví dụ `BILL`) không nằm trong `allowedContextTypes` của dịch vụ.

---

## 3. GIAO DIỆN NGƯỜI DÙNG (FRONTEND IMPLEMENTATION)

### 3.1. Form Quản lý phụ phí (`SurchargeForm.tsx`)
- Khi người dùng chọn Chính sách kích hoạt là **“Tùy chọn có xác nhận (OPTIONAL_OPT_IN)”**:
  - Giao diện hiển thị trường combobox lookup **“Dịch vụ kích hoạt *”**.
  - Dropdown hiển thị đầy đủ `serviceCode — displayName (allowedContextTypes | defaultUom)`.
  - Nếu bản nháp chưa có `service_code`, hiển thị banner inline màu vàng:
    *“⚠️ Cần chọn Dịch vụ kích hoạt trước khi phát hành phiên bản này. Bạn vẫn có thể Lưu nháp.”*
  - Khi bấm **Phát hành bản này**, nếu thiếu `service_code` hoặc API trả lỗi:
    - Ô dropdown lập tức chuyển viền đỏ (`border-color: #ef4444`).
    - Thông báo lỗi hiển thị ngay bên dưới trường: *“Phụ phí tùy chọn bắt buộc phải chọn dịch vụ kích hoạt trước khi phát hành.”* (không chỉ hiện banner chung ở đầu trang).
- Khi chọn chính sách `MANDATORY_BY_POLICY`, `AUTO_CONDITIONAL` hoặc `UNCLASSIFIED_LEGACY`:
  - Trường `service_code` tự động ẩn để tránh nhầm lẫn.

### 3.2. Màn hình Chi tiết phiên bản (`versions/[versionId]/page.tsx`)
Bảng thông tin phiên bản hiển thị thêm:
- **Dịch vụ kích hoạt:** `{version.serviceCode} — {version.serviceDisplayName}`
- **Phạm vi lựa chọn:** `{version.selectionScope || "BILL"}`

### 3.3. Đồng nhất nguồn lựa chọn dịch vụ (`ServiceSelectionPanel.tsx` & `BillRatingPanel.tsx`)
- Khu vực **“Dịch vụ bổ sung”** trên Bill và **“Dịch vụ / Phụ phí tùy chọn”** trong Workspace Tính giá & Tài chính cùng kết nối trực tiếp vào bảng `service_selections` qua BFF `/bff/service-selections`.
- Đảm bảo tính nhất quán (Single Source of Truth): Bất kỳ thay đổi nào (chọn/bỏ chọn/đổi số lượng) trên màn hình này đều phản ánh tức thì trên màn hình kia sau khi lưu.
- Tuyệt đối không tự động chọn sẵn bất kỳ dịch vụ nào (mặc định `isSelected = false`).

---

## 4. BẢNG TỔNG HỢP CODE DIFF THEO TỪNG TẬP TIN

```
========================================================================================
File Path                                      | Thay đổi chính
========================================================================================
src/LCMS.Domain/Entities/                      | Bổ sung MasterCatalogKinds.OptionalService;
  MasterCatalogItem.cs                         | Đăng ký vào All kinds list.
----------------------------------------------------------------------------------------
src/LCMS.Application/Surcharges/               | Định nghĩa Baseline 4 dịch vụ; Parser JSON
  OptionalServiceCatalogSeeder.cs              | attributes; Hàm EnsureForTenantAsync &
                                               | EnsureForAllTenantsAsync.
----------------------------------------------------------------------------------------
src/LCMS.Application/Surcharges/               | Tạo Query & Handler cho API
  ListActivationServicesQuery.cs               | GET /api/surcharges/activation-services.
----------------------------------------------------------------------------------------
src/LCMS.Application/Surcharges/               | Cập nhật SurchargeActivation.EnsurePublishable
  SurchargeActivation.cs                       | ném ValidationAppException.CodedField.
----------------------------------------------------------------------------------------
src/LCMS.Application/Surcharges/               | PreparePublishAsync kiểm tra UNKNOWN_SERVICE,
  SurchargeCommands.cs                         | INACTIVE_SERVICE, INVALID_SCOPE;
                                               | UpdateSurchargeVersionCommand trả warnings;
                                               | ToConfig hỗ trợ linh hoạt top-level và nested.
----------------------------------------------------------------------------------------
src/LCMS.Application/Surcharges/               | MapVersion join MasterCatalogItem lấy
  SurchargeQueries.cs                          | serviceDisplayName; DTO trả serviceCode,
                                               | serviceDisplayName, selectionScope.
----------------------------------------------------------------------------------------
src/LCMS.Application/Tenants/Commands/         | Khi tạo Tenant mới, tự động seed danh mục
  CreateTenantCommand.cs                       | OptionalServiceCatalogSeeder.
----------------------------------------------------------------------------------------
src/LCMS.Application/Common/Exceptions/        | Thêm thuộc tính Field, hàm CodedField
  AppException.cs                              | cho ValidationAppException và Forbidden.
----------------------------------------------------------------------------------------
src/LCMS.Api/Middleware/                       | Error contract JSON payload bổ sung
  ExceptionHandlingMiddleware.cs               | trường field và errorCode chuẩn hóa.
----------------------------------------------------------------------------------------
src/LCMS.Api/Endpoints/                        | Endpoint GET /activation-services;
  SurchargeEndpoints.cs                        | Mapping SaveSurchargeRequest to ToActivation;
                                               | Return Ok(result) với warnings cho PUT draft.
----------------------------------------------------------------------------------------
src/LCMS.Api/Program.cs &                      | Đăng ký chạy seeder EnsureForAllTenantsAsync
  tests/.../LcmsApiFactory.cs                  | khi khởi động app và initialize test DB.
----------------------------------------------------------------------------------------
apps/web/lib/catalog-kinds.ts                  | Đăng ký kind "optional_service" vào frontend.
----------------------------------------------------------------------------------------
apps/web/lib/rate-cards-server.ts              | Bổ sung kiểu ActivationServiceItem;
                                               | Hàm listActivationServices fetcher.
----------------------------------------------------------------------------------------
apps/web/app/bff/surcharges/                   | Route BFF chuyển tiếp yêu cầu danh mục
  activation-services/route.ts                 | dịch vụ kích hoạt tới Backend API.
----------------------------------------------------------------------------------------
apps/web/components/SurchargeForm.tsx          | Dropdown lookup service_code; Inline warning;
                                               | Field-level error highlight; Scope/Basis select.
----------------------------------------------------------------------------------------
apps/web/app/rate-cards/surcharges/            | Truyền danh mục activationServices vào Form;
  new/page.tsx & [id]/page.tsx                 | serviceCode binding đúng draft (không fallback).
----------------------------------------------------------------------------------------
apps/web/app/rate-cards/surcharges/            | Bảng chi tiết phiên bản hiển thị
  [id]/versions/[versionId]/page.tsx           | "Dịch vụ kích hoạt" và "Phạm vi lựa chọn".
----------------------------------------------------------------------------------------
tests/LCMS.Api.Tests/                          | Toàn bộ 18 kịch bản tự động hóa UAT-SUR-SVC-01
  SurchargeOptionalServiceCodeTests.cs         | đến UAT-SUR-SVC-18 (PASS 100%).
========================================================================================
```

