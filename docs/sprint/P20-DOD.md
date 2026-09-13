# P20 — Ngưỡng tài chính theo tenant DoD

**Ngày:** 2026-09-13 · ADR-0004/0009

## Done
1. `TenantFinancialOptionsResolver`: override → node `appsettings`.
2. Write-off AP/AR: `maxWriteOffAmount` tenant.
3. Cost confirm gate: `confirmApprovalThresholdBase` tenant (`CostApprovalGate` async).
4. Recognition policy + version string tenant (P16).

## Verify
- SprintP10 matrix write-off vẫn pass; tenant PUT trong P16 test.

## Non-goals
- SLA hours / close policy override (Dictionary deferred).
