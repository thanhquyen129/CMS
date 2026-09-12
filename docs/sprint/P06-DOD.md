# P06 — Bảng `fx_rates` theo ngày DoD

**Ngày:** 2026-09-12  
**PO:** TD1 D02; ADR-0004/0008/0011 · Checklist `PO-GAP-CHECKLIST.md`

## Done
1. Entity `fx_rates` (tenant): from/to, `rate_date`, `rate`, `source`, `version` + unique index.
2. Migration `P06_FxRates`.
3. Lookup: latest `rate_date ≤ asOf` → gắn `BaseAmount` + `FxRateId` trên Cost / Revenue / Settlement (Payment/Collection/allocations).
4. Fallback `StubFxRatesToBase` khi chưa có dòng (FxRateId null) — không silent 1:1.
5. API: `POST/GET/DELETE /api/fx-rates`, `GET /api/fx-rates/resolve`.
6. Dashboard roll-up dùng dated rates (asOf UTC) + fallback.
7. ADR-0004 amended; ADR-0011 note cập nhật.
8. Tests `SprintP06FxRatesTests`.

## Verify
- `dotnet test` filter `SprintP06FxRatesTests` xanh.
- Stub path Sprint4/5 FX vẫn xanh.

## Non-goals (follow-up)
- UI quản lý tỷ giá (P18-ish / settings).
- Market feed auto-import.
- Bỏ hẳn config stub (giữ cutover an toàn).
