# Sprint kickoff prompts (one New Chat / cloud agent each)

Rule: **một sprint = một chat/agent riêng**. Không nhúng Task subagent vào chat điều phối.

## Two-pass delivery (PO-authorized)
| Pass | Mục tiêu |
|------|----------|
| **Pass 1 — Thin slice** | Mỗi sprint chỉ làm **DoD mỏng nhất vận hành được** (MVP). Deferred ghi rõ trong `SPRINT-N-DOD.md`. |
| **Pass 2 — Full PO** | Sau khi Sprint 0–12 Pass 1 xong: **chạy lại từng sprint** (agent/chat riêng) để bổ sung đủ theo TD6 + CP1–CP7 + CP6.5 (acceptance gates AC-001…, không còn “thin stub”). |

Pass 1 đang chạy. Pass 2 bắt đầu khi Pass 1 hoàn tất Sprint 12.

## Status (Pass 1)
| Sprint | Status |
|--------|--------|
| 0 Foundation | Done |
| 1 Identity + Master | Done (see `SPRINT-1-DOD.md`) |
| 2 Operational Reference | Done (merged) |
| 3 Rate & Pricing | Done (see `SPRINT-3-DOD.md`) |
| 4 Cost | Done (see `SPRINT-4-DOD.md`) |
| 5 Revenue & Profitability | Done (see `SPRINT-5-DOD.md`) |
| 6 Financial Documents | Done (see `SPRINT-6-DOD.md`) |
| 7 Exposure + AP/AR | Done (see `SPRINT-7-DOD.md`) |
| 8 Settlement | Done (see `SPRINT-8-DOD.md`) |
| 9 Financial Control | Done (see `SPRINT-9-DOD.md`) |
| 10 Financial Close | Done (see `SPRINT-10-DOD.md`) |
| 11–12 | Queued |

## How to run
1. Cursor: **New Agent Chat** (hoặc Cloud Agent).
2. Paste đúng một file `PROMPT-SPRINT-N.md` (Pass 1) hoặc `PROMPT-SPRINT-N-FULL.md` (Pass 2, tạo khi tới Pass 2).
3. Sau khi agent ship (handoff + push/PR), mở chat mới cho sprint kế.

---

Repo: `c:\A1\git\cms` · Branch: `main` · Stack: .NET 8 LCMS.* Clean Architecture + PostgreSQL.
Ship rule: update `docs/handoff.md`, commit, push `origin/main` (hoặc PR rồi merge). No secrets. Never deploy to alogex.
