# Sprint kickoff prompts (one New Chat / cloud agent each)

Rule: **một sprint = một chat/agent riêng**. Không nhúng Task subagent vào chat điều phối.

## Two-pass delivery (PO-authorized)
| Pass | Mục tiêu |
|------|----------|
| **Pass 1 — Thin slice** | DoD mỏng nhất vận hành được. |
| **Pass 2 — Full PO** | Chạy lại từng sprint cho đủ TD6 + CP + AC gates. |
| **Pass UI** | Next.js trên VPS — song song Pass 2. Plan: `PLAN-UI.md`. |

**Pass 1: COMPLETE.** **Pass 2: COMPLETE.** **Pass UI: COMPLETE (U0–U5 + G1–G6 + UAT PASS).**

**Post-UAT backlog tuần tự:** `PO-GAP-CHECKLIST.md` (P01→P25). Next: **P01 Shared allocation UI**.

## Status Pass 2
| Sprint | Status |
|--------|--------|
| 0–11 FULL | Done |
| 12 Hardening FULL | Done (see `SPRINT-12-FULL-DOD.md`) — **Pass 2 COMPLETE** |

## Status Pass UI
| Sprint | Status | Prompt |
|--------|--------|--------|
| U0 Scaffold + login + proxy | Done | `PROMPT-UI-0.md` |
| U1 Bill hub | Done | `PROMPT-UI-1.md` |
| U2 Cost & Revenue actions | Done | `PROMPT-UI-2.md` |
| U3 Control desk | Done | `PROMPT-UI-3.md` |
| U4 Documents & AP/AR thin | Done | `PROMPT-UI-4.md` |
| U5 Settlement + Close | Done | `PROMPT-UI-5.md` |

## How to run Pass 2 / Pass UI
1. Cloud / New Agent Chat riêng.
2. Paste `PROMPT-SPRINT-N-FULL.md` **hoặc** `PROMPT-UI-N.md`.
3. Ship **PR** → merge tuần tự → sprint kế (tránh hai agent push `main` cùng lúc).

## Parallel Pass 2 ∥ Pass UI (coordinator)
| OK | Không OK |
|----|----------|
| Dev song song: S-FULL API vs U-sprint UI | Hai deploy / hai push `main` xen kẽ |
| UI chỉ `apps/web` (+ proxy nhẹ); thiếu API → follow-up | UI sửa money path Pass 2 |
| S-FULL không đụng screens/BFF | U2 trước khi U1 Done (trong Pass UI) |
| Merge: API PR trước → rebase UI PR → merge UI | Cả hai `rsync` VPS cùng lúc |

CI: `concurrency` hủy run cũ cùng `ref` — giữ run commit mới. Coordinator: Cancel deploy cũ nếu còn sống; smoke `/health` `/ready` `/`.

---

Repo: `c:\A1\git\cms` · `main` · .NET 8 LCMS.* + `apps/web` (from U0)  
Ship: handoff + commit/PR. No secrets. Never alogex.
