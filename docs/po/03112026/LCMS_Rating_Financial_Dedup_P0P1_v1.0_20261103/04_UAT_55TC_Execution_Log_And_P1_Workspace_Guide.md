# UAT 55 TC Execution Log & P1 Workspace Guide

**DEV QA = PASS** · **PO UAT = NOT RUN** · Commit: xem git log bàn giao

## Kết quả 55 TC

| TC | Phase | DEV QA | PO UAT | Actual / Evidence |
|----|-------|--------|--------|-------------------|
| TC-01 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-02 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-03 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-04 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-05 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-06 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-07 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-08 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-09 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-10 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-11 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-12 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-13 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-14 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-15 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-16 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-17 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-18 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-19 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-20 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-21 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-22 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-23 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-24 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-25 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-26 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-27 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-28 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-29 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-30 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-31 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-32 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-33 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-34 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-35 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-36 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-37 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-38 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-39 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-40 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-41 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-42 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-43 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-44 | P0 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-45 | P1 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-46 | P1 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-47 | P1 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-48 | P1 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-49 | P1 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-50 | P1 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-51 | P1 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-52 | P1 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-53 | P1 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-54 | P1 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |
| TC-55 | P1 | PASS | NOT RUN | DEV logic/automated; chờ PO Staging |

### Blocker coverage (code)

- TC-02/03/23/25/26/30 → RatingFinancialDedupP0Tests
- TC-08 multi-leg → same
- TC-18 applied-only seed → FilterSeedableDetails
- Selector unit → EconomicIdentityEffectiveFactSelectorTests

## P1 Workspace A→F (tab «Tính giá & Tài chính»)

| Khu | Nội dung |
|-----|----------|
| A | RateBillForm context BUY/SELL, Bill/Leg, CW, as-of |
| B | Tính thử giá mua/bán; so sánh lịch sử rating + surcharge buckets |
| C | ApplyRatingFinancialsButton: xem trước delta + xác nhận Apply + idempotency |
| D | Drill-down tab Chi phí/Doanh thu (effective totals trên profile) |
| E | KPI lợi nhuận trên overview/profile (Best Available qua selector); Expected/Actual layers |
| F | Lịch sử tính giá read-only + Apply/maturity qua domain API hiện hữu |

Ảnh tham chiếu PO: screenshots/ (Bang_chung_UAT). Mockup UI_ThamKhao không dùng số liệu làm AC.
