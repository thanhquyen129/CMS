# Inventory / Migration / Impact — MG-01..18

## Inventory (MG-01, MG-06)

Cơ chế: mọi `surcharge_versions` Published trước cutover nhận `activation_policy = UNCLASSIFIED_LEGACY` (migration `20261102170000`).  
Policy đề xuất cho PO/Tenant Admin (không tự gán Mandatory hàng loạt):

| Dạng quan sát | Đề xuất |
|---|---|
| Phí dịch vụ chọn được (đóng gói, nâng hạ, kiểm đếm…) | `OPTIONAL_OPT_IN` + `service_code` + basis |
| Phí theo thuộc tính hàng (DG, ngoài giờ…) | `AUTO_CONDITIONAL` + predicate |
| Phí nhiên liệu / chính sách bắt buộc đã có căn cứ | `MANDATORY_BY_POLICY` + `approval_ref` |
| Chưa rõ | Giữ `UNCLASSIFIED_LEGACY` đến khi duyệt |

Xuất inventory runtime: query `surcharges × surcharge_versions × direction` theo tenant trên Staging sau migrate.

## Impact dry-run (MG-07, BR-SUR-024)

Before: mọi published khớp scope đều vào Applied.  
After: UNCLASSIFIED + OPTIONAL chưa chọn + AUTO false/unknown **không cộng**.  
Expected: giảm phụ phí tự cộng thừa trên Rating mới; Rating/Cost/Revenue lịch sử không đổi.

## Checklist MG-01..18

| MG | Trạng thái DEV | Ghi chú |
|---|---|---|
| MG-01..16 | HOÀN TẤT (DEV) | Schema, engine, API, UI, tests, docs |
| MG-17 | CHỜ PO PHÊ DUYỆT | PO UAT Staging |
| MG-18 | CHỜ PO PHÊ DUYỆT | Production + backup — **chưa deploy** |

## Rủi ro R-01..08 (tóm tắt)

| R | Kiểm soát |
|---|---|
| R-01 cộng thừa | Activation gate + UNCLASSIFIED block |
| R-02 mất phí bắt buộc | Inventory + duyệt Mandatory có approval |
| R-03 sửa lịch sử | Immutability published/rating |
| R-04 đoán policy hàng loạt | Cấm; chỉ UNCLASSIFIED |
| R-05 BUY→SELL | Direction + UNPRICED |
| R-06 thiếu quyền | RBAC Mandatory / ServiceSelect |
| R-07 rollback | Flag/policy version mới; down migration có sẵn |
| R-08 Staging vs Prod | MG-17/18 chờ PO |

Excel `LCMS_Surcharge_Migration_Impact_v1.0_20261102.xlsx` đã cập nhật trạng thái DEV.
