# AS-IS Root Cause & Technical Implementation — Surcharge Application P0

## 1. Root cause

| Hiện tượng | Nguyên nhân mã nguồn |
|---|---|
| Phụ phí tự cộng khi Tính giá | `SurchargeRating.Select` chỉ lọc effective + Matches(scope/condition); không có activation |
| Điều kiện trống → cộng | Scope/condition trống = pass eligibility (đúng BA) nhưng AS-IS coi luôn Applied |
| DG unknown bị coi false | `IsDangerousAsync` trả `false` khi thiếu commodity |
| Không mở v2 Published | UI `versions[0]` + không có GET exact version |

## 2. Ánh xạ Data Model (logic BA → AS-IS)

| Logic BA | PostgreSQL / Entity |
|---|---|
| `surcharge_versions.activation_policy` | `surcharge_versions.activation_policy` (`SurchargeVersion.ActivationPolicy`) |
| `activation_config` | `activation_config_json` (jsonb) |
| `approval_ref` / approved | `approval_ref`, `approved_by_user_id`, `approved_at` |
| `service_selections` | `service_selections` + `service_selection_audits` |
| `rating_details` activation snapshot | cột nullable trên `rating_details` (chỉ dòng Applied có tiền) |
| `migration_audit` | `surcharge_activation_migration_audits` |
| 3 nhóm preview | `Rating.ContextJson`: `appliedCharges` / `eligibleNotActivatedCharges` / `excludedCharges` |

Migration: `20261102170000_SurchargeActivationPolicy` — backfill Published cũ → `UNCLASSIFIED_LEGACY`.

## 3. Engine 6 bước

File: `src/LCMS.Application/Surcharges/SurchargeRating.cs` (`Evaluate`) + `SurchargeActivation.cs` (`SurchargeActivationResolver`).

1. Lọc published/effective/direction  
2. Eligibility (conditions + scopes)  
3. Activation (`MANDATORY` / `AUTO` / `OPTIONAL` / `UNCLASSIFIED`)  
4. Precedence / dedupe / AMBIGUOUS  
5. Calculation (tách `fixed_rate` × `per_bill`/`per_unit`/…)  
6. Snapshot Applied + ContextJson buckets; Seed chỉ Applied

## 4. API

- Create/Update/Publish surcharge: `activationPolicy`, `activationConfig`, `approvalRef`; chặn UNCLASSIFIED publish; Mandatory cần quyền `rate.surcharge.mandatory.approve`
- `GET /api/surcharges/{id}/versions/{versionId}` — đúng version, readOnly nếu published
- `/api/service-selections` GET/PUT + `/audits` — revision + stale rating

## 5. UI CP6.5

- `SurchargeForm`: khu Eligibility / Activation / Calculation  
- Bảng version: **Xem chi tiết** published → `/rate-cards/surcharges/[id]/versions/[versionId]`  
- Draft: **Chỉnh sửa** trên trang master  
- Badge `UNCLASSIFIED_LEGACY`

## 6. Files chính

- Domain: `Surcharge.cs`, `ServiceSelection.cs`, `RatingDetail.cs`, `PermissionCodes.cs`
- App: `SurchargeRating.cs`, `SurchargeActivation.cs`, `SurchargeCommands.cs`, `SurchargeQueries.cs`, `ServiceSelectionCommands.cs`, `CreateRatingCommand.cs`
- Infra: configurations + migration
- Web: `SurchargeForm.tsx`, surcharge detail/version pages
- Tests: `SurchargeApplicationP0Tests.cs`
