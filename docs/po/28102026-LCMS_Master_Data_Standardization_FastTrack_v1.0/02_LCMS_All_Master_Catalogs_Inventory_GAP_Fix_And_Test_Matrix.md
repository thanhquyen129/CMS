# Kiểm kê danh mục LCMS

Mỗi dòng: nguồn chuẩn, nơi dùng, chỗ cứng đã thấy, lệch đã biết, quy tắc chuẩn, tác động rating, việc đã sửa.

| Danh mục | Nguồn | Tenant | Quy tắc mã | GAP đã xử lý trong code | Test |
|---|---|---|---|---|---|
| Transport Mode | `master_catalog_items.kind=transport_mode` | tenant | UPPERCASE. WATER/EXPRESS không gộp. MULTIMODAL chỉ Shipment | Bỏ `.ToLowerInvariant()` khi ghi; dropdown lấy catalog; so khớp không phân biệt hoa thường | `MasterDataStandardizationTests`, `ShipmentModePolicyTests` PASS |
| Service Type | `master_catalog_items.kind=service_type` | tenant | UPPERCASE (`FREIGHT`). `AIR` là mã di sản, chặn chọn mới | Ghi hoa khi sửa; nếu tenant chưa có dòng danh mục thì không tự sinh, vẫn nhận mã đã chuẩn hóa | `RateRatingBaselineTests.Uat01` PASS |
| Cost / Revenue type | `master_catalog_items` `cost_type`, `revenue_type` | tenant | UPPERCASE theo upsert catalog | Không đổi quy tắc tiền | Suite rating/cost PASS |
| Pricing component | `kind=pricing_component` | tenant | UPPERCASE | Không hardcode FREIGHT trên form ma trận (đã có từ trước) | `rate-field-policy.test.ts` PASS |
| Document type | `kind=document_type` | tenant | Giữ mã hiện có (`invoice`, `debit_note`) — không ép UPPERCASE | Không sửa | Không đổi hành vi |
| Payment term | `kind=payment_term` | tenant | Mã như `NET30` | Không sửa | Không đổi |
| Transport route (catalog) | `kind=transport_route` và `routes` | tenant | Mã tuyến giữ nguyên hoa/thường đã khóa (`SGN-HAN`) | So khớp route khi tính stale: trim, không phân biệt hoa thường | `OperationalReferenceEditRatingContextTests` PASS |
| Location | `locations` + `kind=location` | tenant | Mã địa điểm giữ chuẩn riêng (IATA/UNLOCODE) | Không ép UPPERCASE hàng loạt | Không migration |
| Currency | `currencies` | global | ISO `VND`, `USD` đã UPPERCASE | Không đổi | Suite tiền tệ không regress |
| VAT | `rate_versions.vat_rate`, surcharge `vat_rate` | theo bảng giá | Số 0/8/10, không phải mã chữ | Không đổi công thức VAT | `ChargeProfitVatAcceptanceTests` PASS |
| Charge / surcharge type | `economic_charge_types` | tenant | UPPERCASE (`FREIGHT`, `PICKUP`) | Phụ phí cùng ngày hiệu lực với ngày áp dụng vẫn tính; không đổi số tiền công thức | `IndependentSurchargeTests` PASS |
| Commodity | `commodity_types` | tenant | Mã loại hàng UPPERCASE khi ghi quy tắc (`GENERAL`) | Không bulk update | Rating commodity tests PASS |
| Party role | `party_roles` / `PartyCatalog` | tenant | mã thường (`customer`, `vendor`) — không ép UPPERCASE | Không sửa | Không đổi |
| Incoterm | context Bill | theo chứng từ | ICC (`FOB`, `CIF`) đã hoa | Dropdown tạo Bill vẫn là danh sách ICC chuẩn, không lấy từ `master_catalog_items` | Ghi vào danh sách xin PO nếu muốn đưa vào catalog |
| Special flags | context hàng | — | `dg`, `reefer`, `oog` chữ thường | Giữ chữ thường. Không đụng DG rating | `IndependentFsc_CombinesWithBase_AndSkipsNonDg` PASS |
| UOM / measurement source | `operational_measurements` | tenant | `kg`, `cbm`, `wm` | CW air/sea đọc mode không phân biệt hoa thường | `ShipmentModePolicyTests.Water_DoesNotUseSeaChargeableWeight` PASS |
| Movement type / equipment | chưa có `kind` riêng | — | — | Không thấy bảng master riêng. Không bịa danh mục | N/A |

File Excel cùng thư mục: `LCMS_Master_Data_Standardization_Matrix_v1.0_20261028.xlsx`.
