# Executive Handover — LCMS-CR-RATING-FINANCIAL-DEDUP-01 (v1.0)

**Ngày:** 03/11/2026  
**Nhánh:** `main` (af5b81a471e23fa103be3ba734af282db29392a2)  
**DEV QA:** PASS (logic + automated suite dedup) · **PO UAT:** NOT RUN · **G4 Production:** NOT RUN

## Tóm tắt điều hành

P0 sửa gốc: tách **Tính thử** khỏi **Áp dụng**; `EconomicIdentity` + supersede Expected; `EffectiveFactSelector` (Actual > Confirmed > Expected) thay SUM thô trên Bill Financial Profile. P1: tab Bill **Tính giá & Tài chính** với tính thử / xem trước / áp dụng (CP6.5). Tương thích surcharge 3 tầng: chỉ seed bucket `applied` (null legacy). Không deploy Production.

## Trả lời 7 mục bàn giao PO (Mục 11 BA)

1. **Root Cause & Scope** — Seed/idempotency theo `rating_detail_id` tạo Cost mới mỗi lần tính; profile `SUM` mọi dòng `active` → FREIGHT Actual + FREIGHT Expected (+ DONGGOI ×2) = 3.480.000đ. Scope: Rating apply/seed, Cost/Revenue identity, Bill financial totals, UI apply. Không đổi policy activation surcharge UAT OPEN.
2. **Changelog & SHA** — ADR-0043; migration `20261103120000_EconomicIdentityRatingDedup`; Apply/Preview API; Seed supersede; EffectiveFactSelector; RateBillForm tính thử không seed. SHA: af5b81a471e23fa103be3ba734af282db29392a2.
3. **SQL inventory & cleanup** — Script `sql/reconcile_rating_financial_dedup.sql` (MG-02/03/05/09). Cleanup chỉ tenant test; backup bắt buộc trước chạy UPDATE. Chi tiết: `02_SQL_Inventory_...`.
4. **33 BR / 20 AC / 55 TC** — DEV = COMPLETED/PASS trên Excel cập nhật trong thư mục này; **PO UAT = NOT RUN** trên mọi dòng. Blocker logic TC-02/03/23/25/26/30 covered by `RatingFinancialDedupP0Tests` + selector tests.
5. **Staging & PO UAT** — Local/Staging only. Bước: mở Bill → tab Tính giá & Tài chính → Tính thử BUY/SELL → Xem trước → Áp dụng (idempotency) → đối soát Chi phí/Doanh thu/Profit Best Available. Không Production.
6. **Rủi ro & Rollback** — Rủi ro: identity legacy thiếu vendor/leg → AMBIGUOUS. Rollback: revert commit + migration Down; khôi phục DB snapshot MG-01.
7. **Ngoại lệ cần PO** — (a) Bill mẫu số 1.740.000 / 2.440.000 / 700.000 chỉ đúng khi xác minh cùng nghĩa vụ BUY lặp và SELL độc lập đã activation. (b) Nghi vấn surcharge sau gói 869e1e7 giữ UAT OPEN — không đóng trong batch này.

## Release Gates

| Gate | Trạng thái |
|------|------------|
| G1 DEV Code + Test | PASS (DEV) |
| G2 PO UAT P0 | NOT RUN |
| G3 P1 UI | READY FOR PO UAT P1 |
| G4 Production | NOT RUN |
