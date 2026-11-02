# Sổ 34 hạng mục — Root Cause & Resolution (Mục 8 BA / Audit-Grade)

DEV Test = **PASS**. PO UAT = **NOT RUN**.

## Bảng tổng hợp 34 hạng mục

| Mã Issue | Mức | Root cause | Quyết định | File chính | DEV | PO UAT |
|---|---|---|---|---|---|---|
| LCMS-UAT-BUG-20261029-01 | P0 | Context/cargo không persist | Fix | `UpdateBillContextCommand`, `OperationalCargoStore` | PASS | NOT RUN |
| LCMS-UAT-COST-PROJECTION-01 | P0 | 3 nhóm bỏ phân bổ Finalized | Fix | `GetBillFinancialProfileQuery` | PASS | NOT RUN |
| BILL-SERVICE-DISPLAY-01 | P0 | Mode/billType lấp service | Fix | `RatingReadiness`, `revenues/page.tsx` | PASS | NOT RUN |
| COST-RATING-SOURCE-01 | P0 | Trace trống / bịa mã | Fix | `CostQueries` + `RatingTraceSnapshot` | PASS | NOT RUN |
| BILL-SERVICE-MASTER-01 | P0 | AIR service free-text | No Change | UpdateBillContext + catalog | PASS | NOT RUN |
| RATING-SEED-UI-01 | P0 | BUY/SELL seed chiều | No Change | CreateRatingCommand | PASS | NOT RUN |
| RATING-STALE-NORMALIZE-01 | P0 | AIR/air casing | No Change | `RateFieldPolicy.SameMode` | PASS | NOT RUN |
| MASTER-MODE-01 | P1 | Dropdown lệch catalog | No Change | `catalogModeOptions` | PASS | NOT RUN |
| MASTER-SERVICE-01 | P1 | AIR legacy | No Change | Editor + create bill | PASS | NOT RUN |
| COST-RATING-TRACE-UI-01 | P1 | UI thiếu lineage | Fix | `costs/[id]/page.tsx`, CostQueries | PASS | NOT RUN |
| SURCHARGE-FORMULA-TRACE-01 | P1 | FIXED đọc như đ/kg | No Change | costs detail UI | PASS | NOT RUN |
| RATING-VERSION-MATCH-01 | P1 | Weight band | No Change | WeightBand | PASS | NOT RUN |
| RATING-BUY-REASON-01 | P1 | Lý do chung | Fix | BillRateSuggestion | PASS | NOT RUN |
| RATING-RATE-DATE-TRACE-01 | P1 | Nguồn ngày giá | No Change | SuggestBillRatesQuery | PASS | NOT RUN |
| COST-SEED-VAT-DISCLOSURE-01 | P1 | Net/VAT/Gross | No Change | BillRatingPanel | PASS | NOT RUN |
| LCMS-UAT-BUG-20261029-02 | P1 | Compare nuốt service | Fix | CompareRatesQuery | PASS | NOT RUN |
| LCMS-UAT-BUG-20261029-03 | P1 | Chiều tuyến / bidirectional | Fix | RatingEngine + IsBidirectional | PASS | NOT RUN |
| LCMS-UAT-REV-PARTNER-01 | P1 | Picker role | Fix | CreateRevenueForm | PASS | NOT RUN |
| LCMS-UAT-REV-AUDIT-01 | P1 | Confirm không audit | Fix | MaturityTransitionCommands | PASS | NOT RUN |
| LCMS-UAT-APPROVAL-ACTION-01 | P1 | Maker tự duyệt | Fix | DecideApprovalButton | PASS | NOT RUN |
| LCMS-UAT-AR-EXPOSURE-01 | P1 | Basis + ngoại lệ notes/role | Fix | Exposure commands + CreateExposureForm | PASS | NOT RUN |
| LCMS-UAT-AR-RECOGNITION-01 | P1 | Manual AR | No Change | documents page | PASS | NOT RUN |
| LCMS-UAT-AR-IDEMPOTENCY-01 | P1 | Trần revenue | Fix | RecognizeReceivableExposureCommand | PASS | NOT RUN |
| LCMS-UAT-REPORT-DIMENSION-01 | P1 | Báo cáo route | Fix | ProfitabilityBoardQueries | PASS | NOT RUN |
| LCMS-UAT-REPORT-MATURITY-01 | P1 | KPI basis | Fix | revenues/report | PASS | NOT RUN |
| LCMS-UAT-BILL-COMMODITY-01 | P2 | Danh mục trống | Fix | OperationalReferenceEditor | PASS | NOT RUN |
| RATE-DATE-UX-01 | P2 | Locale date | Fix | DateInputVi | PASS | NOT RUN |
| LCMS-UAT-REV-TAXONOMY-01 | P2 | Mã tự do | Fix | CreateRevenueForm | PASS | NOT RUN |
| LCMS-UAT-REV-KPI-01 | P2 | Nhãn KPI | Fix | revenues/page | PASS | NOT RUN |
| LCMS-UAT-REV-SPLIT-01 | P2 | Split conservation | Fix+Test | RevenueMapping + Tc24 | PASS | NOT RUN |
| LCMS-UAT-APPROVAL-HISTORY-01 | P2 | Tab lịch sử | Fix | DEC-04 | PASS | NOT RUN |
| LCMS-UAT-FIN-DOC-TRACE-01 | P2 | 3 chiều chứng từ | Fix | documents/[id] | PASS | NOT RUN |
| LCMS-UAT-FIN-UI-01 | P2 | P&L trộn AR | Fix | DEC-03 | PASS | NOT RUN |
| LCMS-CR-EXPOSURE-VI-01 | P2 | Thuật ngữ | Fix | DEC-02 | PASS | NOT RUN |

---

## Mục 8 — Giải trình chi tiết P0

### P0-1 COST-RATING-SOURCE-01 / COST-RATING-TRACE-UI-01

**Root cause:** `GetCost` join `rating_details`/`rate_cards` thất bại hoặc thiếu snapshot → UI trống; bản cũ từng bịa `TDNT_SGN_HAN`/`QT0001`.

**Before:** khi `card == null` vẫn gán mã mẫu hoặc để trống không parse `ContextJson`.

**After (`CostQueries.cs` + `RatingTraceSnapshot`):**
1. Base rate: đọc card/version/rule; fallback `context_json` (`rateCardCode`, `versionNo`, `rules[].unitAmount`, `chargeable`).
2. Surcharge: `source_type=surcharge` → `Surcharge`/`SurchargeVersion`/`SurchargeRule`; FIXED vs UNIT_RATE; khớp `DONGGOI v2` theo ruleCode.
3. Chỉ khi không có detail **và** không có snapshot hợp lệ → `SourceOrigin=missing_link`, nhãn *"Thiếu dữ liệu liên kết nguồn chi tiết"*.

**API 200 (FREIGHT TESTLSS002 seed):**
```json
{
  "ratingTrace": {
    "rateCardCode": "TDNT_SGN_HAN",
    "rateVersionNo": 2,
    "ruleCode": "QT0001",
    "chargeableWeightKg": 120,
    "unitRate": 12000,
    "isSurcharge": false,
    "sourceOrigin": "rating",
    "formulaText": "120 kg × 12.000 ₫/kg"
  }
}
```

**API 200 (DONGGOI):**
```json
{
  "ratingTrace": {
    "surchargeCode": "DONGGOI",
    "rateVersionNo": 2,
    "isSurcharge": true,
    "calculationMode": "fixed",
    "sourceOrigin": "surcharge",
    "netAmount": 300000
  }
}
```

**Case âm:** cost không gắn `rating_detail` → `sourceOrigin: "missing_link"` (không bịa mã).

**SQL đối chiếu:**
```sql
SELECT c.id, c.cost_type_code, c.amount, rd.rule_code, rd.source_type, r.context_json
FROM costs c
LEFT JOIN rating_details rd ON rd.id = c.source_id
LEFT JOIN ratings r ON r.id = rd.rating_id
WHERE c.bill_id = :bill_testlss002;
```

**Lineage:** `RateCard TDNT_SGN_HAN` → `RateVersion v2` → `PricingRule QT0001` / `Surcharge DONGGOI v2` → `Rating` + `rating_details` → `Cost` → (optional) Financial Document → Exposure → AR.

**Test:** `Cost_rating_trace_reads_freight_base_rate_and_donggoi_surcharge_from_snapshot`.

### P0-2 LCMS-UAT-COST-PROJECTION-01

**Root cause:** UI 3 nhóm chỉ đọc cost `bill_id` trực tiếp, bỏ `CostAllocationDetails` Finalized của shared 240.000.

**After:** `GetBillFinancialProfileQuery` xuất `finalizedAllocations`; cả hồ sơ tài chính và 3 nhóm dùng cùng nguồn → profit 380.000.

**Case âm:** phiên Draft không vào P&L.

### P0-3 LCMS-UAT-BUG-20261029-01 / BILL-SERVICE-DISPLAY-01

**Root cause:** PATCH context không ghi measurement; overview lấy mode làm loại DV.

**After:** `UpdateBillContextCommand` + `OperationalCargoStore`; cột Loại DV / Phương thức tách.

### P0-4 BILL-SERVICE-MASTER-01 / RATING-SEED-UI-01 / RATING-STALE-NORMALIZE-01

**No Change có kiểm chứng:** API reject service AIR mới; BUY/SELL seed chiều; `SameMode(AIR,air)` không sinh stale mới; không DELETE stale lịch sử.

---

## Mục 8 — Giải trình chi tiết P1 (điểm hở đã đóng)

### P1-A LCMS-UAT-AR-EXPOSURE-01

**Root cause:** cho phép tạo exposure thiếu basis; sau đó nới `counterpartyId` không notes để vượt CreditBlock → lệch BA.

**After:**
- Nội bộ: bắt buộc `(Bill + Revenue/Cost)` hoặc `FinancialDocument`.
- Ngoại lệ: `counterpartyId` + `notes` (≥3 ký tự UI) + role hợp lệ (`customer|payer|bill_to` AR; `vendor|payee` AP).
- UI: checkbox ngoại lệ → bắt buộc Đối tác + Căn cứ/Lý do.

**API 200:**
```json
{ "amount": 5000, "currencyCode": "VND", "counterpartyId": "<uuid>", "notes": "Căn cứ ngoại lệ kiểm thử hạn mức tín dụng" }
```

**API 400:** thiếu notes; hoặc đối tác chỉ có role `vendor` khi tạo AR.

**Test:** `Exception_exposure_requires_notes_and_valid_ar_role`; `CreditBlock_RoleGate_SoftDeleteOpenAr_And_Export` PASS.

### P1-B LCMS-UAT-BUG-20261029-03

**Root cause:** compare/match dùng route của card hoặc coi mọi tuyến như nhau.

**After:**
- Mặc định: khớp đúng `Origin→Destination`.
- `IsBidirectional=true`: khớp cặp đảo, **không** khớp cặp khác (`DAD→SGN`).
- `AppliesAnyRoute=true`: khớp mọi cặp.
- Cột DB: `pricing_rules.is_bidirectional` (migration `20261102120000_PricingRuleIsBidirectional`).

**Test:** `Bidirectional_matches_only_the_reverse_pair_and_any_route_matches_other_pairs`.

### P1-C Các P1 còn lại

Compare service (`CompareRatesQuery`), partner picker, audit confirm/actualize, PC-21 maker-checker, AR ceiling, report route_code, KPI maturity — giữ Fix đã ship; bằng chứng suite API + ConsolidatedUat.

---

## Trả lời TV-01 → TV-10

| ID | Kết quả DEV |
|---|---|
| TV-01 | PASS — master mode API/DB + `catalogModeOptions` |
| TV-02 | PASS — service_type master; AIR lọc giao dịch mới |
| TV-03 | PASS — FREIGHT lineage TDNT_SGN_HAN v2 QT0001 CW120×12000 |
| TV-04 | PASS — DONGGOI FIXED 300.000 |
| TV-05 | PASS — missing_link, không bịa mã |
| TV-06 | PASS — WeightBand [min,max) |
| TV-07 | PASS — Directed / Bidirectional / AppliesAnyRoute tách bạch |
| TV-08 | PASS — Exposure notes+role; CreditBlock PASS |
| TV-09 | PASS — Collection ≠ P&L (Tc22/23) |
| TV-10 | PASS — Split conservation Tc24; Postgres CI `LCMS_TEST_POSTGRES` |

## Trả lời BR27-01 → BR27-24

Toàn bộ 24 BR trên sheet `02_QuyTac` chuyển **IMPLEMENTED & VERIFIED (PASS)** — bao gồm BR chiều tuyến, base vs surcharge, missing link, FIXED vs unit rate, exposure basis, P&L độc lập thu tiền, revenue split conservation. Chi tiết từng BR ghi trong Excel cột `Trạng thái DEV`.

## Hiển thị stale lịch sử

Rating `TESTLSS002` nếu còn `stale_reason` dạng `AIR → air` là cờ cũ. Logic mới không sinh thêm và không xóa. PO có thể duyệt dry-run SELECT; DEV không UPDATE hàng loạt.
