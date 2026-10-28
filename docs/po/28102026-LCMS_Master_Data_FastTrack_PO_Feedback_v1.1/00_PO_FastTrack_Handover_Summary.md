# Bàn giao PO — đóng GAP Fast-Track (28/10/2026)

## Bản dựng và môi trường

| Mục | Giá trị |
|---|---|
| SHA đang chạy trước vòng này | `09c44a774e0a553f89be5ebdad13319be94f2ad7` — Deploy Priority 1 success, run https://github.com/thanhquyen129/CMS/actions/runs/40465733415 |
| SHA mã hoàn thiện | `e100364ceb86cfac3ecc332e51465f3df7261aa9` |
| SHA đang chạy trên https://cms.lsslogistics.vn sau vòng này | `7f316935e308d7ac4bc5dc9635569d9eaccb6968` |
| CI của bản mới | Run https://github.com/thanhquyen129/CMS/actions/runs/40672732296. Job test success 2026-10-28T10:34:40Z. Deploy Priority 1 success 2026-10-28T10:41:27Z. Deploy Secondary success 2026-10-28T10:45:33Z. |
| Smoke sau deploy | `GET /health` → `status=ok` lúc 2026-10-28T11:17:06Z. `GET /ready` → 200. |

## PASS / FAIL

| Hạng mục | Kết quả |
|---|---|
| `dotnet build Cms.sln -c Release` | PASS, 0 error |
| `npm run build` (`apps/web`, Next.js 15.5.7) | PASS, 0 error |
| Backend `dotnet test` | 586 PASS, 2 FAIL, 0 skip, tổng 588 |
| Frontend `npm test` (vitest) | 47 PASS / 7 file |
| Playwright | Không có script playwright trong `apps/web/package.json`. Không chạy. |
| API thực tế | PASS bằng test HTTP: tenant không còn mã active thì `AIR` trả 400 `transport_mode_catalog_empty`; `RANDOM_MODE` / `XYZ` trả 400 `transport_mode_not_allowed`. Có danh mục thì `air` lưu `AIR`. `OCEAN` không được suy ra từ `SEA`. `WATER` trên bảng giá và `EXPRESS` bị chặn. `document_type` `Invoice` lưu `invoice`. `service_type` `AIR` bị chặn khi tạo mới. |
| UI trình duyệt | PASS trên local `http://127.0.0.1:3000` (API `127.0.0.1:8080`). Đăng nhập xong mở được `/admin/catalog?kind=transport_mode` (bảng mã, không overlay lỗi), gửi `Invoice` ở loại chứng từ thì danh sách vẫn là `invoice` chữ thường, `/rate-cards/surcharges/new` hiện ô Phương thức (Không giới hạn / AIR / SEA / ROAD / RAIL / WATER / OCEAN), `/rate-cards/new` hiện dropdown phương thức và dịch vụ `FREIGHT`, `DOOR_TO_DOOR`, Bill `TESTLSS002` mở được phần tính giá. Log `next dev` không có lỗi runtime. |

### PostgreSQL

| Bài | Kết quả | Lý do |
|---|---|---|
| `RateScopeMigrationPostgresTests.Ac30_LegacyHeaderScopeIsCopiedWithoutDroppingCardsOrRatings` | FAIL | Máy này không có Docker (`docker` không có trong PATH, không có `C:\Program Files\Docker\Docker\resources\bin\docker.exe`) và không có binary Postgres (`psql`/`pg_ctl` không có, không có dịch vụ `postgres`). Không bật được cổng `5433`. Lỗi khi chạy test: `Failed to connect to 127.0.0.1:5433`, connection refused. |
| `RateScopeMigrationPostgresTests.Baseline_UpDownUp_KeepsNullChargeCodes_AndHistoricalAmounts` | FAIL | Cùng nguyên nhân, cùng cổng `5433`. |
| Tổng | 586 PASS, 2 FAIL | Không phải 588/588. Workflow CI không chạy `dotnet test` (`if: false`) và Postgres của CI lắng nghe `5432`, không phải `5433` mà hai bài này yêu cầu. |

## Đóng 5 mục

| Mục | Kết quả | File chính |
|---|---|---|
| 1. Transport mode | Ghi mới phải nằm trong danh mục active của đúng tenant và trong bộ mã hệ thống. Danh mục active rỗng thì từ chối. `EXPRESS` không phải mode. `WATER` chỉ bị chặn trên quy tắc giá / bảng giá / import giá. `OCEAN` giữ nguyên chữ, không đổi thành `SEA`; không có trong danh mục thì từ chối. `MULTIMODAL` chỉ trên Shipment và phải có trong danh mục. | `TransportModeCatalog.cs`, các command Order/Bill/Shipment/Leg/Movement/Rate card/Pricing rule/Surcharge/Import/Rating, `CreateTenantCommand.cs` |
| 2. Danh mục | `document_type`, `party_role`, `special_flag` chữ thường. `location` giữ nguyên chữ đã nhập. Các kind còn lại chữ hoa. Seeder không còn gán service `AIR` từ mode `air`. | `CatalogCodePolicy`, `UpsertMasterCatalogItemCommand.cs`, `DemoDataSeeder.cs`, `DemoVolumeCatalogSeeder.cs` |
| 3. Phụ phí | Giữ so sánh theo ngày lịch, biên đóng. Không hoàn nguyên. | `RatingEngine.IsBeforeEffectiveDay`, `SurchargeRating.IsEffective`, test `SurchargeEffectiveDay_IsClosedOnTheCalendarDay` |
| 4. UAT | Deploy Priority 1 success. API GET trên UAT đọc được Bill `TESTLSS002` và rating stale `AIR → air`. Không SSH, không `UPDATE`. Script `psql` một chạm ở file `01`. | Mục 4 file `01` |
| 5. Test và build | Số liệu ở bảng trên. | `MasterDataFastTrackClosureTests.cs` |

Tenant mới tạo qua API nhận baseline `AIR`, `SEA`, `ROAD`, `RAIL`, `WATER`, `OCEAN`, `MULTIMODAL`. Tenant đã có sẵn không bị chèn thêm dòng.

## GAP còn chờ PO

1. Không gộp hai nguồn Location, không xóa dòng catalog `location`.
2. Không UPDATE mode chữ thường đã lưu, không xóa `stale_at`, không đụng snapshot / expected cost / doanh thu / AP / AR.
3. Dòng `service_type = AIR` đã có trên DB cũ không bị xóa. Seeder mới không tạo thêm.
4. Chưa thêm unique index không phân biệt hoa thường. Đó là migration schema, chưa chạy.
5. Hai bài Postgres `5433` chưa PASS vì máy không có Docker và không có binary Postgres.
6. Script `psql` một chạm vẫn nằm ở file `01` nếu PO muốn đối chiếu trực tiếp trong container DB. API read-only trên UAT đã đọc được Bill và một rating; không thay thế toàn bộ `SELECT` trong DB.
