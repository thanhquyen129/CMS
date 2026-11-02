# AS-IS Root Cause & Technical Implementation

**Commit:** `869e1e7af45c028c4b9b232c1aff1f8902cf6739`

## Root cause

Rating AS-IS coi mọi surcharge Published khớp eligibility là **Applied**. Không có cổng Activation; UI lấy `versions[0]` nên nhầm draft/published; không có `service_selections`.

## After (P0)

1. **Eligibility** (published/effective/direction/scope/conditions)  
2. **Activation** (`MANDATORY_BY_POLICY` / `AUTO_CONDITIONAL` / `OPTIONAL_OPT_IN` / `UNCLASSIFIED_LEGACY`)  
3. **Calculation** chỉ khi Activated  

Buckets ghi vào `Rating.ContextJson`: `appliedCharges` / `eligibleNotActivatedCharges` / `excludedCharges` (+ `unpricedSelectedServices`).

### Code anchors

| Area | File |
|---|---|
| Activation resolve | `src/LCMS.Application/Surcharges/SurchargeActivation.cs` |
| Evaluate pipeline | `src/LCMS.Application/Surcharges/SurchargeRating.cs` |
| Exact version query | `src/LCMS.Application/Surcharges/SurchargeQueries.cs` (`GetSurchargeVersionQuery`) |
| Service selection API | `ServiceSelectionCommands.cs` + `SurchargeEndpoints` `/api/service-selections` |
| UI form / version RO | `apps/web/.../SurchargeForm.tsx`, `surcharges/[id]/versions/[versionId]` |
| Bill selection + 3 nhóm | `ServiceSelectionPanel.tsx`, `RatingSurchargeBuckets.tsx`, `BillRatingPanel.tsx` |

## AC-16 — JSON API samples

Nguồn đầy đủ: `evidence/api_samples.json`. Tóm tắt:

### 1) GET exact v2 (TC-19)

```http
GET /api/surcharges/{id}/versions/{v2Id}
X-Tenant-Id: {tenant}
```

```json
{
  "version": {
    "versionNo": 2,
    "publishStatus": "published",
    "readOnly": true,
    "activationPolicy": "OPTIONAL_OPT_IN",
    "rules": [{ "calculationMode": "fixed_rate", "rateAmountPercent": 300000 }]
  }
}
```

### 2) PUT service-selections (TC-02 / TC-29)

```json
{
  "contextType": "BILL",
  "contextId": "{billId}",
  "serviceCode": "DONGGOI",
  "isSelected": true,
  "quantity": 1,
  "scope": "BILL"
}
```

Response: `revision: 1`, `confirmedAt` set; audit append-only.

### 3) POST /api/ratings — chưa chọn (TC-01)

`eligibleNotActivatedCharges`: `[{ "code": "DONGGOI", "reasonCode": "NOT_SELECTED" }]` — không có dòng DONGGOI trong `rating_details`.

### 4) POST /api/ratings — đã chọn (TC-02)

`appliedCharges` chứa DONGGOI `amount: 300000`, `activationPolicySnapshot: "OPTIONAL_OPT_IN"`.

### 5) POST publish MANDATORY (TC-11 / TC-30)

Thiếu `approval_ref` → **400** `MANDATORY_APPROVAL_REQUIRED`.  
Thiếu quyền → **403**.

## AC-16 — SQL snapshot (Prod `cms-db` 2026-11-02, read-only)

### Inventory (rút gọn)

| Tenant | Code | Dir | Ver | Status | Amount |
|---|---|---|---|---|---|
| ops | DONGGOI_HAN_SGN | sell | 1–3 | published | 100000 |
| ops | DONGGOI_SGN_HAN | sell | 1–3 | published | 400000 |
| ops | DONGGOI_SGN_HAN_MUA | buy | 1–2 | published | 300000 |
| ops | DONGGOI_SGN_HAN_MUA | buy | 3 | **draft** | 300000 |

**Counts:** headers=3, versions=9, published=8. Cột `activation_policy` **chưa có trên Prod** (migration chưa deploy).

### TESTLSS002 trước/sau (bất biến)

```text
bill_no|total_amount|component|amount
TESTLSS002|1740000|FREIGHT|1440000
TESTLSS002|1740000|DONGGOI_SGN_HAN_MUA|300000
```

Sau bật cổng Activation trên **Rating mới**: Bill SGN–HAN chưa chọn đóng gói → DONGGOI không cộng (0₫ phụ phí optional). Snapshot trên không đổi.

## Screenshots

- `screenshots/01_surcharge_v2_published_readonly.png`
- `screenshots/02_surcharge_v3_draft_activation_policy.png`
- `screenshots/03_bill_optional_service_not_selected_rating.png`
- `screenshots/04_bill_optional_service_selected_rating.png`
- `screenshots/05_sell_rating_unpriced_and_history_testlss002.png`

