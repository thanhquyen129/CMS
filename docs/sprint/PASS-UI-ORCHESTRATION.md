# Pass UI — orchestration (coordinator)

**Mode:** **Agent** (không Plan). Plan đã khóa ở `PLAN-UI.md`.

**Rule:** U0→U5 = **một agent/chat riêng từng sprint**. Vá lỗi sau này → resume đúng agent hoặc New Chat paste `PROMPT-UI-N.md`.

**Status: Pass UI U0–U5 COMPLETE** (Settlement + Close = U5).

## Queue

| Sprint | Agent | Status | Notes |
|--------|-------|--------|-------|
| U0 | [UI-0](cf1b501c-7007-42fa-a90f-5bf7076d3040) | Done | Scaffold + login + proxy |
| U1 | [UI-1](4138d876-5722-42f6-a69a-bc23f8656ab9) | Done | Bill hub |
| U2 | [UI-2](0aefe08a-9f6f-4b1b-9cbb-b6018f50323d) | Done | Cost & Revenue confirm |
| U3 | [UI-3](6ea60a75-b200-4001-b690-33a9fcbd6d74) | Done | Control desk |
| U4 | [UI-4](e1fa18ab-050c-4edd-8f07-b2d80b6fb6c3) | Done | Documents & AP/AR — PR #27; VPS verified |
| U5 | local | Done | Settlement + Close — `UI-5-DOD.md` |

## Follow-ups (not blocking)
- Match UI, reverse allocation, write-off/recognize UI → **Match UI Done** (`UI-MATCH-DOD.md`); còn reverse allocation / write-off / recognize
- `billId` filter on document list DTO
- Actions race → prefer serialize deploy / `--no-cache web` when UI drifts

## Vá lỗi theo sprint
Resume agent trong bảng trên, hoặc New Chat + `PROMPT-UI-N.md`.
