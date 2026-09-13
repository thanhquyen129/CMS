# P12 — Ma trận approver / wizard multi-step DoD

**Ngày:** 2026-09-13  
**PO:** E11; H-009 · Checklist

## Done
1. `ApprovalMatrix` appsettings: objectType × MinAmount → RequiredLevel (1|2).
2. Write-off vượt trần dùng matrix level (không cứng L1).
3. UI wizard 2 bước trên WriteOffButton + `requiredLevel` trong 202.

## Verify
- SprintP10 matrix test: 12k write-off → RequiredLevel 2.

## Non-goals
- Tenant DB matrix (P19/P20); named approvers; cost-confirm → Approval bridge.
