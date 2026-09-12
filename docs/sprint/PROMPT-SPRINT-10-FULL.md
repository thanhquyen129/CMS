# PROMPT — Pass 2 / Sprint 10 FULL Financial Close

Bạn đang làm **Pass 2 — Sprint 10 FULL** trong `c:\A1\git\cms`. Pass 1 S10 + Pass 2 S0–S9 đã trên `main`.

## Mục tiêu TD6 E12 đủ hơn thin
Close eligibility đầy đủ hơn; period lock chặn mutation ledger khi locked; policy Controlled vs Strict; AC-008 giữ vững.

## Must ship
1. **Eligibility checklist** (config flags, default on): block snapshot nếu trong scope còn (a) open/in_progress **critical** exception; (b) unmatched accepted documents stub; (c) unsettled AP/AR with open balance above threshold stub — mỗi gate trả VI reason rõ.
2. **Period lock:** khi close `Locked`, reject Cost/Revenue maturity confirm + Payment/Collection allocate/finalize trong period/scope (config `FinancialClose:EnforcePeriodLock`, default true). Snapshot vẫn insert-only (C-010).
3. **Policy stub:** `Controlled` (Pass 1 behavior) vs `Strict` (eligibility + period lock bắt buộc; không bypass). Ghi trên close record.
4. Reopen/reclose: không sửa snapshot cũ; version/history append; VI errors.
5. Tests: eligibility gates; period lock blocks mutation; immutability; reopen history; tenant isolation. Suite xanh.
6. `SPRINT-10-FULL-DOD.md` + handoff + ADR nếu policy/lock irreversible + PR.

## Non-goals
Reporting dashboard / P&L projections (Sprint 11); full hardening/UAT (Sprint 12); Next.js close UI.

## Ràng buộc
Never secrets/alogex. Vietnamese errors. Read `SPRINT-10-DOD.md` deferred. UUIDv7; snapshots never hard-deleted.
