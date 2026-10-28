# P0 — Transport Mode và Rating Staleness

## Chuẩn hóa

`RateFieldPolicy.ToStoredMode` và `ShipmentModePolicy.Normalize` trả mã đã trim, `UPPERCASE`. `SameMode` coi `AIR`, `air` và `  AIR  ` là một mã.

`TransportModeCatalog.ResolveAsync` (`src/LCMS.Application/Catalog/TransportModeCatalog.cs`) đối chiếu `master_catalog_items` (`kind = transport_mode`, `is_active = true`) của đúng tenant, so bằng `RateFieldPolicy.SameMode`.

### Khi tenant đã có ít nhất một mã `transport_mode` đang active

Mã gửi lên phải khớp một dòng active (không phân biệt hoa thường, có trim). Không khớp thì `ValidationAppException` với `RateFieldPolicy.ModeNotInCatalog` (`"Phương thức không có trong danh mục đang dùng."`). Resolver không `INSERT` dòng danh mục. Dropdown web (`catalogModeOptions` trong `apps/web/lib/create-workspace.ts`) chỉ render mã catalog đang active. Hết danh sách cứng `air/sea/road`.

Trên `lcms_dev.db` tenant `ops` có 1 dòng `transport_mode` / `air` / active. Mọi ghi mới của tenant này chỉ nhận `AIR` (và các mã active khác nếu PO thêm sau). `SEA` không có trong danh mục này nên API từ chối cho đến khi PO thêm mã. Đó là lý do seed volume vẫn đang là chữ thường từ trước, còn ghi mới phải đi qua danh mục.

### Khi tenant chưa có dòng `transport_mode` nào

Resolver không tạo dòng danh mục. Chuỗi trống thành null. Chuỗi dài hơn 32 ký tự bị từ chối. `MULTIMODAL` chỉ được khi caller bật `allowMultimodal` (Shipment và import Shipment). Leg, Movement, Order, Bill ném `"Đa phương thức chỉ là phân loại của Shipment, không gán cho chặng."` (`ShipmentModePolicy.IsMultimodal`).

Quy tắc giá mới vẫn gọi `RateFieldPolicy.NewModeBlock`: `WATER` trả `WaterBlocked`, `EXPRESS` trả `ExpressBlocked` vì `ExpressContextSupported` là false. UI ma trận (`selectableModes` trong `apps/web/lib/rate-field-policy.ts`) cũng bỏ `WATER`, `EXPRESS`, `MULTIMODAL` khỏi lựa chọn quy tắc mới. `OCEAN` vẫn chỉ được gom vào họ biển lúc tính W/M (`ShipmentModePolicy.IsSeaFamily`), không bị ghi thành `SEA`.

Dropdown tạo bảng giá / phụ phí / tính giá khi catalog rỗng hiện `"Chưa có danh mục phương thức"`, không chèn `AIR` giả. Phần còn lại: API/import của tenant chưa có danh mục vẫn lưu chuỗi đã `UPPERCASE` nếu không vướng các chặn trên, và không ghi vào `master_catalog_items`. Việc đóng nốt cửa đó (chỉ nhận AIR/SEA/ROAD/RAIL/OCEAN/WATER/EXPRESS/MULTIMODAL) nằm ở file `04`, vì siết thêm sẽ đổi test tenant trống và cần PO khóa danh sách.

Ghi mới / cập nhật có chủ đích đi qua resolver tại:

1. Order — `UpsertOrderCommand`
2. Bill — `UpdateBillContextCommand`, `EditOperationalReferenceCommand`
3. Shipment — `UpsertShipmentCommand` (cho phép `MULTIMODAL`)
4. Leg — `UpsertTransportLegCommand` (không gán `MULTIMODAL`)
5. Movement — `UpsertTransportMovementCommand`
6. Rate Card — tạo mới lưu `UPPERCASE`; sửa bảng đã có giữ nguyên chữ nếu cùng mã (không viết lại `air` thành `AIR` trên bảng đã phát hành)
7. Pricing Rule — draft mới dùng `ToStoredMode`. Phiên bản đã phát hành không sửa.
8. Surcharge — điều kiện `dimension = transport_mode` lưu `UPPERCASE`
9. Import vận hành — `ShipmentModePolicy.Normalize` + kiểm danh mục nếu tenant đã có mã
10. Rating — `RatingEngine.ModeMatches` / `SameMode`; snapshot mới lưu mã hoa. Snapshot cũ không bị update.

Dropdown web lấy `listCatalog("transport_mode")`. Không còn mảng cứng `air/sea/road` trên lọc bảng giá, phụ phí, tính giá, so sánh giá, form phụ phí. Editor vận hành không fallback mã cứng. `WATER` và `EXPRESS` vẫn bị chặn khi tạo quy tắc giá mới.

`OCEAN` vẫn thuộc họ biển khi tính W/M. Mã lưu vẫn là `OCEAN`, không bị đổi thành `SEA`.

## Stale

`UpdateBillContextCommand` so sánh `transport_mode` bằng `SameMode`, và so route / origin / destination / service / rate date policy sau khi trim, không phân biệt hoa thường.

`RatingStalenessService` bỏ qua khác biệt hoa/thường giữa `context_json.transportMode` và mode hiện tại. Đổi mode thật (ví dụ `AIR` → `SEA`) vẫn đánh dấu stale. Đổi gross/CW vẫn stale khi basis dùng trọng lượng. Đổi khách hàng, vendor, ngày áp dụng trên Bill cũng nằm trong tập so sánh.

Không xóa `stale_at` đã có. Không sửa `context_json` của rating cũ. Không sửa Expected Cost / Revenue.

## Bằng chứng test

`MasterDataStandardizationTests.HistoricalAir_SavedAsAir_DoesNotStale_SeaAndWeightStillDo_SnapshotStays`

- Bill đang lưu `air`, sửa thành `AIR`: 0 rating stale, `context_json` và số tiền giữ nguyên, cột bill vẫn `air`.
- Sửa thành `SEA`: 1 rating stale, snapshot và số tiền vẫn giữ nguyên, bill lưu `SEA`.
- Đổi gross 85 → 120: stale, snapshot và số tiền giữ nguyên.

`MasterDataStandardizationTests.PublishedRateCard_KeepsStoredModeBytes_WhenTheSameModeIsSentInAnotherCase`

- Tạo mới lưu `AIR`.
- Bản ghi đã đưa về `air` rồi phát hành: gửi lại `AIR` không đổi byte `transport_mode`, version vẫn `published`.

`ShipmentModePolicyTests` giữ WATER ≠ SEA và EXPRESS không thành AIR.

## Bill TESTLSS002 — đọc lại `src/LCMS.Api/lcms_dev.db` ngày 2026-10-28

Câu lệnh read-only, URI `mode=ro`. Postgres `5432`/`5433` không mở nên không có bản sao UAT trong lượt này. Script để PO chạy cùng câu trên PostgreSQL nằm ở file `03`.

Bill: `01A16CF7-30D8-731C-A50F-928FC7AA575B`, số `TESTLSS002`, `transport_mode = air`, `service_type_code = FREIGHT`, `route_code = SGN-HAN`, `created_at = 2026-10-24 08:11:41.7715121+00:00`, `updated_at` null.

14 rating, tất cả `status = completed`, `context_type = bill`, `party_type_code = vendor` (chiều mua), `stale_at` null, `stale_reason` null. `context_json.transportMode = air`, `serviceType = FREIGHT`.

| # | Rating ID | rated_at (UTC) | BUY/SELL | Scope | context transportMode | total_amount | currency | basis | rate_version_id | stale_at | stale_reason |
|---|---|---|---|---|---|---:|---|---|---|---|---|
| 1 | `01A16DC5-FF89-7B71-8C52-E95C69083DD1` | 2026-10-24 11:57:34.9866322+00 | vendor / BUY | bill | air | 1440000 | VND | manual | `01A16DBD-BFFA-7065-A883-7260C83D4CBA` | null | null |
| 2 | `01A16DC6-035B-7DA4-AC25-DBF18CEFC885` | 2026-10-24 11:57:35.9630898+00 | vendor / BUY | bill | air | 1440000 | VND | manual | `01A16DBD-BFFA-7065-A883-7260C83D4CBA` | null | null |
| 3 | `01A16DC6-03B6-70FE-8F91-505DC713566B` | 2026-10-24 11:57:36.0548013+00 | vendor / BUY | bill | air | 1440000 | VND | manual | `01A16DBD-BFFA-7065-A883-7260C83D4CBA` | null | null |
| 4 | `01A16DC6-0414-7987-BA70-016795D4B351` | 2026-10-24 11:57:36.1488027+00 | vendor / BUY | bill | air | 1440000 | VND | manual | `01A16DBD-BFFA-7065-A883-7260C83D4CBA` | null | null |
| 5 | `01A16DC6-0490-7A3D-82B0-2E0EA580B23F` | 2026-10-24 11:57:36.2721294+00 | vendor / BUY | bill | air | 1200000 | VND | manual | `01A16CF7-3B7A-7F90-8A6E-DEDDBF487F62` | null | null |
| 6 | `01A16DC6-04D8-7E03-9D63-EA7CB29884C9` | 2026-10-24 11:57:36.3442952+00 | vendor / BUY | bill | air | 1200000 | VND | manual | `01A16DBD-BFFA-7065-A883-7260C83D4CBA` | null | null |
| 7 | `01A16DD2-5DDE-7575-8836-7B4F16B69A65` | 2026-10-24 12:11:05.5663847+00 | vendor / BUY | bill | air | 1440000 | VND | manual | `01A16DD0-D08F-72D4-9C3C-01C3E108EFF6` | null | null |
| 8 | `01A16DD2-5EAC-767A-8B9F-6918F85DAB4F` | 2026-10-24 12:11:05.7728923+00 | vendor / BUY | bill | air | 1440000 | VND | manual | `01A16DD0-D08F-72D4-9C3C-01C3E108EFF6` | null | null |
| 9 | `01A16DD2-5F45-7524-B774-EE45A41F7EE4` | 2026-10-24 12:11:05.9255797+00 | vendor / BUY | bill | air | 1440000 | VND | manual | `01A16DD0-D08F-72D4-9C3C-01C3E108EFF6` | null | null |
| 10 | `01A16DD2-614E-70DA-ADF3-43110EC494A2` | 2026-10-24 12:11:06.4468+00 | vendor / BUY | bill | air | 1440000 | VND | manual | `01A16DD0-D08F-72D4-9C3C-01C3E108EFF6` | null | null |
| 11 | `01A16DD2-6266-712F-ACFA-75DA3918DBCE` | 2026-10-24 12:11:06.7261409+00 | vendor / BUY | bill | air | 1200000 | VND | manual | `01A16CF7-3B7A-7F90-8A6E-DEDDBF487F62` | null | null |
| 12 | `01A16DD2-62D3-7B71-9C81-67B3E1C9B8B6` | 2026-10-24 12:11:06.8352429+00 | vendor / BUY | bill | air | 1200000 | VND | manual | `01A16DD0-D08F-72D4-9C3C-01C3E108EFF6` | null | null |
| 13 | `01A16DD7-3054-7549-A197-CE93787701AC` | 2026-10-24 12:16:21.5888635+00 | vendor / BUY | bill | air | 1440000 | VND | manual | `01A16DD6-8B84-78AA-A9C8-509E0224A2CF` | null | null |
| 14 | `01A16DD7-F111-78AA-A9C8-509E0224A2CF` | 2026-10-27T00:00:00Z | vendor / BUY | bill | air | 1740000 | VND | actual_or_formula | `01A16DD6-8B84-78AA-A9C8-509E0224A2CF` | null | null |

Không có rating SELL trên Bill này. Hai rating khác trong cùng file DB không thuộc Bill này: `DEMO-10-EMPTY` (`serviceType` context = `freight`) và `VOL-BILL-0120` (`serviceType` context = `master`).

`audit_events` không có dòng nào có `object_id` của Bill, không có `reason`/`before_json`/`after_json` chứa `TESTLSS002`, và không có action `rating.mark_stale`. Toàn bảng audit của file này chỉ có `rate_card.version_created` 24, `rate_card.declaration_changed` 24, `rate_card.published` 3.

Cost của Bill, không sửa trong lượt này: `01A16DD7-C111-78AA-A9C8-509E0224A2CF` amount 1440000 `FREIGHT` expected `rating_detail`; `01A16DD7-C222-78AA-A9C8-509E0224A2CF` amount 300000 `DONGGOI_SGN_HAN_MUA` expected `rating_detail`. Tổng 1.740.000 khớp `total_amount` của rating 14.

Cảnh báo UAT `AIR → air` không có trong file SQLite này. Nguyên nhân đã ghi ở RCA 27/10: form cũ ép `ToLowerInvariant` rồi so sánh ordinal. Code `d2c7c1c` so bằng `SameMode` nên lần lưu cùng phương thức không tạo stale mới và không sửa 14 dòng `context_json`. Nếu UAT PostgreSQL vẫn còn `stale_at`, file `04` xin PO trước khi xóa.
