# Truy vết 24 BR + 18 AC — DEV hoàn tất / PO UAT = NOT RUN

## BR-SUR-001 → BR-SUR-024

| BR | DEV xử lý | Vị trí thực thi | Test | PO UAT |
|---|---|---|---|---|
| BR-SUR-001 | ĐÃ HOÀN TẤT | `SurchargeActivationValidator`, migration UNCLASSIFIED | P0 + Independent | NOT RUN |
| BR-SUR-002 | ĐÃ HOÀN TẤT | `SurchargeRating.Evaluate` | TC01/02/10 | NOT RUN |
| BR-SUR-003 | ĐÃ HOÀN TẤT | Eligibility ≠ Activation | TC01 | NOT RUN |
| BR-SUR-004 | ĐÃ HOÀN TẤT | Publish Mandatory + approval_ref | TC11 | NOT RUN |
| BR-SUR-005 | ĐÃ HOÀN TẤT | AUTO predicates + UNKNOWN | TC07-09 | NOT RUN |
| BR-SUR-006 | ĐÃ HOÀN TẤT | `service_selections` persisted | TC01/02 API | NOT RUN |
| BR-SUR-007 | ĐÃ HOÀN TẤT | Direction filter + UNPRICED | TC12 | NOT RUN |
| BR-SUR-008 | ĐÃ HOÀN TẤT | Selection scope Bill/Shipment/Leg + allocation note | scope rank | NOT RUN |
| BR-SUR-009 | ĐÃ HOÀN TẤT | FIXED × basis | TC15 | NOT RUN |
| BR-SUR-010 | ĐÃ HOÀN TẤT | Dedupe by code | Independent ambiguous | NOT RUN |
| BR-SUR-011 | ĐÃ HOÀN TẤT | `AMBIGUOUS_SURCHARGE` | Independent | NOT RUN |
| BR-SUR-012 | ĐÃ HOÀN TẤT | ContextJson 3 nhóm | API rating | NOT RUN |
| BR-SUR-013 | ĐÃ HOÀN TẤT | Seed Applied only; rerate supersede | baseline seed tests | NOT RUN |
| BR-SUR-014 | ĐÃ HOÀN TẤT | Published immutable; clone draft | version commands | NOT RUN |
| BR-SUR-015 | ĐÃ HOÀN TẤT | selection audits | ServiceSelectionCommands | NOT RUN |
| BR-SUR-016 | ĐÃ HOÀN TẤT | NEEDS_CONTEXT | TC09 | NOT RUN |
| BR-SUR-017 | ĐÃ HOÀN TẤT | UNPRICED | TC12 | NOT RUN |
| BR-SUR-018 | ĐÃ HOÀN TẤT | No bulk mandatory; legacy unclassified | migration | NOT RUN |
| BR-SUR-019 | ĐÃ HOÀN TẤT | Conditions/scopes retained | Matches() | NOT RUN |
| BR-SUR-020 | ĐÃ HOÀN TẤT | DATA_CONFLICT on selection conflict | resolver | NOT RUN |
| BR-SUR-021 | ĐÃ HOÀN TẤT | Percentage on base only | Independent percent | NOT RUN |
| BR-SUR-022 | ĐÃ HOÀN TẤT | FX/VAT via existing rating path | Independent FX | NOT RUN |
| BR-SUR-023 | ĐÃ HOÀN TẤT | `RateSurchargeMandatoryApprove` / `RateServiceSelect` | TC11/permissions | NOT RUN |
| BR-SUR-024 | ĐÃ HOÀN TẤT | Inventory/impact report + unclassified list | báo cáo 04 | NOT RUN |

## AC-01 → AC-18

Tất cả AC map BR/TC như Excel; cột **PO nghiệm thu = NOT RUN**. Cột bằng chứng DEV = `SurchargeApplicationP0Tests` + implementation files trên. Excel `LCMS_Surcharge_BR_AC_Traceability_v1.0_20261102.xlsx` đã cập nhật DEV.
