# Root Cause, Architecture & EconomicIdentity (Remaster)

**Handover SHA:** `ebc2b27ca08314ecaa69a2bb9bf9d35762222a01` · **PO UAT:** NOT RUN · **G4:** NOT RUN
## Root Cause (Bill LSSTEST02112026002)

1. Seed `SourceId = RatingDetail.Id` → mỗi lần tính = Cost mới cùng FREIGHT.
2. Bill totals SUM mọi dòng `active` → Actual + Expected trùng nghĩa vụ + DONGGOI ×2 = 3.480.000đ.
3. Remaster thêm: không fallback mù về `bill`-only khi thiếu leg/occurrence → `AMBIGUOUS_ECONOMIC_IDENTITY`.

## Thiết kế

### EconomicIdentity

`tenant|bill|direction|charge|occurrence|counterparty` — `LCMS.Domain.Pricing.EconomicIdentity`.

### Ambiguity Guard & Resolve (`BR-DF-006`, TC-11/12)

Khi cùng charge/amount/direction thiếu định danh phân biệt → exception `AMBIGUOUS_ECONOMIC_IDENTITY`.  
`POST /api/ratings/obligations/resolve-exception` (`ResolveAmbiguousObligationCommand`):

- `resolutionAction`: `MERGE_SAME_OBLIGATION` | `SPLIT_DISTINCT_OCCURRENCE` | `SUPERSEDE`
- Bắt buộc `reason`, RBAC, audit actor/timestamp; sau đó EffectiveFactSelector tính lại.

### Apply / Preview / Manual SoT

- `POST /api/ratings/{id}/preview-apply`
- `POST /api/ratings/{id}/apply-financials` + `Idempotency-Key`
- Manual: `service_occurrence_key = manual:{id}` — Apply không đè (TC-33)
- Import/API: cùng `external_id` / source ref → cùng EconomicIdentity (TC-09/34)
- Surcharge: `MaySeedToFinancials` chỉ khi `ApplicationBucket == APPLIED` (TC-18..22)

### JSON mẫu

**CreateRating (tính thử, không seed)**

```json
POST /api/ratings
{ "billId": "...", "partyTypeCode": "vendor", "seedExpectedCosts": false, "seedExpectedRevenues": false }
→ { "id": "...", "status": "draft", "totalAmount": 1740000, "currencyCode": "VND" }
```

**PreviewApplyRating**

```json
POST /api/ratings/{id}/preview-apply
→ {
  "deltaCost": 0, "deltaRevenue": 0, "deltaProfit": 0,
  "warningsVi": ["Nghĩa vụ FREIGHT đã có Thực tế — không ghi đè Actual."],
  "lines": [{ "componentCode": "FREIGHT", "action": "KEEP_ACTUAL_UPDATE_EXPECTED" }]
}
```

**ApplyRatingToFinancials**

```json
POST /api/ratings/{id}/apply-financials
Headers: Idempotency-Key: <uuid>
→ { "applied": true, "supersededExpectedCount": 1, "createdExpectedCount": 0 }
```

**EffectiveFinancialLines / Workspace**

```json
GET /api/bills/{id}/effective-financial-workspace
→ {
  "effectiveCost": 1740000, "effectiveRevenue": 2440000, "bestAvailableProfit": 700000,
  "profitKpis": [
    { "code": "expected", "labelVi": "Lợi nhuận Dự kiến", "amount": 700000 },
    { "code": "best_available", "labelVi": "Lợi nhuận Ước tính tốt nhất", "maturityMix": true },
    { "code": "actual_only", "labelVi": "Lợi nhuận Thực tế", "incomplete": true }
  ]
}
```

**ResolveDuplicateOrAmbiguousObligation**

```json
POST /api/ratings/obligations/resolve-exception
{
  "exceptionId": "...",
  "resolutionAction": "MERGE_SAME_OBLIGATION",
  "reason": "Cùng chặng/vendor — xác nhận một nghĩa vụ",
  "occurrenceOverride": null
}
→ { "resolved": true, "action": "MERGE_SAME_OBLIGATION", "auditEventId": "..." }
```

### Files (I-01…I-14)

| I | Thay đổi |
|---|----------|
| I-01 | CreateRating mặc định không seed |
| I-02 | ApplyRatingToFinancialsCommands |
| I-03 | EconomicIdentity + DB columns |
| I-04 | Seed supersede Expected |
| I-05/06 | EffectiveFactSelector + Profile |
| I-07 | FilterSeedableDetails APPLIED-only surcharge |
| I-08 | Migration 20261103120000 |
| I-09/10 | Workspace API + KPIs |
| I-11…13 | BillRatingPanel A/B, Apply C, Workspace D/E/F |
| I-14 | ResolveAmbiguousObligationCommand |

ADR: `docs/adr/ADR-0043-economic-identity-rating-apply.md`
