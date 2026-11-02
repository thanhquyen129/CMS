# Câu trả lời bàn giao DEV → PO/BA — `LCMS-CR-SURCHARGE-APPLICATION-01`

**Ngày:** 02/11/2026  
**Gói:** Surcharge Application P0 (Eligibility / Activation / Calculation)  
**Trạng thái DEV:** Commercial-ready cho Staging UAT — **chưa đẩy Production**

## Kết luận gửi PO

Đã triển khai cơ chế áp dụng phụ phí 3 tầng trên toàn hệ thống: khớp điều kiện giá chỉ tạo **ứng viên**; chỉ cộng tiền khi **Activation Policy** xác minh TRUE; công thức tính chạy sau bước đó. Legacy Published không có policy được gán `UNCLASSIFIED_LEGACY` và **không tự cộng** ở Rating mới. Published version / Rating snapshot / Expected Cost-Revenue lịch sử **không bị sửa**.

## Root cause AS-IS

1. **Tự cộng phụ phí:** `SurchargeRating.Select` / `SurchargeRatingApplier` cộng mọi published rule khớp scope/condition. Điều kiện trống = không giới hạn phạm vi → bị hiểu nhầm thành “đã phát sinh”.
2. **Không xem được v2 Published:** UI luôn lấy `versions[0]` (mới nhất) và chỉ mở form nháp; không có route đọc đúng `versionId`.

## TO-BE đã ship

- `activation_policy` + `activation_config_json` + `approval_ref` trên `surcharge_versions`
- `service_selections` + audit revision
- Pipeline Evaluate: Applied / Eligible-not-activated / Excluded (+ UNPRICED / unclassified warnings) ghi vào `Rating.ContextJson`
- API `GET /api/surcharges/{id}/versions/{versionId}` read-only đúng version
- UI form chính sách kích hoạt + nút **Xem chi tiết** từng version
- Migration `20261102170000_SurchargeActivationPolicy` (legacy → `UNCLASSIFIED_LEGACY`)

## Bản dựng

| Hạng mục | Giá trị |
|---|---|
| Nhánh | `main` (commit sau bàn giao) |
| Tests | `SurchargeApplicationP0Tests` + `IndependentSurchargeTests` — **20 PASS** (filter focused) |
| DoD-01..05 | Schema+engine+API+UI+tests DEV xong; Production **chưa** deploy |
| MG-17 / MG-18 | **CHỜ PO** |

## Hướng dẫn PO UAT Staging (nhanh)

1. Admin: mở phụ phí → **Xem chi tiết** vPublished (read-only) vs **Chỉnh sửa** draft.
2. Publish `OPTIONAL_OPT_IN` với `service_code` (vd đóng gói) + basis; không chọn dịch vụ trên Bill → Rating BUY **không** cộng; chọn + Lưu → cộng đúng.
3. Publish `MANDATORY_BY_POLICY` thiếu `approval_ref` → bị chặn; user thiếu quyền duyệt Mandatory → 403.
4. `AUTO_CONDITIONAL` với DG unknown → `NEEDS_CONTEXT`, không cộng.
5. Giữ cột **PO UAT = NOT RUN** trong Excel cho đến khi PO tự nghiệm thu.

**Không Production** cho đến khi PO duyệt MG-17/18 và backup.
