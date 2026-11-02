# UAT 32 scenarios — DEV QA PASS / PO UAT NOT RUN

**Commit:** `869e1e7af45c028c4b9b232c1aff1f8902cf6739`  
**Excel:** `LCMS_Surcharge_UAT_Matrix_v1.0_20261102.xlsx`  
**Filter:** `dotnet test --filter FullyQualifiedName~SurchargeApplicationP0Tests` → **PASS**

## Ma trận TC-01..32

| TC | Kết quả thực tế chi tiết | Hàm xUnit | TD |
|---|---|---|---|
| TC-01 | BUY: applied=[FREIGHT]; eligibleNotActivated=[DONGGOI:NOT_SELECTED] | `TC01_Optional_NotSelected_GoesToEligibleBucket` | TD-01 |
| TC-02 | DONGGOI applied 300000; OPTIONAL_OPT_IN | `TC02_Optional_Selected_AppliesFixedAmount` | TD-02 |
| TC-03 | Unselect + rerate: old snapshot immutable; new without DONGGOI | `TC03_Optional_SelectThenUnselect_RerateSupersedesAndKeepsOldSnapshotImmutable` | TD-02 |
| TC-04 | UI tick chưa PUT → không activate | `TC04_Optional_UnpersistedUiPreview_DoesNotActivateOrSeed` | TD-01 |
| TC-05 | Route mismatch → EXCLUDED_SCOPE_MISMATCH | `TC05_Optional_SelectedButRouteMismatch_ExcludedScopeMismatch` | TD-03 |
| TC-06 | Ngoài hiệu lực → EXCLUDED_INACTIVE | `TC06_Optional_SelectedButOutsideEffectiveDates_ExcludedInactive` | TD-03 |
| TC-07 | DG false → NOT_ACTIVATED_CONDITION_FALSE | `TC07_AutoConditional_DangerousGoodsFalse_EligibleNotActivated` | TD-04 |
| TC-08 | DG true → applied | `TC08_AutoConditional_DangerousGoodsTrue_Applies` | TD-04 |
| TC-09 | DG null → NEEDS_CONTEXT | `TC09_AutoConditional_MissingDg_NeedsContextOrDataUnknown` | TD-04 |
| TC-10 | MANDATORY applies w/o selection | `TC10_Mandatory_AppliesWithoutSelection_WhenPolicyApproved` | TD-05 |
| TC-11 | Publish MANDATORY thiếu approval → 400 | `TC11_PublishMandatory_WithoutApprovalRef_Rejected` | TD-05 |
| TC-12 | SELL không mượn BUY; UNPRICED | `TC12_BuyRule_DoesNotPriceSellSelection` | TD-06 |
| TC-13 | BUY/SELL independent amounts | `TC13_BuyAndSell_IndependentRulesAndAmounts` | TD-06 |
| TC-14 | Double match dedup/ambiguous | `TC14_DoubleMatch_DeduplicatesByPriorityOrReturnsAmbiguous` | TD-07 |
| TC-15 | 300k×2 PER_UNIT=600k; PER_BILL=300k | `TC15_FixedRate_PerUnit_MultipliesQuantity` | TD-02 |
| TC-16 | Rerate new linked rating | `TC16_RerateOnSelectionChange_CreatesNewLinkedRatingWithoutOverwriting` | TD-02 |
| TC-17 | Seed BUY applied-only idempotent | `TC17_SeedBuy_AppliedOnly_Idempotent_NoRevenueCreated` | TD-08 |
| TC-18 | Seed SELL applied-only idempotent | `TC18_SeedSell_AppliedOnly_Idempotent_NoCostCreated` | TD-08 |
| TC-19 | GET v2 readOnly published | `TC19_GetExactPublishedVersionV2_ReturnsReadOnlySnapshot` | TD-09 |
| TC-20 | Draft v3; v2 immutable | `TC20_UpdateDraftV3_KeepsPublishedV2Immutable` | TD-09 |
| TC-21 | UNCLASSIFIED excluded | `TC21_UnclassifiedLegacy_BlockedFromNewAutoApply` | TD-10 |
| TC-22 | TESTLSS002 1.740.000 giữ nguyên | `TC22_History_TestLss002_Preserves1740000Net_1440000Freight_300000Donggoi` | TD-10 |
| TC-23 | per_shipment không nhân mỗi bill | `TC23_Scope_ShipmentWithMultipleBills_AllocatesWithoutMultiplyingFullFeePerBill` | TD-03 |
| TC-24 | LEG+BILL không double count | `TC24_Scope_LegAndBillBothEligible_AppliesCorrectEconomicScopeWithoutDoubleCount` | TD-03 |
| TC-25 | FX/original currency snapshot | `TC25_Currency_ForeignCurrencySurcharge_AppliesFxSnapshotCorrectly` | TD-07 |
| TC-26 | VAT Net/Vat/Gross | `TC26_Vat_SurchargeVatCalculatedCorrectly_NetVatGross` | TD-07 |
| TC-27 | % base no stack loop | `TC27_Percentage_CalculatesOnEligibleBaseOnly_NoStackingLoop` | TD-07 |
| TC-28 | Vendor party match only | `TC28_Party_MatchesSpecificBuyVendorOrSellCustomerOnly` | TD-07 |
| TC-29 | Audit actor/qty/revision | `TC29_Audit_ServiceSelectionChanges_RecordActorTimestampQuantityAndRevision` | TD-02 |
| TC-30 | RBAC/approval block publish | `TC30_Rbac_UserWithoutMandatoryApprovePermission_Blocked403OnPublishMandatory` | TD-05 |
| TC-31 | Tenant isolation | `TC31_TenantIsolation_SelectionsDoNotLeak` | TD-01 |
| TC-32 | Cost/revenue maturity no double | `TC32_Regression_CostRevenueProfitReport_NoDoubleCountAndPreservesMaturity` | TD-08 |

## Log chạy (tóm tắt)

```text
dotnet test --filter FullyQualifiedName~SurchargeApplicationP0Tests
Passed! Failed: 0, Passed: 33, Total: 33

npm test -- --run
Test Files 7 passed | Tests 47 passed

npx tsc --noEmit → exit 0
```

API helper thêm: `TC01_TC02_Api_OptionalSelection_AndBucketsInContextJson` (bằng chứng ContextJson end-to-end).

