# Ma trận danh mục LCMS — 6 chiều

Số liệu lệch lấy từ `src/LCMS.Api/lcms_dev.db` (read-only, 2026-10-28), tenant `ops`. Excel cùng thư mục có sheet `Matrix` và `DbInventory`.

Cơ chế catalog trống của Transport Mode nằm ở mục 1 và được nhắc lại ở file `01`.

## 1. Transport Mode

1. Nguồn: `master_catalog_items` với `kind = transport_mode`, phạm vi tenant (`TenantEntityBase`). Bảng giao dịch lưu chuỗi, không có FK: `orders.transport_mode`, `bills.transport_mode`, `shipments.transport_mode`, `transport_legs.transport_mode`, `transport_movements.transport_mode`, `rate_cards.transport_mode`, `pricing_rules.transport_mode`, `routes.transport_mode_code`, `surcharge_conditions.value_text` khi `dimension = transport_mode`, `ratings.context_json` khóa `transportMode`.
2. API: `GET /api/catalog?kind=transport_mode` (`ListMasterCatalogItemsQuery`), `POST /api/catalog` (`src/LCMS.Application/Catalog/Commands/UpsertMasterCatalogItemCommand.cs` ép `Code` bằng `ToUpperInvariant`). Ghi nghiệp vụ: `UpsertOrderCommand`, `UpdateBillContextCommand`, `EditOperationalReferenceCommand`, `UpsertShipmentCommand`, `UpsertTransportLegCommand`, `UpsertTransportMovementCommand`, `RateCardCommands`, `SaveRateCardDeclarationCommand`, `ComposeTariffCommand`, `PricingRuleCommands`, `SurchargeCommands.AddCondition`, `ImportOperationalBatchCommand`, `CreateRatingCommand`. UI: `apps/web/app/admin/catalog/page.tsx`, `apps/web/app/operations/legs/new/page.tsx`, `apps/web/app/rate-cards/new/page.tsx`, `apps/web/app/rate-cards/[id]/edit/page.tsx`, `apps/web/app/rate-cards/page.tsx`, `apps/web/app/rate-cards/rate/page.tsx`, `apps/web/app/rate-cards/compare/page.tsx`, `apps/web/app/rate-cards/surcharges/page.tsx`, `apps/web/app/rate-cards/surcharges/new/page.tsx`, `apps/web/app/rate-cards/surcharges/[id]/page.tsx`, `apps/web/components/RateCalculatorForm.tsx`, `CompareRatesForm.tsx`, `SurchargeForm.tsx`, `OperationalReferenceEditor.tsx`, `RateCardDeclarationForm.tsx`.
3. Hardcode đã gỡ: mảng `TRANSPORT_MODES` trong `apps/web/lib/create-workspace.ts`, option `value="air"` trên các form ở mục 2, fallback `canonicalModes` trong `apps/web/lib/rate-field-policy.ts`. Seed còn chữ thường ở dữ liệu đã sinh: `src/LCMS.Application/Demo/DemoVolumeCatalogSeeder.cs` mảng mode `air/sea/road/rail`. Seed danh mục cho tenant mới trong `DemoDataSeeder.EnsureDefaultCatalogAsync` ghi `AIR/SEA/ROAD/RAIL` và bỏ qua nếu đã có mã cùng kind (so không phân biệt hoa thường). Import: `ImportOperationalBatchCommand`, `ImportRateCardsCommand` (`ToStoredMode`).
4. DB local: catalog chỉ `air` × 1, active. `orders` 30/30/30/30 cho `air/sea/road/rail` (120). `bills` 32 `air`, 30 `sea`, 30 `road`, 30 `rail`, 10 null (132). `shipments` 30 mỗi mã (120). `transport_legs` 120 null. `transport_movements` 120 null. `rate_cards` 121 null, 2 `air`, 1 `sea`. Ba bảng giá có chữ thường và đã published: `NSE-AIR-VN-MY` (`air`), `NSE-SEA-VN-MY` (`sea`), `TDNT_SGN_HAN` (`air`, 2 version published). `pricing_rules` 126 null, 3 `air`, cả 3 nằm trên version `published`. `routes` 1 dòng `SGN-HAN` mode `air` service `FREIGHT`. `surcharge_conditions` 0 dòng `transport_mode` (4 dòng `destination`: SBH 2, SWK 2). `ratings.context_json.transportMode` = `air` trên cả 16 rating. Không có mã `AIR` hoa trong các cột này. Service Type `AIR` trong catalog là mã khác, không phải transport mode.
5. Validation: `TransportModeCatalog.ResolveAsync`. Có dòng active thì mã phải `SameMode`. Không có dòng thì không insert catalog; chặn độ dài, chặn `MULTIMODAL` ngoài Shipment; `NewModeBlock` chặn `WATER`/`EXPRESS` trên quy tắc giá mới. Normalize: trim + `ToUpperInvariant` cho ghi mới. Map đọc: `SameMode` và `RatingEngine.ModeMatches`. `OCEAN` thuộc họ `SEA` khi tính W/M, không đổi chữ lưu.
6. Rating: match rule và surcharge không gãy vì khác hoa thường. Stale không bật khi chỉ khác cách viết. Snapshot cũ giữ `air`. Đổi `AIR`→`SEA` vẫn stale. Bảng giá published không bị viết lại chữ `air` khi lưu lại cùng mã. Ảnh hưởng lịch sử: 122 bill, 120 order, 120 shipment, 3 rate card, 3 pricing rule, 1 route, 1 catalog, 16 context JSON đang chữ thường. Không UPDATE trong lượt code.
7. GAP đã sửa: bỏ ép chữ thường trên command; dropdown lấy catalog; so sánh semantic. Chưa sửa: bulk uppercase DB, unique index citext. Test: `MasterDataStandardizationTests.HistoricalAir_SavedAsAir_DoesNotStale_SeaAndWeightStillDo_SnapshotStays`, `PublishedRateCard_KeepsStoredModeBytes_WhenTheSameModeIsSentInAnotherCase`, `ShipmentModePolicyTests`, `RateRatingBaselineTests.Uat02_mode_adapter_is_bidirectional_and_does_not_collapse_water_or_express`. PASS.

## 2. Service Type

1. Nguồn: `master_catalog_items.kind = service_type`, tenant. Cột: `bills.service_type_code`, `orders.service_type_code`, `shipments.service_type_code`, `transport_legs.service_type_code`, `routes.service_type_code`, `pricing_rules.service_type_code`.
2. API: cùng catalog endpoint. Ghi: `EditOperationalReferenceCommand`, `UpdateBillContextCommand`, `RouteCommands` (từ chối service không active). UI: `apps/web/lib/create-form-options.ts` (bỏ mã `AIR` khỏi lựa chọn mới), `OperationalReferenceEditor.tsx` nhánh `service_type`, `RateCardDeclarationForm.tsx` qua `selectableServices`.
3. Seed: `DemoDataSeeder.EnsureDefaultCatalogAsync` tạo `FREIGHT` và `AIR`. `DemoVolumeCatalogSeeder` gán service `AIR` khi mode là air (lẫn service với mode). Không có importer riêng ngoài operational import (import đó không ghi service type trong row hiện tại).
4. Catalog: `FREIGHT` active 1, `AIR` active 1 (mã di sản). Bill: 130 null, 1 `FREIGHT` (`TESTLSS002`), 1 `AIR`. Rating context: 14 `FREIGHT`, 1 `freight`, 1 `master` (hai mã sau không thuộc TESTLSS002). Không trùng theo unique `(tenant, kind, code)` vì chỉ một dòng mỗi mã.
5. `EditOperationalReferenceCommand`: nếu tenant có service active thì mã phải khớp, không phân biệt hoa thường; ghi `ToUpperInvariant`. Mã `AIR` bị chặn khi Bill hiện tại không phải `AIR` (`RateFieldPolicy.ServiceBlock` / `IsLegacyServiceAir`). Catalog trống thì không insert dòng, vẫn nhận mã đã hoa. Không dùng `ToUpper` cho document type.
6. Rating `Matches` so service bằng `Eq` không phân biệt hoa thường. Stale service chỉ khi mã canonical khác và rule có `service_type_code`. Đổi `freight`→`FREIGHT` không stale. Lịch sử `freight` và `master` trong context không bị sửa.
7. GAP đã sửa: so sánh ignore case, ghi hoa khi sửa field, UI không đưa `AIR` vào lựa chọn mới. Chưa sửa seeder volume (file `04`). Test: `RateRatingBaselineTests.Uat01_service_air_is_not_a_mode_and_stays_readable`, `apps/web/lib/rate-field-policy.test.ts` ("hides AIR from new service choices"). PASS.

## 3. Cost type

1. `master_catalog_items.kind = cost_type`, tenant. Dùng trên dòng giá mua và seed expected cost, không phải transport mode.
2. API catalog. UI `apps/web/app/rate-cards/new/page.tsx` và `apps/web/app/rate-cards/[id]/edit/page.tsx` gọi `listCatalog("cost_type")`. Lệnh phát hành: `RateFieldPolicy.PublishBlock` yêu cầu `CostTypeCode` trên giá mua.
3. Seed `DemoDataSeeder`: `FREIGHT`, `AP-FREIGHT`, `FUEL`. Không hardcode dropdown cost type trên các màn đã chuyển sang catalog.
4. DB: ba mã, mỗi mã 1, active. Không thấy chữ thường. `costs` của TESTLSS002 dùng `FREIGHT` và `DONGGOI_SGN_HAN_MUA` (mã cost trên dòng chi phí, không nằm trong ba mã catalog).
5. Upsert catalog ép UPPERCASE. Publish từ chối dòng mua thiếu loại chi phí. Không normalize hàng loạt cost đã có.
6. Không đổi số tiền cost. Expected cost seed vẫn đi từ rating detail. Lịch sử cost không UPDATE.
7. Không có GAP code mới trong lượt này ngoài việc không đụng cost. Test hồi quy suite cost trong 579 PASS, gồm `Sprint4CostTests`.

## 4. Revenue type

1. `kind = revenue_type`, tenant.
2. Catalog API. UI rate card new/edit `listCatalog("revenue_type")`. `PublishBlock` yêu cầu `RevenueTypeCode` trên giá bán.
3. Seed: `FREIGHT-REV`, `AR-FREIGHT`.
4. DB: hai mã, mỗi mã 1, active.
5. Upsert UPPERCASE. Không ép revenue amount.
6. Seed expected revenue từ rating bán. Lượt này không sửa công thức doanh thu.
7. Không GAP mới. Test: `RatingModeTests.SellWeightBreak_SeedsExpectedRevenue_WithoutAnExtraComponent` PASS (seed cost từ rating bán trả 400, đúng guard BUY/SELL).

## 5. Pricing component

1. `kind = pricing_component`, tenant. Nhóm tính giá suy từ khoản giá, không nhập tay trên UI ma trận.
2. Catalog API. UI `RateCardDeclarationForm.tsx` chọn khoản giá từ `economic_charge_types`, không ô mã nhóm tự do.
3. Seed: `FREIGHT`. Không fallback sinh `FREIGHT` khi dòng trống (`RateFieldPolicy.ChargeRequired`).
4. DB: 1 mã `FREIGHT` active.
5. `ChargeCode` trên rule mới `ToUpperInvariant` trong `PricingRuleCommands` và `SaveRateCardDeclarationCommand`. Publish thiếu charge thì `PublishChargeMissing`.
6. Rating group theo charge code hoa. Không đổi amount của rule đã published.
7. GAP hardcode FREIGHT trên advanced matrix đã đóng trước lượt này. Test: `apps/web/lib/rate-field-policy.test.ts` UAT-13/16 và UAT advanced trong `RateCardDeclarationForm.test.tsx`. PASS.

## 6. Document type

1. `kind = document_type`, tenant. Chứng từ tài chính, không phải mode.
2. Catalog API. Màn chứng từ dùng kind này khi tạo document. Lệnh nhận chứng từ: `ReceiveFinancialDocumentCommand` (lowercase cho `documentType` vì mã lưu `invoice`, không phải mã UPPERCASE).
3. Seed: `invoice`, `debit_note`. Không có dropdown hardcode transport ở đây.
4. DB: `invoice` 1, `debit_note` 1, active. Đúng quy ước chữ thường của loại chứng từ.
5. Không chạy `ToUpperInvariant` cho document type trong lượt này. Upsert catalog vẫn hoa mọi code mới nếu user gõ hoa — điểm cần PO nếu muốn giữ `invoice` chữ thường (file `04` không bắt buộc đổi vì dữ liệu hiện đúng chữ thường và không có ghi mới trong lượt này).
6. Không ảnh hưởng rating BUY/SELL.
7. Không sửa code. Test hồi quy chứng từ nằm trong 579 PASS.

## 7. Payment term

1. `kind = payment_term`, tenant.
2. Catalog API. Dùng trên điều khoản đối tác, không trên rating engine.
3. Seed: `NET15`, `NET30`.
4. DB: mỗi mã 1, active. Không trùng, không chữ thường lệch.
5. Upsert catalog UPPERCASE. Không có normalize riêng trên hóa đơn trong lượt này.
6. Không đổi công nợ AP/AR.
7. Không GAP code. Không test riêng; không regress suite tài chính.

## 8. Route

1. Hai lớp: `routes` (id, code, origin, destination, `transport_mode_code`, `service_type_code`) và `master_catalog_items.kind = transport_route`. Cả hai tenant.
2. API: `src/LCMS.Application/ReferenceMasters/RouteCommands.cs` (`UpsertRouteCommand`). UI tạo Bill/Order lấy `listRoutes` trong `apps/web/lib/create-form-options.ts`. Rating: `RatingEngine.RouteMatches`.
3. Seed volume tạo mã `VOL-RTE-0001` … `VOL-RTE-0120` trong catalog kind `transport_route` (120 dòng, mỗi mã 1). Bảng `routes` chỉ 1 dòng nghiệp vụ `SGN-HAN`. Không importer tuyến riêng trong lượt này.
4. `routes.transport_mode_code = air` (chữ thường) trên `SGN-HAN`, service `FREIGHT`. 120 mã catalog là mã kỹ thuật seed, active, không trùng. Không có hai dòng cùng code.
5. `RouteCommands` ghi `transport_mode_code` qua `ToStoredMode` chỉ khi mã mới khác `SameMode`, nên lưu lại cùng mode không viết đè chữ cũ. Service trên tuyến phải có trong catalog active. Stale route: trim + ignore case trong `UpdateBillContextCommand.SameRatingInput`.
6. Đổi mã tuyến thật (không chỉ hoa thường) vẫn stale nếu rule dùng route. `RouteModeConflict` khi mode rule khác mode tuyến và không chọn mọi tuyến. Published rate không bị sửa route trong lượt này.
7. GAP đã sửa: hết `.ToLowerInvariant()` trên `RouteCommands` cho mode; so khớp stale. Chưa bulk `air`→`AIR` trên `routes`. Test: `RateRatingBaselineTests.Uat04_route_mode_conflict_and_any_route`, `OperationalReferenceEditRatingContextTests.ContextChange_MarksOnlyUsedRatingsStale_KeepsAmounts_AndRerateAddsHistory` (đổi route khi rule không dùng route thì không stale). PASS.

## 9. Location

1. Bảng `locations` (tenant) là nguồn địa điểm vận hành. `master_catalog_items.kind = location` là lớp seed song song, không FK sang `locations`.
2. API: `src/LCMS.Application/ReferenceMasters` location commands / `listLocations`. UI `create-form-options.ts` `listLocations(true)`. Bill bind: `UpdateBillContextCommand.BindPlacesAsync`.
3. Seed volume: 120 mã `VOL-LOC-0001` … `0120` trong catalog kind `location`. Bảng `locations` chỉ `SGN` (Tân Sơn Nhất, airport, VN) và `HAN` (Nội Bài, airport, VN).
4. Không trùng code trong `locations`. Catalog location là mã giả lập, không trùng `SGN`/`HAN`. Không có `AIR`/`air` ở cột location.
5. Mã địa điểm giữ dạng đã nhập (IATA). Không ép UPPERCASE hàng loạt trong lượt này. Bind điểm đi/đến map sang `location_id`.
6. Origin/destination trên rating so ignore case khi tính stale. Không sửa tọa độ, không sửa snapshot.
7. GAP: hai nguồn (catalog kind `location` và bảng `locations`) chưa gộp. Để PO, file `04`. Test hồi quy prefill/route trong `InputOptimizationInheritanceTests` PASS.

## 10. Currency

1. Bảng `currencies`, không có `tenant_id` (global). 15 mã ISO active: AUD, CAD, CHF, CNY, EUR, GBP, HKD, JPY, KRW, MYR, SGD, THB, TWD, USD, VND. Mỗi mã 1.
2. API `listCurrencies`. UI `create-form-options.ts`. Rate card: `RateCardCatalog.EnsureCurrencyAsync` trong `RateCardCommands` / `SaveRateCardDeclarationCommand`.
3. Seed: `CurrencyCatalogSeeder.EnsureBaselineAsync` gọi từ `LcmsApiFactory.InitializeDatabaseAsync` và startup. Không hardcode dropdown VND/USD trên form bảng giá đã nối catalog.
4. Toàn bộ mã đã hoa, active, không trùng. Không có `vnd` thường.
5. Mọi ghi `CurrencyCode` trên card, rule, surcharge dùng `ToUpperInvariant` và phải có trong `currencies`. Đây là quy ước ISO, không dùng rule transport mode.
6. FX và làm tròn VND 0 số thập phân nằm ở `RatingEngine.RoundCurrency`. Lượt này không đổi tỷ giá hay số tiền đã lưu.
7. Không GAP hoa/thường. Test suite tiền tệ trong 579 PASS.

## 11. VAT

1. Không có bảng `tax_rates`. VAT là số trên `rate_versions.vat_rate` và `surcharge_versions.vat_rate`, theo từng version của tenant.
2. API khai báo bảng giá và phụ phí (`SaveRateCardDeclarationCommand`, `SurchargeCommands`). UI form bảng giá và `SurchargeForm.tsx` ô VAT.
3. Không seed danh mục 0/8/10. Không hardcode thuế vào transport mode.
4. Không thống kê từng mức % trong lượt này vì không có bảng mã thuế. Rating detail của TESTLSS002 không bị đọc để sửa VAT.
5. Không normalize chữ. Số VAT giữ nguyên trên version published.
6. Công thức VAT không đổi. Test `ChargeProfitVatAcceptanceTests.Packaging_loss_uses_net_and_published_vat_stays_put` và `Component_charge_type_wins_and_legacy_surcharge_is_excluded_once` PASS sau khi phụ phí cùng ngày lịch vẫn được áp.
7. GAP: chưa có master VAT 0/8/10. Đề xuất file `04` nếu PO muốn danh mục thuế riêng. Không tự tạo bảng.

## 12. Economic charge / surcharge type

1. `economic_charge_types`, tenant. Mã: DELIVERY, FREIGHT, FUEL, HANDLING, PICKUP, REMOTE. Mỗi mã 1, active.
2. API `EconomicChargeCommands`, `GET` loại khoản. UI `apps/web/app/rate-cards/surcharges/page.tsx` lọc theo `economicChargeTypeCode`, `SurchargeForm.tsx` chọn `economicChargeTypeId`.
3. Seed `EconomicChargeCatalogSeeder` / `DemoDataSeeder`. Phụ phí không còn khai trên dòng pricing rule (`RateFieldPolicy.SurchargeRedirect`).
4. Surcharge version published: 6. Điều kiện: không có `transport_mode`; có `destination` SBH 2 và SWK 2. Không mã charge chữ thường.
5. `ChargeCode` UPPERCASE. Điều kiện mode mới lưu UPPERCASE trong `SurchargeCommands.AddCondition`. Khớp bằng `SurchargeRating.ConditionMatches` (`OrdinalIgnoreCase`). Hiệu lực theo ngày lịch (`RatingEngine.IsBeforeEffectiveDay` / `IsAfterEffectiveDay`), không theo mili-giây trong cùng ngày.
6. BUY/SELL phụ phí giữ chiều. Không sửa amount đã publish. Cùng ngày phát hành phụ phí và ngày áp dụng của Bill vẫn tính phụ phí (trước đó `ValidFrom <= rateDate` loại phụ phí tạo sau Bill vài giây).
7. GAP đã sửa: ngày hiệu lực theo ngày; mode condition hoa. Test: `IndependentSurchargeTests.IndependentFsc_CombinesWithBase_AndSkipsNonDg`, `Dedup_PicksTheMoreSpecificRule_AndAmbiguityStoresNothing`, `BuyAndSell_WriteExpectedCostAndRevenue`, `OptionalRateCardScope_AppliesOnlyToThatCard`, `PercentageContainerAndFx_StoreTheLineSnapshot`. PASS.

## 13. Commodity

1. `commodity_types`, tenant. Năm mã active, không DG: COSMETICS, COSMETICS_FOOD, DRY_FOOD, EXPRESS, GENERAL.
2. API reference masters `listCommodities`. UI `create-form-options.ts`. Rule: `PricingRuleCommands` ghi `CommodityCode` hoa. Rating `Matches` so commodity ignore case.
3. Seed demo hàng. `EXPRESS` ở đây là loại hàng, không phải transport mode `EXPRESS` bị chặn trên quy tắc giá. Không gộp hai mã.
4. Không trùng code. Không chữ thường. Rating context TESTLSS002 không mang commodity lệch.
5. Không bulk rename. `EXPRESS` commodity giữ nguyên.
6. Đổi `commodity_type_id` trên Bill vẫn có thể stale nếu rule có commodity. Không sửa 5 dòng.
7. Không GAP hoa/thường. Test rating commodity trong suite 579 PASS (`BuyRatingLegTests.LegSuggestion_UsesLegCommodity_NotTheBill`).

## 14. Party role

1. `party_roles.role_code`, tenant, gắn `party_id`. Quy ước chữ thường: `customer`, `vendor`, `payer`, `payee`.
2. API `PartyRoleCommands`, `BusinessPartyCommands`. UI đối tác và typeahead `PartyTypeahead.tsx`. Rating chiều lấy `party_type` `vendor`/`customer`, không lấy từ transport mode.
3. Không seed role bằng UPPERCASE. `PartyCatalog` so mã sau `ToLowerInvariant` — đúng quy ước role, không áp cho transport mode.
4. DB: 484 dòng, mỗi role 121, `is_active = 1`. Không mã hoa lệch.
5. Giữ chữ thường. Không chạy `ToUpperInvariant`.
6. Đổi `customer_party_id` / `vendor_party_id` trên Bill nằm trong `RatingStalenessService` (field `customer_party_id`, `vendor_party_id`) và làm stale rating hiện tại. Không sửa party đã có.
7. Không GAP. Test đối tác trong 579 PASS.

## 15. Incoterm

1. Không có `kind` và không có bảng incoterm. Giá trị nằm `bills.incoterm_code` / `orders.incoterm_code`.
2. UI tạo Bill: `apps/web/lib/create-workspace.ts` hằng `INCOTERMS` (EXW, FCA, FOB, CIF, CFR, DAP, DDP). Không API catalog.
3. Hardcode ICC cố ý. Không seed DB. Không importer.
4. `bills.incoterm_code`: 132 null. Không mã lệch.
5. Mã ICC đã hoa trên UI. Không normalize server trong lượt này. Không đụng transport mode.
6. Không tham gia `RatingEngine.Matches`. Không stale.
7. GAP: chưa catalog hóa. File `04`. Test không có hàm riêng. Hằng UI không đổi nên không regress.

## 16. Special flags (DG / lạnh / quá khổ)

1. Không bảng master. Cờ trên context hàng, chuỗi `dg`, `reefer`, `oog`, `other`.
2. UI `SPECIAL_FLAGS` trong `apps/web/lib/create-workspace.ts`. Editor: `EditOperationalReferenceCommand` nhánh `Flags` giữ `ToLowerInvariant`. Rating phụ phí DG: `SurchargeRating` dimension `dangerous_goods`.
3. Hardcode bốn cờ. Không seed bảng. Không import riêng.
4. Không đếm được bằng `COUNT` trên một cột mã vì không có bảng. Commodity `is_dangerous_goods = 0` trên cả 5 loại hàng.
5. Giữ chữ thường. Không UPPERCASE. Tách khỏi transport mode.
6. Phụ phí DG không áp cho hàng không DG (`IndependentFsc_CombinesWithBase_AndSkipsNonDg`). Không sửa cờ lịch sử.
7. Không GAP với mode. Test hàm trên PASS.

## 17. UOM / measurement source

1. `operational_measurements` (quantity, uom, measure code, source channel), tenant. UOM hệ thống: `kg`, `cbm`, `wm`/`W/M`, `pcs`.
2. API đo lường trong `EditOperationalReferenceCommand` và `ChargeableWeightPolicy`. UI editor vận hành, nhãn basis trong `apps/web/lib/opref-edit.ts`.
3. Không catalog UOM. `ChargeableWeightPolicy.UomFor` chọn `W/M` khi `ShipmentModePolicy.IsSeaFamily`, còn lại `kg`. Đọc được `air` và `AIR`.
4. Không có mã UOM hoa lệch cần đếm trong lượt này. Mode null trên 120 leg nên CW chặng không suy được từ mode riêng.
5. `IsSea` / `IsAir` không còn `ToLowerInvariant`; dùng `Normalize`. WATER không dùng W/M.
6. Đổi gross/volume/CW vẫn stale khi basis thuộc `measured`, `confirmed`, `air_volumetric`, `sea_wm`. Không ghi đè measurement lịch sử khi chỉ đổi cách viết mode.
7. GAP đã sửa: CW nhận mode hoa. Test: `ShipmentModePolicyTests.Water_DoesNotUseSeaChargeableWeight`, `MasterDataStandardizationTests` (gross 85 → 120 vẫn stale, amount và snapshot giữ). PASS.
