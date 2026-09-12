# Sprint kickoff prompts (one New Chat / cloud agent each)

Rule: **một sprint = một chat/agent riêng**. Không nhúng Task subagent vào chat điều phối.

## Two-pass delivery (PO-authorized)
| Pass | Mục tiêu |
|------|----------|
| **Pass 1 — Thin slice** | DoD mỏng nhất vận hành được. |
| **Pass 2 — Full PO** | Chạy lại từng sprint cho đủ TD6 + CP + AC gates. |
| **Pass UI** | Next.js trên VPS — song song Pass 2. Plan: `PLAN-UI.md`. |

**Pass 1: COMPLETE.** **Pass 2: IN PROGRESS.** **Pass UI: READY (start U0).**

## Status Pass 2
| Sprint | Status |
|--------|--------|
| 0–7 FULL | Done |
| 8 Settlement FULL | Done (see `SPRINT-8-FULL-DOD.md`) |
| 9 Financial Control FULL | In progress (cloud) |
| 10–12 FULL | Queued |

## Status Pass UI
| Sprint | Status | Prompt |
|--------|--------|--------|
| U0 Scaffold + login + proxy | Ready | `PROMPT-UI-0.md` |
| U1 Bill hub | Queued | `PROMPT-UI-1.md` |
| U2 Cost & Revenue actions | Queued | `PROMPT-UI-2.md` |
| U3 Control desk | Queued | `PROMPT-UI-3.md` |
| U4 Documents & AP/AR thin | Queued | `PROMPT-UI-4.md` |

## How to run Pass 2 / Pass UI
1. Cloud / New Agent Chat riêng.
2. Paste `PROMPT-SPRINT-N-FULL.md` **hoặc** `PROMPT-UI-N.md`.
3. Ship PR → merge → sprint kế.

---

Repo: `c:\A1\git\cms` · `main` · .NET 8 LCMS.* + `apps/web` (from U0)  
Ship: handoff + commit/PR. No secrets. Never alogex.
