# UI Control desk decide — Approve/Reject + Exception resolve

**Status:** Done  
**Date:** 2026-09-12  
**Source:** `UI-3-DOD.md` follow-ups · handoff after write-off/reverse

## Outcome
Controller **quyết định** trên hàng đợi: phê duyệt/từ chối Approval; xử lý / leo thang / đóng Exception — không còn list chỉ đọc.

## Checklist

| # | Criterion | Status |
|---|-----------|--------|
| 1 | BFF `POST /bff/approvals/{id}/approve\|reject` `{ decisionReason }` | Done |
| 2 | BFF `POST /bff/exceptions/{id}/resolve\|close\|escalate` | Done |
| 3 | `/queues/approvals`: nút Phê duyệt / Từ chối (pending); reject bắt buộc lý do | Done |
| 4 | `/queues/exceptions`: Xử lý · Leo thang · Đóng theo trạng thái; escalate bắt buộc lý do | Done |
| 5 | Copy: Approval ≠ Permission; multi-step stub hiện cấp hiện tại | Done |
| 6 | Deep-link payment/collection khi có; empty/error thật; không invent money API | Done |

## Non-goals
Bank feed · write-off approval gate · reconciliation session UI · multi-step approval wizard

## Verify
- `npm run build` apps/web
- VPS: `/queues/approvals`, `/queues/exceptions` thao tác được (cần data seed/demo)
