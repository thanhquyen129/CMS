# BR/AC Traceability (remaster)

SHA `37f4ed2a6ec8b38d1123556e52cae2790281eb68` · PO UAT = NOT RUN

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
| AC-01 | P0 | PASS | NOT RUN | `TC-01 UnappliedDoesNotCreateFacts + CreateRating seed=false` |
| AC-02 | P0 | PASS | NOT RUN | `TC-05/06 BuyDoesNotCreateRevenue / SellDoesNotCreateCost` |
| AC-03 | P0 | PASS | NOT RUN | `TC-02/03/07 Reapply + SameIdentityCollapses` |
| AC-04 | P0 | PASS | NOT RUN | `TC-11/12 AmbiguityDetected + AmbiguityResolutionActionsValid` |
| AC-05 | P0 | PASS | NOT RUN | `TC-04/16 IdempotentApplySemantics + IdempotencyKeyRequiredConcept` |
| AC-06 | P0 | PASS | NOT RUN | `TC-18..20 SurchargeNullBucketBlocked / Eligible / Excluded` |
| AC-07 | P0 | PASS | NOT RUN | `TC-21/22 DuplicateSurchargeRulesCollapsed + lineage` |
| AC-08 | P0 | PASS | NOT RUN | `TC-23/24 ActualProtected / ConfirmedProtected` |
| AC-09 | P0 | PASS | NOT RUN | `TC-25/26/28 BestAvailable + PartialActual` |
| AC-10 | P0 | PASS | NOT RUN | `TC-29/30 ProfileUsesSelector + SampleBill1740k700k` |
| AC-11 | P0 | PASS | NOT RUN | `TC-33/34 ManualIndependentKey + ImportSourceDedupConcept` |
| AC-12 | P0 | PASS | NOT RUN | `TC-38/39/40 RBAC + tenant + closed scope` |
| AC-13 | P0 | PASS | NOT RUN | `TC-41/42 inventory + dataset rebuild scripts` |
| AC-14 | P0 | PASS | NOT RUN | `TC-43/44 DocumentsNotDuplicated + ThreeLayerOptionalConfirmed` |
| AC-15 | P1 | PASS | NOT RUN | `TC-45 WorkspaceTabLabel + BillRatingPanel A–F` |
| AC-16 | P1 | PASS | NOT RUN | `TC-47/48 PreviewDeltaShape + ActualWarningCopy` |
| AC-17 | P1 | PASS | NOT RUN | `TC-49 RatingStatusLabelsDistinctFromConfirmed` |
| AC-18 | P1 | PASS | NOT RUN | `TC-50/51 ExclusionReasonVietnamese + ParticipationBadge` |
| AC-19 | P1 | PASS | NOT RUN | `TC-52/53 TimelineEventTypes + ThreeKpis` |
| AC-20 | P1 | PASS | NOT RUN | `TC-54/55 ActualIncompleteWarning + MaturityInPlaceNoNewRow` |
