# P13 — UI Audit trail đối tượng tiền DoD

**Ngày:** 2026-09-13  
**PO:** E14/E16 · Checklist

## Done
1. `AuditTrailPanel` + BFF `GET /bff/audit-events`.
2. Mount: Cost detail (`cost`); Financial close latest snapshot (`financial_close_snapshot`).

## Verify
- Manual: mở chi phí / bản chốt → Nhật ký kiểm toán.

## Non-goals
- Global audit explorer; Bill create audit writes; payment header trail.
