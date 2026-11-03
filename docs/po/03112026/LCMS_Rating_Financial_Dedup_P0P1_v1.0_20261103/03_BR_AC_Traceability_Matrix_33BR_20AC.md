# BR/AC Traceability

SHA `ebc2b27ca08314ecaa69a2bb9bf9d35762222a01` · PO UAT = NOT RUN

## 33 BR

| BR | Phase | DEV | PO UAT | Implementation | Tests |
|----|-------|-----|--------|----------------|-------|
| BR-DF-001 | P0 | COMPLETED | NOT RUN | `CreateRatingCommand seed flags default false; RateBillForm seed=false` | `TC-01` |
| BR-DF-002 | P0 | COMPLETED | NOT RUN | `ApplyRatingToFinancialsCommand + ApplyRatingFinancialsButton` | `TC-02,04` |
| BR-DF-003 | P0 | COMPLETED | NOT RUN | `Apply BUY→Cost / SELL→Revenue separation` | `TC-05,06` |
| BR-DF-004 | P0 | COMPLETED | NOT RUN | `EconomicIdentity.Build 9-dimension key` | `TC-07,09` |
| BR-DF-005 | P0 | COMPLETED | NOT RUN | `Leg/vendor occurrence keeps multi FREIGHT` | `TC-08,10` |
| BR-DF-006 | P0 | COMPLETED | NOT RUN | `AMBIGUOUS_ECONOMIC_IDENTITY + ResolveAmbiguousObligationCommand` | `TC-11,12` |
| BR-DF-007 | P0 | COMPLETED | NOT RUN | `Single active Expected via supersede` | `TC-02,13` |
| BR-DF-008 | P0 | COMPLETED | NOT RUN | `Reapply same price no sum inflation` | `TC-02,14` |
| BR-DF-009 | P0 | COMPLETED | NOT RUN | `Newer Expected supersedes older` | `TC-03,15` |
| BR-DF-010 | P0 | COMPLETED | NOT RUN | `IdempotencyScopes.RatingApplyCost/Revenue` | `TC-04,16` |
| BR-DF-011 | P0 | COMPLETED | NOT RUN | `MaySeedToFinancials surcharge requires applied` | `TC-18..20` |
| BR-DF-012 | P0 | COMPLETED | NOT RUN | `FilterSeedableDetails group by source lineage` | `TC-21,22` |
| BR-DF-013 | P0 | COMPLETED | NOT RUN | `HasProtectedMaturity blocks Actual/Confirmed overwrite` | `TC-23,24` |
| BR-DF-014 | P0 | COMPLETED | NOT RUN | `EffectiveFactSelector Actual>Confirmed>Expected` | `TC-25,26` |
| BR-DF-015 | P0 | COMPLETED | NOT RUN | `Partial remaining Expected with Actual` | `TC-28` |
| BR-DF-016 | P0 | COMPLETED | NOT RUN | `GetBillFinancialProfileQuery uses selector` | `TC-29,30` |
| BR-DF-017 | P0 | COMPLETED | NOT RUN | `as_of + FX via ProjectMaturity/reporting` | `TC-31,32` |
| BR-DF-018 | P0 | COMPLETED | NOT RUN | `ManualCostIdentityKey / CreateCostCommand` | `TC-33,34` |
| BR-DF-019 | P0 | COMPLETED | NOT RUN | `AdjustCost/Revenue commands for higher maturity` | `TC-35` |
| BR-DF-020 | P0 | COMPLETED | NOT RUN | `AppliedFromRatingId + exclusion reasons` | `TC-36,37` |
| BR-DF-021 | P0 | COMPLETED | NOT RUN | `Permission CostCreate/RevenueCreate on Apply` | `TC-17,38` |
| BR-DF-022 | P0 | COMPLETED | NOT RUN | `tenant_id in identity + indexes` | `TC-39` |
| BR-DF-023 | P0 | COMPLETED | NOT RUN | `FinancialClose Locked guard on Apply` | `TC-40` |
| BR-DF-024 | P0 | COMPLETED | NOT RUN | `sql/run_mg_inventory.py + seed_lsstest_bill.py` | `TC-41,42` |
| BR-DF-025 | P0 | COMPLETED | NOT RUN | `No document seed on Apply path` | `TC-43` |
| BR-DF-026 | P0 | COMPLETED | NOT RUN | `2/3 maturity layers without fake Confirmed` | `TC-27,44` |
| BR-DF-027 | P1 | COMPLETED | NOT RUN | `BillRatingPanel + BillRatingFinancialWorkspace` | `TC-45` |
| BR-DF-028 | P1 | COMPLETED | NOT RUN | `PreviewApply + confirm modal` | `TC-47,48` |
| BR-DF-029 | P1 | COMPLETED | NOT RUN | `Rating status labels CP6.5` | `TC-49` |
| BR-DF-030 | P1 | COMPLETED | NOT RUN | `Workspace table EffectiveIncluded + reason VI` | `TC-50,51` |
| BR-DF-031 | P1 | COMPLETED | NOT RUN | `Timeline events in workspace` | `TC-52` |
| BR-DF-032 | P1 | COMPLETED | NOT RUN | `SegmentedProfitKpiDto 3 cards` | `TC-53,54` |
| BR-DF-033 | P1 | COMPLETED | NOT RUN | `In-place confirm/actualize buttons` | `TC-55` |

## 20 AC

| AC | Phase | DEV | PO UAT | Evidence |
|----|-------|-----|--------|----------|
| AC-01 | P0 | PASS | NOT RUN | `CreateRatingCommand (seed flags default false) + RateBillForm (seed=false) | TC-01` |
| AC-02 | P0 | PASS | NOT RUN | `ApplyRatingToFinancialsCommand (BUY→Cost, SELL→Revenue tách biệt) | TC-02, TC-04, TC-05, TC-06` |
| AC-03 | P0 | PASS | NOT RUN | `EconomicIdentity.Build (9-dimension key: giữ độc lập khác Leg/Vendor, gộp đúng cùng nghĩa vụ) | TC-07, TC-08, TC-09, TC-10` |
| AC-04 | P0 | PASS | NOT RUN | `Ambiguity Guard (AMBIGUOUS_ECONOMIC_IDENTITY) + ResolveAmbiguousObligationCommand có audit | TC-11, TC-12` |
| AC-05 | P0 | PASS | NOT RUN | `SeedExpectedCosts/RevenuesFromRatingCommand (supersede Expected cũ, giữ tối đa 1 active Expected tại as-of) | TC-02, TC-03, TC-13, TC-14, TC-15` |
| AC-06 | P0 | PASS | NOT RUN | `IdempotencyScopes.RatingApplyCost/Revenue + Atomic UnitOfWork rollback + RBAC guard | TC-04, TC-16, TC-17, TC-38` |
| AC-07 | P0 | PASS | NOT RUN | `RatingFinancialApply.FilterSeedableDetails (chỉ seed surcharge APPLIED; chặn NOT_SELECTED/UNPRICED/EXCLUDED; giữ lineage riêng) | TC-18, TC-19, TC-20, TC-21, TC-22` |
| AC-08 | P0 | PASS | NOT RUN | `HasProtectedMaturity (không ghi đè Confirmed/Actual) + FinancialClose Locked guard + Adjust/Reversal audit | TC-23, TC-24, TC-35, TC-40` |
| AC-09 | P0 | PASS | NOT RUN | `EffectiveFactSelector (Actual > Confirmed > Expected; Partial Actual = Actual + phần Expected còn lại; hỗ trợ 2/3 lớp) | TC-25, TC-26, TC-27, TC-28, TC-44` |
| AC-10 | P0 | PASS | NOT RUN | `GetBillFinancialProfileQuery + ChargeProfitability + Export đồng bộ qua EffectiveFactSelector cùng as-of & FX snapshot | TC-25, TC-29, TC-30, TC-31, TC-32` |
| AC-11 | P0 | PASS | NOT RUN | `ManualCostIdentityKey (manual:{id} độc lập) + Import external_id SoT + Tenant isolation | TC-33, TC-34, TC-39` |
| AC-12 | P0 | PASS | NOT RUN | `GetBillEffectiveFinancialWorkspaceQuery (drill-down dòng tham gia, dòng bị loại, AppliedFromRatingId, exclusion_reason) | TC-36, TC-37` |
| AC-13 | P0 | PASS | NOT RUN | `MG01_backup_manifest.json + run_mg_inventory.py + seed_lsstest_bill.py + verify_lsstest_effective.py trên test tenant | TC-41, TC-42` |
| AC-14 | P0 | PASS | NOT RUN | `ApplyRatingToFinancials không tự sinh hoặc nhân đôi FinancialDocument / AP / AR / Settlement | TC-43` |
| AC-15 | P1 | PASS | NOT RUN | `BillRatingPanel (Khu vực A/B) + BillRatingFinancialWorkspace (Khu vực D/E/F) + giữ drill-down tab Chi phí/Doanh thu | TC-45, TC-46` |
| AC-16 | P1 | PASS | NOT RUN | `ApplyRatingFinancialsButton Preview delta + cảnh báo khi có Actual/Confirmed + nhãn trạng thái Rating CP6.5 tách khỏi Maturity | TC-47, TC-48, TC-49` |
| AC-17 | P1 | PASS | NOT RUN | `Bảng Hồ sơ tài chính Khu vực D hiển thị EffectiveIncluded (Có/Không), EconomicIdentity, nguồn Rating và lý do loại trừ tiếng Việt | TC-50, TC-51` |
| AC-18 | P1 | PASS | NOT RUN | `Khu vực F Audit Timeline hiển thị lịch sử Rating, Apply, Supersede, version và người thực hiện ở chế độ read-only | TC-52` |
| AC-19 | P1 | PASS | NOT RUN | `Khu vực E hiển thị 3 thẻ KPI (Lợi nhuận Dự kiến, Ước tính tốt nhất kèm Maturity Mix, Thực tế kèm cảnh báo Incomplete) | TC-53, TC-54` |
| AC-20 | P1 | PASS | NOT RUN | `Khu vực F thao tác Xác nhận / Ghi nhận Thực tế tại chỗ qua domain API hiện hữu kèm evidence, không tạo dòng mới | TC-55` |
