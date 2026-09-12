# Pass UI — orchestration (coordinator)

**Mode:** **Agent** (không Plan). Plan đã khóa ở `PLAN-UI.md`; mỗi đoạn đầu = làm theo DoD.

**Rule:** U0→U4 = **một agent/chat riêng từng sprint**. Coordinator chỉ kickoff tuần tự; vá lỗi sau này → resume đúng agent hoặc New Chat paste lại `PROMPT-UI-N.md`.

## Queue

| Sprint | Agent | Status | Notes |
|--------|-------|--------|-------|
| U0 | [UI-0](cf1b501c-7007-42fa-a90f-5bf7076d3040) | **Done** | Scaffold + login + proxy; ADR-0007; `UI-0-DOD.md` |
| U1 | — | Ready | Bill hub |
| U2 | — | Blocked on U1 | Cost & Revenue |
| U3 | — | Blocked on U2 | Control desk |
| U4 | — | Blocked on U3 | Documents & AP/AR |

## Stop conditions (serious)
- Money/auth tests đỏ không sửa được trong sprint
- Production deploy hỏng `/health` sau ship
- Merge conflict phá Pass 2 money path không resolve an toàn

Còn lại: agent tự sửa và ship tiếp.
