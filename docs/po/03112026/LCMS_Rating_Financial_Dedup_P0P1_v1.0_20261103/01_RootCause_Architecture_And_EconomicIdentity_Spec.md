# Root Cause, Architecture & EconomicIdentity

## Root Cause (Bill LSSTEST02112026002 pattern)

1. **Seed theo `SourceId = RatingDetail.Id`** (`SeedExpectedCostsFromRatingCommand`) — mỗi lần CreateRating sinh detail mới → Cost mới dù cùng FREIGHT/vendor/leg.
2. **Leg re-rate** có `RetireLegExpectedCostsAsync` (inactive) nhưng Bill-level / seed nút riêng không supersede theo nghĩa vụ.
3. **Tổng Bill** (`GetBillFinancialProfileQuery`) cộng Best Available **từng dòng active** → Actual FREIGHT 1.440.000 + Expected FREIGHT 1.440.000 + 2× DONGGOI 300.000 = **3.480.000**.

## Thiết kế đã ship

### EconomicIdentity (9 chiều → key ổn định)

`tenant|bill|BUY|SELL|charge|occurrence(leg/shipment/bill)|counterparty` — `LCMS.Domain.Pricing.EconomicIdentity`. Không GROUP BY amount.

### ApplyRatingToFinancials

- `POST /api/ratings/{id}/preview-apply` · `POST /api/ratings/{id}/apply-financials` (+ Idempotency-Key)
- BUY → CostCreate · SELL → RevenueCreate · chặn FinancialClose locked
- Atomic qua seed + idempotency remember; chỉ `applied` / null-bucket details

### EffectiveFactSelector

Một đại diện / identity: Actual > Confirmed > Expected; Partial Actual = Actual + remaining Expected (không 10M+6M); superseded/shadowed có `exclusion_reason`.

### Files (I-01…I-14)

| I | Thay đổi |
|---|----------|
| I-01 | CreateRating mặc định không seed; UI tính thử `seed=false` |
| I-02 | `ApplyRatingToFinancialsCommands.cs` |
| I-03 | `EconomicIdentity.cs`, cột `economic_identity_key` |
| I-04 | Seed Cost/Revenue supersede Expected |
| I-05/06 | `EffectiveFactSelector` + Bill Financial Profile |
| I-07 | Filter `ApplicationBucket` may-seed |
| I-08 | Migration `20261103120000_EconomicIdentityRatingDedup` |
| I-09/10 | Best Available / reporting sync |
| I-11…14 | Tab workspace, Apply button, BFF preview/apply, labels CP6.5 |

ADR: `docs/adr/ADR-0043-economic-identity-rating-apply.md`
