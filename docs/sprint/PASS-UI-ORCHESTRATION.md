# Pass UI — orchestration (coordinator)

**Mode:** **Agent** (không Plan). Plan đã khóa ở `PLAN-UI.md`; giai đoạn đầu = làm theo DoD.

**Rule:** U0→U4 = **một agent/chat riêng từng sprint**. Coordinator chỉ kickoff tuần tự; vá lỗi sau này → resume đúng agent hoặc New Chat paste lại `PROMPT-UI-N.md`.

**vs Pass 2:** Dev song song OK. Merge/deploy **serialize** (xem `README-AGENTS.md` § Parallel). Prefer PR; không đua push `main` với S-FULL agent. `handoff.md` / README: append section riêng — không rewrite cả file.

## Queue

| Sprint | Agent | Status | Notes |
|--------|-------|--------|-------|
| U0 | [UI-0](cf1b501c-7007-42fa-a90f-5bf7076d3040) | Done | Scaffold + login + proxy — `52f5ca1`, 86 tests |
| U1 | [UI-1](4138d876-5722-42f6-a69a-bc23f8656ab9) | Done | Bill hub — `4aeab7a`; VPS `/bills` + maturity verified |
| U2 | [UI-2](bc-5377dafd-4e8b-4761-801f-ae76de6079b7) | Done | Cost & Revenue — [Review](bc-5377dafd-4e8b-4761-801f-ae76de6079b7#changes); on `main` (`01d8a50`+) |
| U3 | [UI-3](bc-e514db5a-484e-4333-a372-f8d08e0cdb32) | Done | Control desk — [Review](bc-e514db5a-484e-4333-a372-f8d08e0cdb32#changes); merged PR #26 → `657be51` |
| U4 | [UI-4](bc-9c035d0b-b5a7-44ca-9511-2927f0d3c03d) | In progress (cloud) | Documents & AP/AR thin |

## Stop conditions (serious)
- Money/auth tests đỏ không sửa được trong sprint
- Production deploy hỏng `/health` sau ship
- Merge conflict phá Pass 2 money path không resolve an toàn

Còn lại: agent tự sửa và ship tiếp.
