# Inventory / Migration / Impact Report

**Commit:** `869e1e7af45c028c4b9b232c1aff1f8902cf6739`  
**Nguồn DB:** Prod Priority-1 `cms-db` (read-only) · 2026-11-02  
**Excel:** `LCMS_Surcharge_Migration_Impact_v1.0_20261102.xlsx`

## 1. Inventory thực tế (MG-01, MG-06, DoD-01)

### SQL đã chạy

```sql
SELECT t.code AS tenant, s.code AS surcharge_code, s.name, s.direction,
       sv.version_no, sv.publish_status, sv.valid_from::text, sv.valid_to::text,
       sr.calculation_mode, sr.basis, sr.currency_code, sr.rate_amount_percent,
       /* conditions + route_scope aggregates */
FROM surcharges s
JOIN tenants t ON t.id = s.tenant_id AND t.deleted_at IS NULL
JOIN surcharge_versions sv ON sv.surcharge_id = s.id AND sv.deleted_at IS NULL
LEFT JOIN surcharge_rules sr ON sr.surcharge_version_id = sv.id AND sr.deleted_at IS NULL
WHERE s.deleted_at IS NULL
ORDER BY t.code, s.code, sv.version_no;
```

**Lưu ý schema Prod:** chưa có cột `activation_policy` / `activation_config_json` / `approval_ref` (migration `20261102170000` chưa apply — MG-18).

### Kết quả (9 dòng)

| Tenant | Code | Tên | BUY/SELL | Ver | Published | Hiệu lực | Applicability | Căn cứ activation hiện có | Policy đề xuất | Review |
|---|---|---|---|---|---|---|---|---|---|---|
| ops | DONGGOI_HAN_SGN | Phí đóng gói/đồng kiểm HAN-SGN | SELL | 1 | published | 2026-10-08→∞ | route:eq:HAN-SGN; fixed 100000 | *(trống — pre-migration)* | OPTIONAL_OPT_IN | Chờ PO/Tenant Admin |
| ops | DONGGOI_HAN_SGN | … | SELL | 2 | published | 2026-10-08→∞ | fixed 100000 | trống | OPTIONAL_OPT_IN | Chờ PO/Tenant Admin |
| ops | DONGGOI_HAN_SGN | … | SELL | 3 | published | 2026-10-12→2026-10-13 | fixed 100000; VAT8 | trống | OPTIONAL_OPT_IN | Chờ PO/Tenant Admin |
| ops | DONGGOI_SGN_HAN | Phí đóng gói/đồng kiểm SGN-HAN | SELL | 1 | published | 2026-10-08→∞ | route SGN-HAN; fixed 400000 | trống | OPTIONAL_OPT_IN | Chờ PO/Tenant Admin |
| ops | DONGGOI_SGN_HAN | … | SELL | 2 | published | 2026-10-08→∞ | fixed 400000 | trống | OPTIONAL_OPT_IN | Chờ PO/Tenant Admin |
| ops | DONGGOI_SGN_HAN | … | SELL | 3 | published | 2026-10-12→∞ | fixed 400000; VAT8 | trống | OPTIONAL_OPT_IN | Chờ PO/Tenant Admin |
| ops | DONGGOI_SGN_HAN_MUA | Phí đóng gói/đồng kiểm SGN-HAN mua | BUY | 1 | published | 2026-10-08→∞ | route SGN-HAN; fixed 300000 | trống | OPTIONAL_OPT_IN | Chờ PO/Tenant Admin |
| ops | DONGGOI_SGN_HAN_MUA | … | BUY | 2 | published | 2026-10-12→∞ | fixed 300000; VAT8 | trống | OPTIONAL_OPT_IN | Chờ PO/Tenant Admin |
| ops | DONGGOI_SGN_HAN_MUA | … | BUY | 3 | **draft** | 2026-10-12→∞ | fixed 300000; VAT8 | Draft UI 3 tầng | OPTIONAL_OPT_IN | Chờ PO/Tenant Admin |

**Định lượng:** `surcharge_headers=3`, `all_versions=9`, `published_versions=8`, `donggoi_versions=9`.

Sau migrate: **8 published** nhận `UNCLASSIFIED_LEGACY` (không tự cộng Rating mới) cho đến khi Tenant Admin duyệt policy → version mới.

## 2. Impact dry-run (MG-07, BR-SUR-024)

| Chỉ số | Trước cutover (AS-IS Prod) | Sau bật Activation (engine P0) |
|---|---|---|
| Published versions | 8 | 8 (immutable) + stamp UNCLASSIFIED_LEGACY |
| Bill SGN–HAN BUY chưa chọn đóng gói | Tự cộng DONGGOI **300.000** | **0** phụ phí optional (`NOT_SELECTED`) |
| Bill SGN–HAN BUY đã lưu DONGGOI qty=1 | 300.000 | 300.000 (`OPTIONAL_OPT_IN`) |
| SELL chọn DONGGOI khi chỉ có BUY rule | Có thể lẫn | **UNPRICED**, không mượn BUY |
| Rating TESTLSS002 | Net **1.740.000** | **1.740.000** (bất biến) — SQL xác nhận |
| Cost/Revenue maturity | expected | expected; không double-count (TC-32) |

## 3. Migration & Rollback

- **Up:** `20261102170000_*` thêm activation columns + `service_selections` / audits; stamp published → `UNCLASSIFIED_LEGACY`.  
- **Down:** drop columns/tables theo migration Down — **không** rewrite `ratings` / `rating_details`.  
- **Cutover:** MG-17 Staging PO UAT → MG-18 Production approve. **Chưa deploy Production trong gói này.**

## 4. Checklist MG-01..18

| MG | Trạng thái | Bằng chứng |
|---|---|---|
| MG-01 | HOÀN TẤT (DEV) | Inventory bảng trên + Excel sheet |
| MG-02 | HOÀN TẤT (DEV) | Tách policy/activation/calculation |
| MG-03 | HOÀN TẤT (DEV) | Đề xuất OPTIONAL — **Chờ Tenant Admin / PO duyệt Mandatory** |
| MG-04..16 | HOÀN TẤT (DEV) | Schema, engine, UI, tests, evidence pack |
| MG-17 | **CHỜ PO PHÊ DUYỆT** | PO UAT Staging |
| MG-18 | **CHỜ PO PHÊ DUYỆT** | Production |

## 5. Rủi ro R-01..08

| R | Kiểm soát | Kiểm chứng |
|---|---|---|
| R-01 cộng thừa | Activation + UNCLASSIFIED block | TC-01,21 |
| R-02 mất phí bắt buộc | Inventory + approval Mandatory | MG-03 chờ PO |
| R-03 sửa lịch sử | Immutability | TC-19,20,22 |
| R-04 đoán policy hàng loạt | Cấm; chỉ UNCLASSIFIED stamp | Migration |
| R-05 BUY→SELL | Direction + UNPRICED | TC-12 |
| R-06 thiếu quyền | RBAC | TC-30 |
| R-07 rollback | Down migration | Mục 3 |
| R-08 Staging vs Prod | MG-17/18 | Chờ PO |

