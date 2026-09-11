# Sprint kickoff prompts (one New Chat / cloud agent each)

Rule: **một sprint = một chat/agent riêng**. Không nhúng Task subagent vào chat điều phối.

## Status
| Sprint | Status |
|--------|--------|
| 0 Foundation | Done |
| 1 Identity + Master | Done (see `SPRINT-1-DOD.md`) |
| 2 Operational Reference | Next |
| 3–12 | Queued |

## How to run
1. Cursor: **New Agent Chat** (hoặc Cloud Agent).
2. Paste đúng một file `PROMPT-SPRINT-N.md` dưới đây.
3. Sau khi agent ship (handoff + push), mở chat mới cho sprint kế.

---

Repo: `c:\A1\git\cms` · Branch: `main` · Stack: .NET 8 LCMS.* Clean Architecture + PostgreSQL.
Ship rule: update `docs/handoff.md`, commit, push `origin/main`. No secrets. Never deploy to alogex.
