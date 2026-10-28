# Trả lời 5 mục PO — 28/10/2026

Không viết lại RCA. Không migration lịch sử. Không UPDATE dữ liệu tài chính và không xóa `stale`.

## Mục 1 — Transport mode

`TransportModeCatalog.Decide` là một cổng cho mọi ghi mới: Order, Bill, Shipment, Leg, Movement, Rate card, Pricing rule, điều kiện phụ phí `transport_mode`, import vận hành, import bảng giá, rating.

Thứ tự:

1. Trùng mã đang lưu (không phân biệt hoa thường) thì giữ nguyên byte cũ.
2. `MULTIMODAL` trên Leg, Movement, Order, Bill, bảng giá: `400` `transport_mode_multimodal_scope`.
3. `EXPRESS`: `400` `transport_mode_express_blocked`. Đây là tính chất dịch vụ, không phải mode. Không đổi thành `AIR`.
4. `WATER` trên quy tắc giá, bảng giá, import giá, ma trận giá: `400` `transport_mode_water_blocked`. Trên Shipment/Leg vẫn lưu `WATER` nếu danh mục tenant đang có `WATER`. Không đổi thành `SEA`.
5. Mã ngoài bộ `AIR`, `SEA`, `ROAD`, `RAIL`, `WATER`, `OCEAN`, `MULTIMODAL`: `400` `transport_mode_not_allowed`.
6. Tenant không có dòng `transport_mode` active: `400` `transport_mode_catalog_empty`.
7. Có danh mục nhưng không khớp: `400` `transport_mode_not_in_catalog`.
8. `OCEAN` chỉ được lưu khi chính dòng `OCEAN` đang active. Có `SEA` không làm `OCEAN` hợp lệ. Chữ lưu vẫn là `OCEAN`. Họ biển chỉ dùng khi tính W/M.

Thông điệp tiếng Việt đi cùng `code` và `error_code` trên JSON lỗi.

Bằng chứng test:

- `EmptyCatalog_RejectsRecognizedAndGarbageModes`
- `LockedModes_StayWaterExpressOceanMultimodal`
- `EmptyCatalog_BlocksAirAndGarbage_OnRateCardAndBill`
- `ActiveCatalog_StoresUppercase_AndKeepsOceanDistinct`

Tenant tạo qua `POST /api/tenants` được gắn baseline bảy mã trên. Tenant cũ không bị ghi thêm.

## Mục 2 — Danh mục

### Chuẩn hóa mã

| Kind / nhóm | Quy tắc | Chỗ áp dụng |
|---|---|---|
| `document_type` | chữ thường (`invoice`, `debit_note`, `credit_note`) | `CatalogCodePolicy.Normalize`. API quản trị không còn `ToUpperInvariant` cho kind này. Test: gửi `Invoice` → lưu `invoice`. |
| `party_role` | chữ thường | Policy dùng chung. Lệnh gán vai trò đã `ToLowerInvariant` từ trước. |
| `special_flag` | chữ thường (`dg`, `reefer`, `oog`, `other`) | Policy dùng chung. Editor vận hành giữ chữ thường. |
| `location` | giữ nguyên sau trim | Không ép hoa. |
| `transport_mode`, `service_type`, `cost_type`, `revenue_type`, `pricing_component`, `payment_term`, `transport_route` | chữ hoa | API quản trị và lệnh tuyến (service). |
| `charge_code` / khoản giá, `currency` | chữ hoa | Giữ như cũ trên rule, surcharge, currency. |
| UOM | `kg` hoặc `W/M` theo `ChargeableWeightPolicy.UomFor` | Xác nhận trọng lượng tính cước dùng đúng hàm này. Không sửa dòng đo cũ. |

Tạo mới `service_type = AIR` trên API quản trị trả `400` `service_type_not_a_mode`. Dòng `AIR` đã có vẫn sửa tên / tắt được.

### Seeder

`DemoVolumeCatalogSeeder` không còn `ServiceType = mode == "air" ? "AIR" : "FREIGHT"`. Mọi mẫu mới dùng `FREIGHT`.

`DemoDataSeeder.EnsureDefaultCatalogAsync` không còn dịch vụ `AIR`. Dịch vụ mẫu là `FREIGHT` và `DOOR_TO_DOOR`. Mode mẫu vẫn là `AIR` / `SEA` / `ROAD` / `RAIL`.

Dữ liệu đã sinh không bị UPDATE.

### Hai nguồn Location

| Nguồn | Vai trò | Ai đọc |
|---|---|---|
| Bảng `locations` (+ `location_aliases`) | Nguồn chuẩn địa điểm vận hành. Có id, loại, IATA/UNLOCODE. Route gắn `origin_location_id` / `destination_location_id`. | `GET/PUT /api/locations`, `CanonicalPlaceBinder`, form tạo Bill/Order/Leg, màn `/admin/locations` và `/admin/routes`. |
| `master_catalog_items.kind = location` | Lớp seed song song (`VOL-LOC-*` trên file local). Không có FK sang `locations`. | `GET/PUT /api/master-catalog?kind=location`, trang `/admin/catalog`. |

Quy tắc ngắn hạn: màn nghiệp vụ và bind điểm đi/đến chỉ dùng `locations`. Catalog `location` chỉ để xem trên màn quản trị danh mục.

Quy tắc dài hạn: PO chọn một nguồn cho dropdown. Khi đã chọn, mới lập kế hoạch chuyển dữ liệu. Lượt này không gộp, không xóa, không sửa dòng location nào.

## Mục 3 — Ngày hiệu lực phụ phí

Commit `d2c7c1c` (28/10/2026) đổi so sánh mốc thời gian (`asOf < validFrom`, `validFrom <= rateDate`) thành so sánh ngày lịch UTC:

- `rateDate` trước ngày `effective_from` → không áp dụng
- cùng ngày với `effective_from` hoặc `effective_to` → áp dụng
- sau ngày `effective_to` → không áp dụng

Lý do trong gói Fast-Track: phụ phí phát hành cùng ngày với ngày áp dụng của Bill bị loại vì lệch vài giây. Biên PO yêu cầu là đoạn đóng theo ngày `[effective_from, effective_to]`. Cách so ngày lịch khớp biên đó. Cách so mili-giây thì không.

Không hoàn nguyên. Không có đề xuất tách riêng.

Test `SurchargeEffectiveDay_IsClosedOnTheCalendarDay`: trước 28/10 không áp; 28/10 và 30/10 áp (kể cả giờ khác mốc); 31/10 không áp. `ValidFrom` mẫu 28/10 18:30 UTC, `ValidTo` 30/10 02:00 UTC.

## Mục 4 — UAT và TESTLSS002

### Môi trường đã chạm

- `GET https://cms.lsslogistics.vn/health` → `{"status":"ok","service":"cms-api"}` lúc 2026-10-28T10:11:19Z.
- `GET /ready` → HTTP 200.
- SSH `deploy@169.58.47.30` từ máy agent: `Permission denied (publickey,password)`.
- Postgres trên VPS không publish ra internet. Local `127.0.0.1:5432` và `5433` đều refused.

Chưa chạy SELECT trên PostgreSQL UAT. Số liệu dưới đây là file SQLite local `src/LCMS.Api/lcms_dev.db`, mở `mode=ro`. Đây không phải DB của https://cms.lsslogistics.vn.

### File local (không phải UAT)

Tenant `ops` `01A16CF7-2EF2-7299-9730-0EC040B35EDA`.

Bill `TESTLSS002` `01A16CF7-30D8-731C-A50F-928FC7AA575B`:

| Trường | Giá trị |
|---|---|
| `transport_mode` | `air` |
| `service_type_code` | `FREIGHT` |
| `route_code` | `SGN-HAN` |
| `etd_at` / `eta_at` | null |
| Đo lường | một dòng `chargeable_weight_kg` = 120, `uom` = `kg`. Không có cột `gross_weight` trên `bills`. |

14 rating `completed`, chiều `vendor`, `stale_at` null, `stale_reason` null. Snapshot `transportMode` = `air`, `serviceType` = `FREIGHT`. Số tiền 1.440.000, 1.200.000, và một dòng 1.740.000 VND. Không có rating bán.

Catalog local: `transport_mode` chỉ `air` active; `service_type` có `FREIGHT` và `AIR`; `document_type` là `invoice`, `debit_note`; `location` là 120 mã `VOL-LOC-*`.

Trên file này, `stale` không bật. Mode bill và snapshot cùng `air`. Nếu UAT vẫn hiện stale, đó là cờ đã lưu từ trước. Code vòng này không xóa cờ đó và không sửa snapshot.

### SQL read-only để chạy trên PostgreSQL UAT

Chạy trong container DB của `/opt/cms`, chỉ `SELECT`. Thay tên DB nếu khác `lcms`.

```sql
SELECT id, code, name FROM tenants WHERE code = 'ops';

SELECT kind, code, is_active, count(*) AS n
FROM master_catalog_items
WHERE tenant_id = (SELECT id FROM tenants WHERE code = 'ops')
  AND deleted_at IS NULL
GROUP BY kind, code, is_active
ORDER BY kind, code;

SELECT b.id, b.bill_no, b.transport_mode, b.service_type_code, b.route_code, b.etd_at, b.eta_at
FROM bills b
JOIN tenants t ON t.id = b.tenant_id
WHERE t.code = 'ops' AND b.bill_no = 'TESTLSS002' AND b.deleted_at IS NULL;

SELECT m.measure_code, m.quantity, m.uom
FROM operational_measurements m
JOIN bills b ON b.id = m.object_id
JOIN tenants t ON t.id = b.tenant_id
WHERE t.code = 'ops' AND b.bill_no = 'TESTLSS002';

SELECT r.id, r.status, r.party_type_code, r.total_amount, r.currency_code,
       r.chargeable_weight_kg, r.stale_at, r.stale_reason,
       r.context_json->>'transportMode' AS snap_mode,
       r.context_json->>'serviceType' AS snap_service
FROM ratings r
JOIN bills b ON b.id = r.bill_id
JOIN tenants t ON t.id = b.tenant_id
WHERE t.code = 'ops' AND b.bill_no = 'TESTLSS002' AND r.deleted_at IS NULL
ORDER BY r.rated_at;
```

Không có `UPDATE` trong script.

## Mục 5 — Test

- Release build: PASS.
- `dotnet test`: 586 PASS, 2 FAIL (hai bài Postgres `5433` ở trên).
- vitest: 47 PASS.
- `next build`: PASS sau khi sửa chỗ `transportMode` có thể null trong `SurchargeForm.tsx`.
