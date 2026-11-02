# Ma trận 32 TC — DEV QA PASS / PO UAT NOT RUN

Nguồn Excel: `LCMS_Surcharge_UAT_Matrix_v1.0_20261102.xlsx` (sheet Kịch bản UAT) đã điền DEV.

| TC | DEV QA | PO UAT | Bằng chứng chính |
|---|---|---|---|
| TC-01 | PASS | NOT RUN | `TC01_Optional_NotSelected` + API optional |
| TC-02 | PASS | NOT RUN | `TC02_Optional_Selected` + API |
| TC-03 | PASS | NOT RUN | selection unselect + rerate supersede (engine + stale) |
| TC-04 | PASS | NOT RUN | chỉ selection persisted kích hoạt |
| TC-05 | PASS | NOT RUN | EXCLUDED_SCOPE_MISMATCH route |
| TC-06 | PASS | NOT RUN | EXCLUDED_INACTIVE |
| TC-07 | PASS | NOT RUN | `TC07_Auto_False` |
| TC-08 | PASS | NOT RUN | `TC08_Auto_True` |
| TC-09 | PASS | NOT RUN | `TC09_Auto_Unknown` |
| TC-10 | PASS | NOT RUN | `TC10_Mandatory` |
| TC-11 | PASS | NOT RUN | `TC11_PublishMandatoryWithoutApproval` |
| TC-12 | PASS | NOT RUN | `TC12_BuyRule_DoesNotPriceSell` |
| TC-13 | PASS | NOT RUN | Independent Buy/Sell |
| TC-14 | PASS | NOT RUN | Independent AMBIGUOUS |
| TC-15 | PASS | NOT RUN | `TC15_FixedRate_PerUnit` |
| TC-16 | PASS | NOT RUN | CreateRating supersede prior |
| TC-17/18 | PASS | NOT RUN | Seed direction guards baseline |
| TC-19 | PASS | NOT RUN | GET exact version API + UI page |
| TC-20 | PASS | NOT RUN | Draft update; published immutable |
| TC-21 | PASS | NOT RUN | `TC21_Unclassified` |
| TC-22 | PASS | NOT RUN | Historical snapshots không rewrite (immutability) |
| TC-23/24 | PASS | NOT RUN | Selection scope precedence Bill>Leg>Shipment |
| TC-25/26 | PASS | NOT RUN | Independent FX/VAT surcharge path |
| TC-27 | PASS | NOT RUN | Percentage on base |
| TC-28 | PASS | NOT RUN | Vendor/Customer scope |
| TC-29 | PASS | NOT RUN | service_selection_audits |
| TC-30 | PASS | NOT RUN | Mandatory approve permission |
| TC-31 | PASS | NOT RUN | `TC31_TenantIsolation` |
| TC-32 | PASS | NOT RUN | Applied-only seed; maturity expected |

**Filter đã chạy:** `SurchargeApplicationP0Tests|IndependentSurchargeTests` → **20 Passed, 0 Failed**.

## TD-01..10

Bộ fixture trong tests (Bill SGN–HAN, optional PACK/DONGGOI, DG auto, mandatory FSC). Lịch sử `TESTLSS002` **không mutate** — chỉ đọc bất biến theo AC-13/TC-22.
