# ADR-0039 — Operational reference edit, rating context readiness, stale rating + rerate

- Status: Accepted
- Date: 2026-09-29
- Related: PO package `docs/po/29092026/LCMS_DEV_Fix_Operational_Reference_Edit_Rating_Context_v1.0` (D01–D08,
  RATING-CW-01, BILL-EDIT-01, RATING-READY-01, RERATE-01, OPREF-EDIT-01, OPREF-SOT-01), ADR-0036 (If-Match), H-002, H-003

## Context

UAT could not fix a Bill once it was created (weight, volume, CW, commodity, route, ETD/ETA, mode), rating silently
defaulted the freight quantity to 1 and CW to Actual Weight / 0, and a context change after rating left the old
rating looking current. Order / Shipment / Chặng / Chuyến had no controlled edit either, and a later sync could
erase a user's correction without trace.

## Decision

1. **One edit pattern for all five objects** — `GET /api/operational-references/{type}/{id}/edit`,
   `PATCH /api/operational-references/{type}/{id}` `{changes:{code:value|null}, reason, revertFields}` + If-Match
   (row version), `POST …/chargeable-weight/confirm`. Field catalog per type (`OperationalFieldCatalog`) states
   label, kind, group, rating relevance and ownership key. Permission: Bill → `bill.update`; others → new
   `opref.update`. Data scope: Bill org/owner; non-Bill passes when the actor created it or a linked Bill is in scope,
   else 404. Rating amounts are **not** in the edit DTO (cost/revenue separation).
2. **Source of truth (D04/D08)** — field source is resolved per field: active override → `override`; measure channel;
   FieldOwnership by external system → `api` (needs new `opref.source_override` + reason); external object with a value
   → `import` (reason required — default tenant policy "controlled override with reason"); else `manual`.
   Overrides are stored in `operational_field_overrides` (unique per tenant/object/field, soft delete) with source value,
   override value, channel, reason, actor, time. While an override is active, sync/import writes only update
   `SourceValue` (`OperationalOverrideGuard`) — never the effective value. "Bỏ ghi đè" restores the latest source value.
3. **Chargeable Weight (D02/D07)** — never inferred from Actual Weight, a default or 0. System value needs mode + gross
   + volume (air/express/courier: factor 167 or rule factor; sea: W/M 1000). Undetermined CW is `null`
   ("Chưa xác định / Chưa tính được"). Confirm ≠ override (D06): confirm persists the current value, no reason;
   changing a confirmed / system / source CW is an override that needs `rate.quantity.override` + reason + audit.
4. **Readiness (D03)** — `RatingContextResolver` is shared by `POST /api/ratings/readiness` and rating create. It
   checks only what the *selected* rules need (quantity rules → CW; per-kg → gross; container rate → containers;
   composite → components; no match → the missing dimension + rules). Create throws 409 `rating_not_ready` with
   `missing[]` (`field,label,ruleCodes,message,action`); no rating with quantity 1 is created. Explicit quantity with
   no Bill CW → basis `manual` (API compatibility); explicit quantity different from Bill CW → `override` + reason.
   Rules that do not need a quantity use basis `not_required` and `ChargeableWeightKg = null`.
5. **Stale + rerate (D05)** — `ratings.stale_at/stale_reason`. A Bill edit marks a current rating stale only when that
   rating used the field (basis / selected rules / version rule filters). A rating-relevant edit on a linked Order /
   Shipment / Chặng / Chuyến marks the current ratings of linked Bills. Amounts, details, Cost, Revenue, AP, AR are
   never changed. Rerate = new rating with `supersedesRatingId`; the old one becomes `superseded`. Audit
   `rating.mark_stale`, `opref.field_update`, `opref.field_override`, `measurement.confirm|override|override_clear`.

## Consequences

- Migration `20260929104232_OperationalReferenceEditRatingContext` (additive: 2 nullable columns + 1 table). Rollback:
  drop table + columns; no existing data rewritten.
- Rating API contract: omitted `quantity` no longer means 1 — clients that relied on it now get 409 `rating_not_ready`
  when a quantity rule needs CW. Error payload gains `missing`.
- Compare (`/api/ratings/compare`) follows the same CW rule and no longer uses gross-only / 1.

## Follow-ups

- Tenant setting for import policy (read-only vs controlled override); today: controlled override with reason.
- `POST /api/ratings` has no dedicated execute permission (pre-existing); consider `rate.execute`.
- Stale propagation from sync upserts (Order/Shipment API) — today only user edits and the Bill context form mark stale.
