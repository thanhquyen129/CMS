# P0 — Transport Mode và Rating Staleness

## Chuẩn hóa

`RateFieldPolicy.ToStoredMode` và `ShipmentModePolicy.Normalize` trả mã đã trim, `UPPERCASE`. `SameMode` coi `AIR`, `air` và `  AIR  ` là một mã.

`TransportModeCatalog.ResolveAsync` đối chiếu `master_catalog_items` (`kind = transport_mode`, `is_active`) theo tenant, không phân biệt hoa thường. Danh mục trống thì không tự thêm dòng; mã mới vẫn được lưu `UPPERCASE`. Khi tenant đã có danh mục, mã không khớp bị từ chối.

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

## Bill TESTLSS002

Nguồn đã kiểm trên `lcms_dev.db` (RCA 2026-10-28, không query lại DB PO trong lượt này):

- Bill `01A16CF7-30D8-731C-A50F-928FC7AA575B`, mode lưu `air`, service `FREIGHT`, route `SGN-HAN`.
- 14 rating completed. Trên DB local, `stale_at` đều NULL, kể cả bản 2026-10-27. `audit_events` local không có `rating.mark_stale`.
- Cảnh báo UAT `AIR → air` sinh ra trước khi so sánh ngữ cảnh: form ép `ToLowerInvariant`, so sánh ordinal coi `AIR` khác `air`, rồi `MarkForBillChangeAsync` ghi stale. Snapshot vẫn `air`. Số tiền UAT đã khóa: Net 1.740.000 ₫, VAT 139.200 ₫, Gross 1.879.200 ₫.

Sau bản này, lưu lại cùng phương thức không tạo stale mới và không sửa 14 snapshot. Cờ stale đã ghi trên UAT (nếu còn) không bị xóa. Việc xóa cờ đó nằm ở danh sách xin PO.
