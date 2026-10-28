# Bàn giao PO — đóng GAP Fast-Track (28/10/2026)

## Bản dựng và môi trường

| Mục | Giá trị |
|---|---|
| SHA đang chạy trên https://cms.lsslogistics.vn trước bản này | `09c44a774e0a553f89be5ebdad13319be94f2ad7` |
| Cách xác định | Run CI thành công cuối cùng có job Deploy Priority 1 success: https://github.com/thanhquyen129/CMS/actions/runs/40465733415. `/health` lúc kiểm tra trả `status=ok` (2026-10-28T10:11:19Z). |
| Push sau đó chưa lên máy | `af9a1927ebbd6199e5a863582c1fe128512d129e` — job test **failure**, hai job deploy **skipped**: https://github.com/thanhquyen129/CMS/actions/runs/40637870588 |
| SHA mã hoàn thiện của vòng này | `e100364ceb86cfac3ecc332e51465f3df7261aa9` |
| SHA tip trên `main` sau commit ghi SHA | commit ngay sau `e100364`, chỉ sửa hai file báo cáo này |
| CI/CD | GitHub Actions workflow `CI` trên `main`. Deploy Priority 1 rsync `/opt/cms` rồi `docker compose up`. Bước `dotnet test` trong workflow đang `if: false`. Cổng vào deploy là build Release, smoke `/health` `/ready`, và `npm run build`. |

Sau khi push `main`, SHA trên site chỉ đổi khi job Deploy Priority 1 của tip success.

## PASS / FAIL

| Hạng mục | Kết quả |
|---|---|
| `dotnet build Cms.sln -c Release` | PASS, 0 error |
| `npm run build` (`apps/web`, Next.js 15.5.7) | PASS, 0 error |
| Backend `dotnet test` | 586 PASS, 2 FAIL, 0 skip, tổng 588 |
| Frontend `npm test` (vitest) | 47 PASS / 7 file |
| Playwright | Không có script playwright trong `apps/web/package.json`. Không chạy. |
| API thực tế | PASS bằng test HTTP: tenant không còn mã active thì `AIR` trả 400 `transport_mode_catalog_empty`; `RANDOM_MODE` / `XYZ` trả 400 `transport_mode_not_allowed`. Có danh mục thì `air` lưu `AIR`. `OCEAN` không được suy ra từ `SEA`. `WATER` trên bảng giá và `EXPRESS` bị chặn. `document_type` `Invoice` lưu `invoice`. `service_type` `AIR` bị chặn khi tạo mới. |
| UI trình duyệt | Không đổi màn hình. Sửa một lỗi kiểu TypeScript ở `SurchargeForm.tsx` để `next build` qua. Không bấm lại form trên trình duyệt. |

### PostgreSQL

| Bài | Kết quả | Lý do |
|---|---|---|
| `RateScopeMigrationPostgresTests.Ac30_LegacyHeaderScopeIsCopiedWithoutDroppingCardsOrRatings` | FAIL | `NpgsqlException: Failed to connect to 127.0.0.1:5433`. Socket: connection refused. |
| `RateScopeMigrationPostgresTests.Baseline_UpDownUp_KeepsNullChargeCodes_AndHistoricalAmounts` | FAIL | Cùng cổng `127.0.0.1:5433`, cùng lỗi kết nối. |
| Cổng 5432 và 5433 trên máy agent | Đóng | Không có instance Postgres local. CI có Postgres `5432`, nhưng bước `dotnet test` của workflow đang tắt, và hai bài này nối `5433`. |

## Đóng 5 mục

| Mục | Kết quả | File chính |
|---|---|---|
| 1. Transport mode | Ghi mới phải nằm trong danh mục active của đúng tenant và trong bộ mã hệ thống. Danh mục active rỗng thì từ chối. `EXPRESS` không phải mode. `WATER` chỉ bị chặn trên quy tắc giá / bảng giá / import giá. `OCEAN` giữ nguyên chữ, không đổi thành `SEA`; không có trong danh mục thì từ chối. `MULTIMODAL` chỉ trên Shipment và phải có trong danh mục. | `TransportModeCatalog.cs`, các command Order/Bill/Shipment/Leg/Movement/Rate card/Pricing rule/Surcharge/Import/Rating, `CreateTenantCommand.cs` |
| 2. Danh mục | `document_type`, `party_role`, `special_flag` chữ thường. `location` giữ nguyên chữ đã nhập. Các kind còn lại chữ hoa. Seeder không còn gán service `AIR` từ mode `air`. | `CatalogCodePolicy`, `UpsertMasterCatalogItemCommand.cs`, `DemoDataSeeder.cs`, `DemoVolumeCatalogSeeder.cs` |
| 3. Phụ phí | Giữ so sánh theo ngày lịch, biên đóng. Không hoàn nguyên. | `RatingEngine.IsBeforeEffectiveDay`, `SurchargeRating.IsEffective`, test `SurchargeEffectiveDay_IsClosedOnTheCalendarDay` |
| 4. UAT | Health và CI đã đọc. PostgreSQL UAT không kết nối được từ máy này. Không có lệnh UPDATE. | Mục 4 file `01` |
| 5. Test và build | Số liệu ở bảng trên. | `MasterDataFastTrackClosureTests.cs` |

Tenant mới tạo qua API nhận baseline `AIR`, `SEA`, `ROAD`, `RAIL`, `WATER`, `OCEAN`, `MULTIMODAL`. Tenant đã có sẵn không bị chèn thêm dòng.

## GAP còn chờ PO

1. Không gộp hai nguồn Location, không xóa dòng catalog `location`.
2. Không UPDATE mode chữ thường đã lưu, không xóa `stale_at`, không đụng snapshot / expected cost / doanh thu / AP / AR.
3. Dòng `service_type = AIR` đã có trên DB cũ không bị xóa. Seeder mới không tạo thêm.
4. Chưa thêm unique index không phân biệt hoa thường. Đó là migration schema, chưa chạy.
5. PostgreSQL UAT chưa đọc được trong lượt này. Script SELECT nằm ở file `01`.
6. Hai bài Postgres `5433` chưa PASS trên máy agent.
