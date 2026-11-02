# Traceability — 24 BR / 18 AC

**Commit:** `__GIT_SHA__`  
**Excel:** `LCMS_Surcharge_BR_AC_Traceability_v1.0_20261102.xlsx`  
**PO UAT / PO nghiệm thu:** **NOT RUN** trên mọi dòng.

## Bảng 24 BR (DEV xử lý = ĐÃ HOÀN TẤT)

| BR | DEV xử lý | PO UAT | Bằng chứng chính | TC |
|---|---|---|---|---|
| BR-SUR-001 | ĐÃ HOÀN TẤT | NOT RUN | `EnsurePublishable` + migrate UNCLASSIFIED | TC-11,21,30 |
| BR-SUR-002 | ĐÃ HOÀN TẤT | NOT RUN | `SurchargeRating.Evaluate` 6 bước | TC-01,02,07,08 |
| BR-SUR-003 | ĐÃ HOÀN TẤT | NOT RUN | OPTIONAL + `ServiceSelectionSnapshot` | TC-01–06,15 |
| BR-SUR-004 | ĐÃ HOÀN TẤT | NOT RUN | MANDATORY + `approval_ref` | TC-11,30 |
| BR-SUR-005 | ĐÃ HOÀN TẤT | NOT RUN | AUTO predicates / UNKNOWN | TC-07–10 |
| BR-SUR-006 | ĐÃ HOÀN TẤT | NOT RUN | NOT_SELECTED; TC-04 unpersisted | TC-01,04 |
| BR-SUR-007 | ĐÃ HOÀN TẤT | NOT RUN | BUY≠SELL; UNPRICED | TC-12,13 |
| BR-SUR-008 | ĐÃ HOÀN TẤT | NOT RUN | Dedup / AMBIGUOUS | TC-14 |
| BR-SUR-009 | ĐÃ HOÀN TẤT | NOT RUN | PER_UNIT × qty | TC-15 |
| BR-SUR-010 | ĐÃ HOÀN TẤT | NOT RUN | Rerate supersede immutability | TC-03,16,22 |
| BR-SUR-011 | ĐÃ HOÀN TẤT | NOT RUN | Seed BUY applied-only | TC-17 |
| BR-SUR-012 | ĐÃ HOÀN TẤT | NOT RUN | Seed SELL applied-only | TC-18 |
| BR-SUR-013 | ĐÃ HOÀN TẤT | NOT RUN | Exact version GET readOnly | TC-19 |
| BR-SUR-014 | ĐÃ HOÀN TẤT | NOT RUN | Draft update; published blocked | TC-20 |
| BR-SUR-015 | ĐÃ HOÀN TẤT | NOT RUN | ContextJson 3 buckets | TC-01,02,05 |
| BR-SUR-016 | ĐÃ HOÀN TẤT | NOT RUN | ServiceSelection audits | TC-29 |
| BR-SUR-017 | ĐÃ HOÀN TẤT | NOT RUN | RBAC ServiceSelect / MandatoryApprove | TC-29,30 |
| BR-SUR-018 | ĐÃ HOÀN TẤT | NOT RUN | Tenant isolation selections | TC-31 |
| BR-SUR-019 | ĐÃ HOÀN TẤT | NOT RUN | FX snapshot | TC-25 |
| BR-SUR-020 | ĐÃ HOÀN TẤT | NOT RUN | VAT Net/Vat/Gross | TC-26 |
| BR-SUR-021 | ĐÃ HOÀN TẤT | NOT RUN | Percentage base | TC-27 |
| BR-SUR-022 | ĐÃ HOÀN TẤT | NOT RUN | Party/vendor scope | TC-28 |
| BR-SUR-023 | ĐÃ HOÀN TẤT | NOT RUN | Legacy blank ≠ mandatory lúc rating | TC-01,21 |
| BR-SUR-024 | ĐÃ HOÀN TẤT | NOT RUN | Impact inventory + dry-run | TC-22,32 |

## Bảng 18 AC (PO nghiệm thu = NOT RUN)

| AC | DEV verify | PO | Evidence |
|---|---|---|---|
| AC-01 | PASS | NOT RUN | Publish gate; TC-11/21/30 |
| AC-02 | PASS | NOT RUN | TC-01 + screenshot 03 |
| AC-03 | PASS | NOT RUN | TC-02/15 + screenshot 04 |
| AC-04 | PASS | NOT RUN | TC-03/16 |
| AC-05 | PASS | NOT RUN | TC-04 |
| AC-06 | PASS | NOT RUN | TC-07–10 |
| AC-07 | PASS | NOT RUN | TC-12/13 + screenshot 05 |
| AC-08 | PASS | NOT RUN | TC-14 |
| AC-09 | PASS | NOT RUN | TC-15 |
| AC-10 | PASS | NOT RUN | TC-17/18 |
| AC-11 | PASS | NOT RUN | TC-19 + screenshot 01 |
| AC-12 | PASS | NOT RUN | TC-20 + screenshot 02 |
| AC-13 | PASS | NOT RUN | TC-22 + SQL |
| AC-14 | PASS | NOT RUN | TC-23/24 |
| AC-15 | PASS | NOT RUN | TC-25–28 |
| AC-16 | PASS | NOT RUN | JSON/SQL/screenshots/SHA pack |
| AC-17 | PASS | NOT RUN | TC-30 |
| AC-18 | PASS | NOT RUN | TC-31 |

## Truy vết AC ↔ BR ↔ TC ↔ Test method ↔ SHA

| AC | BR liên quan | TC | Hàm test | SHA |
|---|---|---|---|---|
| AC-01 | 001,004,018,023 | TC-11,21,30 | `TC11_…`, `TC21_…`, `TC30_…` | `__GIT_SHA__` |
| AC-02 | 002,003,006,016 | TC-01 | `TC01_Optional_NotSelected_GoesToEligibleBucket` | `__GIT_SHA__` |
| AC-03 | 003,009 | TC-02,15 | `TC02_…`, `TC15_…` | `__GIT_SHA__` |
| AC-04 | 010 | TC-03,16 | `TC03_…`, `TC16_…` | `__GIT_SHA__` |
| AC-05 | 006 | TC-04 | `TC04_Optional_UnpersistedUiPreview_DoesNotActivateOrSeed` | `__GIT_SHA__` |
| AC-06 | 005,016 | TC-07–10 | `TC07_…`…`TC10_…` | `__GIT_SHA__` |
| AC-07 | 007 | TC-12,13 | `TC12_…`, `TC13_…` | `__GIT_SHA__` |
| AC-08 | 008 | TC-14 | `TC14_…` | `__GIT_SHA__` |
| AC-09 | 009 | TC-15 | `TC15_…` | `__GIT_SHA__` |
| AC-10 | 011,012 | TC-17,18 | `TC17_…`, `TC18_…` | `__GIT_SHA__` |
| AC-11 | 013 | TC-19 | `TC19_GetExactPublishedVersionV2_ReturnsReadOnlySnapshot` | `__GIT_SHA__` |
| AC-12 | 014 | TC-20 | `TC20_UpdateDraftV3_KeepsPublishedV2Immutable` | `__GIT_SHA__` |
| AC-13 | 010,024 | TC-22 | `TC22_History_TestLss002_…` | `__GIT_SHA__` |
| AC-14 | 002 | TC-23,24 | `TC23_…`, `TC24_…` | `__GIT_SHA__` |
| AC-15 | 019–022 | TC-25–28 | `TC25_…`…`TC28_…` | `__GIT_SHA__` |
| AC-16 | all | TC-01–32 | Full class + evidence pack | `__GIT_SHA__` |
| AC-17 | 004,017 | TC-30 | `TC30_Rbac_…` | `__GIT_SHA__` |
| AC-18 | 018 | TC-31 | `TC31_TenantIsolation_…` | `__GIT_SHA__` |
