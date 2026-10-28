# Việc cần PO quyết một lần

| # | Việc | Việc đã biết trên `lcms_dev.db` | Đề xuất | Nếu không duyệt |
|---|---|---|---|---|
| 1 | Xóa cờ stale cũ trên UAT nếu còn lý do `AIR → air` | File SQLite: 14 rating của `TESTLSS002` đều `stale_at` null. Audit Bill này: 0 dòng. Không có `rating.mark_stale` | Chạy script đọc ở file `03` trên PostgreSQL UAT. Chỉ xóa `stale_at`/`stale_reason` của đúng rating PO chỉ định. Không sửa `context_json`, không đổi 1.440.000 và 300.000 | Cờ UAT nếu có vẫn hiện. Lưu cùng mode không tạo cờ mới |
| 2 | Chạy script uppercase ở file `03` | 120 order, 122 bill, 120 shipment, 1 route, 1 catalog `air`, 3 rate card published (`NSE-AIR-VN-MY`, `NSE-SEA-VN-MY`, `TDNT_SGN_HAN`), 3 pricing rule published. Draft rule mode và surcharge mode: 0 | Backup + rollback đã viết. Loại published rate card, published pricing rule, published surcharge, ratings, cost, revenue, AP/AR | Cột DB giữ chữ thường. `SameMode` vẫn khớp |
| 3 | Tenant chưa có dòng `transport_mode` | Tenant `ops` đã có 1 dòng `air`, nên ghi mới của tenant này bị chặn nếu mã không khớp. Tenant trống: API không insert catalog, UI không bịa dropdown, nhưng API vẫn nhận chuỗi UPPERCASE ngoài bộ AIR/SEA/ROAD/RAIL nếu không dính chặn MULTIMODAL / WATER / EXPRESS | Khóa allow-list khi catalog trống: chỉ AIR, SEA, ROAD, RAIL, OCEAN, WATER, EXPRESS, và MULTIMODAL cho Shipment | Giữ hành vi hiện tại, ghi ở file `01` |
| 4 | Incoterm chưa có kind | 132 bill, `incoterm_code` toàn NULL. UI vẫn là hằng ICC trong `apps/web/lib/create-workspace.ts` | Thêm kind khi PO muốn quản trị EXW/FOB/CIF | Giữ hằng ICC |
| 5 | `DemoVolumeCatalogSeeder` gán service `AIR` theo mode air | Catalog service đang có cả `AIR` và `FREIGHT`. Một bill lưu service `AIR` | Seed mới: mode `AIR`, service `FREIGHT`. Không UPDATE bill cũ | Seed cũ giữ đến lần seed lại |
| 6 | Unique index không phân biệt hoa thường `(tenant_id, kind, code)` | Chưa có cặp trùng trong file này | Migration schema, không UPDATE dữ liệu | Ứng dụng đã từ chối mã lệch hoa thường khi catalog có dòng |
| 7 | Hai nguồn địa điểm | `locations`: `SGN`, `HAN`. Catalog kind `location`: 120 mã `VOL-LOC-*` | Chọn một nguồn cho dropdown | Giữ cả hai cho đến khi PO chọn |
| 8 | VAT chưa có bảng master 0/8/10 | VAT nằm trên version bảng giá và phụ phí | Lập kind khi PO muốn | Giữ số trên version |
