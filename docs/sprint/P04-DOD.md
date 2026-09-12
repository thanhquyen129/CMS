# P04 — UAT Strict close + period lock stress (VPS) DoD

**Ngày:** 2026-09-12  
**PO:** E12; ADR-0010; AC-008 · Checklist `PO-GAP-CHECKLIST.md`

## Done
1. Script `scripts/uat-vps-p04-strict-period-lock.ps1` — stress AC-008 trên VPS.
2. Chạy trên `http://194.233.89.26` → **PASS** (`docs/sprint/P04-UAT-VPS-RESULT.json`).
3. UI note: Strict ép eligibility + period lock (`StartFinancialCloseForm`).

## VPS evidence (PASS)
| Step | Result |
|------|--------|
| Start close `policy=strict` → snapshot | `locked` + policy/snap `strict` + immutable hash |
| Confirm cost / revenue while locked | **409** + `period lock` |
| Allocate payment while locked | **409** + `period lock` |
| Reopen | status `reopened`; snapshot v1 hash unchanged |
| Confirm + allocate/finalize after reopen | **204** / OK |
| Reclose snapshot | v2 appended; v1 hash stable (AC-008) |

Bill UAT: see `P04-UAT-VPS-RESULT.json` (`billNo` `P04-STRICT-*`).

## How to re-run
```powershell
$env:CMS_UAT_EMAIL = "ops@cms.local"   # from host infra/.env — do not commit
$env:CMS_UAT_PASSWORD = "<from host>"
powershell -File scripts/uat-vps-p04-strict-period-lock.ps1
```

## Non-goals
- Soft-off `EnforcePeriodLock` on Controlled (unit-covered Sprint10).
- Per-tenant eligibility tables (P20).
