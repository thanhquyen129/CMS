# ADR-0010: Financial close eligibility, period lock, Controlled vs Strict

- Status: Accepted
- Date: 2026-09-12
- Relates: TD1 D11; C-010 / AC-008; TD6 E12; Sprint 10 FULL (Pass 2)

## Context
Pass 1 shipped close sessions + immutable snapshots + reopen/reclose versioning, with a thin critical-exception eligibility stub and Controlled policy only. Ops still needed a fuller eligibility checklist, period lock that blocks live ledger mutations while Locked, and a Strict policy that cannot be bypassed via config.

## Decision
1. **Eligibility checklist** (config `FinancialClose:Eligibility`, default all on):
   - (a) open/in_progress/escalated **critical** exceptions in scope;
   - (b) accepted documents still unmatched/partially matched (stub);
   - (c) unsettled AP/AR with derived outstanding above `UnsettledApArOpenBalanceThreshold` (default 0).
   Each gate returns a distinct Vietnamese Conflict reason.
2. **Period lock**: when a close is `Locked`, reject Cost/Revenue **confirm** and Payment/Collection **allocate/finalize** in that period/scope. Config `FinancialClose:EnforcePeriodLock` default **true**. Snapshots remain insert-only (C-010 / AC-008).
3. **Policy on close record**: `controlled` (respects config flags) vs `strict` (forces eligibility + period lock — no bypass). Copied onto each snapshot at lock time.
4. **Reopen/reclose**: never mutate prior snapshots; append `SnapshotVersion` / new close `VersionNo` + history (unchanged from Pass 1).

## Consequences
- Money-path mutations in a locked bill/period fail fast with VI period-lock message until reopen.
- Controlled tenants may soften gates via config; Strict closes ignore soft-off.
- Full document-match / settlement completion remains the path to clear eligibility — no silent overwrite of snapshots.

## Alternatives rejected
- Mutating locked snapshots on reopen — breaks C-010 / AC-008.
- Soft-deleting snapshots — forbidden (insert-only history).
- Period lock without Strict override — would allow config to disable lock on Strict closes.
