# PROMPT — Pass 2 / Sprint 4 FULL Cost

Bạn đang làm **Pass 2 — Sprint 4 FULL** trong `c:\A1\git\cms`. Pass 1 S4 + Pass 2 S0–S3 đã trên `main` (hoặc base sau S0–S2 nếu S3 FULL chưa merge — Cost FULL không phụ thuộc Rate depth).

## Mục tiêu TD6 E05/E06 đủ hơn thin
Cost allocation bases đầy đủ; Shared vs Direct; FX stub `base_amount`; optional approval threshold trước confirm; reallocation history.

## Must ship
1. Allocation basis `equal` / `quantity` / `manual_ratio`; C-005/C-006; reallocation supersedes history.
2. Shared vs Direct rules; FX stub `base_amount`; optional approval threshold before confirm.
3. Tests + `SPRINT-4-FULL-DOD.md` + handoff + PR → main. `dotnet test` xanh.

## Non-goals
Full FX market feed; multi-step approval matrix (dùng Sprint 9 Approval); Next.js; Revenue.

## Ràng buộc
Never secrets/alogex. Vietnamese errors. Read `SPRINT-4-DOD.md` deferred.
