# P03 — Approval gate write-off > trần DoD

**Ngày:** 2026-09-12  
**PO:** ADR-0008; E10/E11; H-009 · Checklist `PO-GAP-CHECKLIST.md`

## Done
1. `Settlement:MaxWriteOffAmount` = trần **áp dụng ngay**; vượt trần → tạo Approval (`accounts_payable` / `accounts_receivable`) + **202** `{ requiresApproval, approvalId }` — outstanding chưa đổi.
2. Duyệt cuối trên `/api/approvals/{id}/approve` → áp cùng path xóa nợ (AdjustmentAmount âm + note); từ chối → không ghi.
3. UI `WriteOffButton`: cho phép số > trần; thông báo + link hàng đợi phê duyệt khi 202.
4. Queue label + deep-link AP/AR → `/ap-ar`.
5. ADR-0008 cập nhật; test `WriteOff_OverThreshold_RequiresApproval_ThenApplies`.

## Verify
- `dotnet test` filter Sprint8FullSettlementTests: 4 passed
- `npm run build` apps/web OK

## Non-goals
- Ma trận approver multi-level (P12)
- Ngưỡng theo tenant (P20)
