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

### Deploy

`main` đã ở `7f316935e308d7ac4bc5dc9635569d9eaccb6968`. Run https://github.com/thanhquyen129/CMS/actions/runs/40672732296: Deploy Priority 1 success 2026-10-28T10:41:27Z. Sau đó `/health` vẫn `ok`.

Không thêm bước CI đọc DB. SSH vào `deploy@169.58.47.30` vẫn `Permission denied`. Postgres không mở ra internet.

### Đọc qua API GET trên UAT

Chỉ `GET`. Không `POST`/`PUT`/`PATCH` sửa Bill, rating, hay danh mục. Tenant của phiên đăng nhập: `01a1007b-75da-73b5-b57d-0f580c5286c1`.

Danh mục active:

| Kind | Mã |
|---|---|
| `transport_mode` | `AIR` (tên `AIR`, tạo 2026-10-05). Không có `SEA`/`ROAD`/`RAIL` trên tenant này. |
| `service_type` | `FREIGHT`, `AIR` |
| `document_type` | `invoice`, `debit_note` |

Bill `TESTLSS002` `01a12e67-644c-7eb5-9e5c-dd218df54111`:

| Trường | Giá trị |
|---|---|
| `transportMode` | `air` |
| `serviceTypeCode` | `FREIGHT` |
| `routeCode` | `SGN-HAN` |
| `etdAt` | 2026-10-26T05:02:00Z |
| `etaAt` | null |
| Ngữ cảnh hàng | gross 120 kg, chargeable 120 kg. `context.serviceType` trong JSON ngữ cảnh là `AIR`, khác cột `serviceTypeCode`. |

Một rating `01a17724-20ef-7f69-97d5-d0295f361acd`, `completed`, chiều `vendor`, 1.740.000 VND, chargeable 120.

| | Bill đang lưu | Snapshot rating |
|---|---|---|
| Phương thức | cột `air` | `transportMode` = `AIR` |
| Dịch vụ | cột `FREIGHT` | `serviceType` = `FREIGHT` |
| Trọng lượng | 120 kg | gross 120, chargeable 120 |
| Stale | — | `staleAt` 2026-10-27T02:49:44Z |

`staleReason`: `Bill TESTLSS002 thay đổi — Phương thức vận chuyển: AIR → air`.

Đó là cờ cũ do khác hoa/thường. Snapshot vẫn `AIR`, cột Bill vẫn `air`, số tiền không đổi. Code hiện so cùng mã không phân biệt hoa thường và không xóa cờ này. Không có lệnh sửa snapshot hay expected cost 1.740.000.

### Script một chạm trên máy chủ UAT

Chạy trong `/opt/cms`, chỉ in ra, không ghi.

```bash
cd /opt/cms
docker compose -f infra/docker-compose.host.yml --env-file infra/.env exec -T db \
  psql -U lcms -d lcms -c "SELECT b.bill_no, b.transport_mode, b.service_type_code, b.etd_at, r.id, r.total_amount, r.stale_at, r.stale_reason, r.context_json->>'transportMode' AS snap_mode FROM bills b LEFT JOIN ratings r ON r.bill_id = b.id AND r.deleted_at IS NULL WHERE b.bill_no = 'TESTLSS002' AND b.deleted_at IS NULL;"
```

Đối chiếu danh mục:

```bash
docker compose -f infra/docker-compose.host.yml --env-file infra/.env exec -T db \
  psql -U lcms -d lcms -c "SELECT kind, code, is_active FROM master_catalog_items WHERE tenant_id = '01a1007b-75da-73b5-b57d-0f580c5286c1' AND deleted_at IS NULL AND kind IN ('transport_mode','service_type','document_type') ORDER BY kind, code;"
```

Không có `UPDATE` trong hai lệnh.

## Mục 5 — Test

- Release build: PASS.
- `dotnet test`: 586 PASS, 2 FAIL. Hai bài `RateScopeMigrationPostgresTests` nối `127.0.0.1:5433`. Máy không có Docker và không có binary Postgres nên không bật được cổng đó.
- vitest: 47 PASS.
- `next build`: PASS.
- Trình duyệt local: bốn màn ở file `00` mở được, log dev không có lỗi runtime. Gửi mã `Invoice` trên form danh mục giữ `invoice` chữ thường.
