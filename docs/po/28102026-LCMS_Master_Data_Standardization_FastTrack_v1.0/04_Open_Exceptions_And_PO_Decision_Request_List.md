# Việc cần PO quyết một lần

| # | Việc | Đề xuất | Nếu không duyệt |
|---|---|---|---|
| 1 | Xóa cờ stale cũ trên UAT, gồm Bill `TESTLSS002` nếu còn lý do `AIR → air` | Xóa từng rating sau khi đối chiếu audit. Không xóa snapshot, không đổi số tiền | Cờ cũ vẫn hiện đến khi tính lại. Lưu Bill cùng mode không tạo cờ mới |
| 2 | Chạy script `03` để đưa `air/sea/road/rail` lịch sử lên `AIR/SEA/ROAD/RAIL` | Có backup + rollback. Không gồm rating snapshot và rate card đã phát hành | Đọc/so khớp trong bộ nhớ vẫn đúng. Cột DB giữ chữ thường |
| 3 | Đưa Incoterm (`FOB`, `CIF`, …) vào `master_catalog_items` | Giữ dropdown ICC đến khi có kind riêng | Không ép UPPERCASE ngoài bộ ICC đang dùng |
| 4 | `DemoVolumeCatalogSeeder` gán Service Type `AIR` khi mode là air | Tách: mode `AIR`, service `FREIGHT` cho seed mới. Không sửa DB hiện có | Seed cũ giữ nguyên đến lần seed lại |
| 5 | Index unique không phân biệt hoa thường trên `(tenant_id, kind, code)` | Migration schema, không update dữ liệu | Validate ứng dụng đã chặn trùng hoa/thường khi ghi |
