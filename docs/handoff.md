# Handoff

## 2026-09-12 — Demo seed: đủ case bấm test UI

### User
Seed demo dữ liệu thật tất cả các trường hợp có thể xảy ra để bấm test.

### Done
- `DemoDataSeeder` idempotent (marker bill `DEMO-SEED-MARKER`) vào tenant `ops`.
- Cover: Bill hub / empty; cost+revenue maturity; shared cost draft+finalized; chứng từ Received≠Accepted≠Matched (+ rejected/cancelled); exposure/AP/AR open→partial→settled + write-off; payment/collection draft/finalized/reversed/cancelled; close open/locked(+snapshot)/reopened + period; exceptions/approvals/recon/variance; parties + rate card draft/published.
- Startup: `Demo:SeedOnStartup` (dev default true). Endpoint: `POST /api/dev/seed-demo` (Dev hoặc `Demo:AllowEndpoint=true`).
- Compose/env: `Demo__SeedOnStartup`, `Demo__AllowEndpoint`, `Demo__TenantCode`.

### Files
- `src/LCMS.Application/Demo/DemoDataSeeder.cs`, `DemoOptions.cs`
- `DependencyInjection.cs`, `Program.cs`, `DevAuthEndpoints.cs`
- `appsettings.json`, `appsettings.Development.json`
- `infra/docker-compose.host.yml`, `infra/.env.example`

### How to seed VPS
1. Trong `/opt/cms/infra/.env`: `Demo__SeedOnStartup=true` (hoặc `Demo__AllowEndpoint=true` rồi `POST /api/dev/seed-demo`).
2. `docker compose … up -d api` (recreate). Re-run an toàn — đã có marker thì skip.

### Click map (BillNo)
| Bill | Mục đích |
|------|----------|
| DEMO-01-HUB | Cost/Revenue expected→confirmed→actual |
| DEMO-02-SHARE-A/B | Shared cost + allocation |
| DEMO-03-AP | AP + thanh toán nháp/chốt/đảo |
| DEMO-04-AR | AR + thu tiền |
| DEMO-05-DOCS | Chứng từ đủ triad |
| DEMO-06/07/08-CLOSE-* | Chốt mở / khóa / mở lại |
| DEMO-09-CONTROL | Queue ngoại lệ / phê duyệt |
| DEMO-10-EMPTY | Empty state |

### Verify
- `dotnet build src/LCMS.Api` xanh
- Local Dev: restart API → bills `DEMO-*`; hoặc `POST /api/dev/seed-demo`

---

## 2026-09-12 — Pass UI / Sprint U5 Settlement + Close

### User
UI Settlement + Close (sau U4).

### Done
- `/settlements` — tab Thanh toán / Thu tiền; tạo; detail phân bổ nháp + **chốt phân bổ** (BFF → S8 APIs).
- `/financial-closes` — list/filter; mở chốt (kỳ/Bill); **tạo snapshot** (primary); mở lại; P&L đọc từ snapshot.
- Bill panel: CTA tạo payment/collection + chốt theo Bill; AP/AR copy trỏ `/settlements`.
- Nav + middleware; docs `PROMPT-UI-5.md`, `UI-5-DOD.md`; README/PLAN/orchestration U5.

### Files / API
- UI: `apps/web/app/settlements/**`, `apps/web/app/financial-closes/**`, `lib/settlements.ts`, `lib/financial-closes.ts`, BFF payments/collections/financial-closes, settlement/close components
- API (unchanged): `/api/payments`, `/api/collections`, `/api/financial-closes` (+ snapshot/reopen/pnl)

### Verify
- `npm run build` trong `apps/web`
- Sau deploy: `/settlements`, `/financial-closes`, `/health`

### Next / Follow-ups
- Reverse allocation UI; write-off/recognize; match UI; documents `billId` filter API.

### Residual
- Không có reverse từ UI; list AP/AR trên allocate lọc currency (+ bill soft preference).

---

## 2026-09-12 — U4 local takeover: BFF 201 fix + VPS verify

### User
Implement U4 alone; prior cloud id unreachable. Finish existing branch if present; ship; verify VPS.

### Done
- Confirmed U4 already on `main` (`0953cce` / PR #27): `/documents`, `/ap-ar`, Bill AP/AR panel, receive/accept BFF.
- Fixed BFF `forwardApiMutation`: **201** now forwards JSON body (`{ id }`) so nhận chứng từ redirects to detail (was empty body).
- Docs already mark **Pass UI COMPLETE (U0–U4)**; follow-ups stay match UI + settlement UI.

### Files
- `apps/web/lib/bff-api.ts`

### Verify
- `npm run build` apps/web xanh (local).
- VPS stale web (404 `/documents`) after Actions race — fallback sync `apps/web` + `docker compose build --no-cache web` (never touched `infra/.env` / alogex).
- Smoke: BFF login **200** → `/documents` `/ap-ar` `/documents/receive` **200** + VI markers (Chứng từ / phải trả / Số dư); `/health` `/ready` **200**.

### Residual
- List documents still no `billId` filter on API list DTO.
- Match UI deferred (settlement → U5).

---

## 2026-09-12 — Coordinator: U4 merged — **Pass UI COMPLETE**

### User
Follow-up after [UI-4](bc-9c035d0b-b5a7-44ca-9511-2927f0d3c03d) PR #27.

### Done
- Rebased UI-4 onto main (handoff/orchestration conflicts); CI #80 green on prior tip.
- Merged to `main` → `0953cce` (**Pass UI COMPLETE** U0–U4). Closed PR #27 (history already on main).

### Next
- VPS smoke `/documents` `/ap-ar` after Actions deploy; follow-ups in `UI-4-DOD.md`.

---

## 2026-09-12 — Pass UI / Sprint U4 Documents & AP/AR (thin)

### User
Ship U4: chứng từ Received ≠ Accepted ≠ Matched nhìn thấy; outstanding AP/AR đọc được (thin). Copy CP6.5; không gộp Cost = Payment. Chỉ `apps/web/**` + docs. PR vào main. Pass UI track complete nếu U4 là sprint cuối.

### Done
- `/documents` list + filters (chờ chấp nhận / đã chấp nhận chưa khớp); triad trạng thái VI.
- `/documents/receive` — Nhận chứng từ (BFF → `POST /api/financial-documents`); chỉ đặt Received.
- `/documents/[id]` — detail + Chấp nhận (BFF accept); lines open/matched; không match UI.
- `/ap-ar` — AP / AR outstanding + tab Exposure (read); copy AP ≠ Cost, AR ≠ Revenue.
- Bill detail: panel AP/AR outstanding theo `billId` + CTA nhận chứng từ gắn Bill.
- Nav AppShell + middleware; approval queue deep-link chứng từ khi `objectType` document.
- Docs: `UI-4-DOD.md`; README/orchestration **U4 Done** → **Pass UI COMPLETE**.
- Rebased onto main (incl. U3 VPS verify); PR #27.

### Files / API
- UI: `apps/web/app/documents/**`, `apps/web/app/ap-ar/**`, `lib/documents.ts`, `lib/ap-ar.ts`, components Document*/BillDocuments*, BFF financial-documents
- API (unchanged): financial-documents, accounts-payable/receivable, payable/receivable-exposures, terminology

### Verify
- `npm run build` trong `apps/web`
- Sau merge/Actions: `/documents`, `/ap-ar`, Bill AP/AR block, `/health`

### Next / Follow-ups
- API: list financial-documents thiếu `billId` filter/DTO — không lọc chứng từ theo Bill trên UI.
- Match UI + Settlement/Payment UI (non-goals U4).
- Approve/reject từ queue.

### Residual risks
- List chứng từ trên Bill không filter được (thiếu field API) — CTA + danh sách chung trung thực.
- Recognize/write-off/settlement chưa có UI — chỉ đọc outstanding.

---

## 2026-09-12 — U3 VPS verify + fallback redeploy (local takeover)

### User
Implement U3 Control desk alone; prior cloud id unreachable — pull latest `main`; verify VPS.

### Done
- Confirmed U3 already on `main` (PR #26 → `657be51` / `e5bb257`): `/dashboard`, `/queues/exceptions`, `/queues/approvals`, Dashboard nav live, `UI-3-DOD.md`.
- VPS web was still U2 (dashboard/queues **404**) after Actions race with U4 kickoff.
- Fallback operator redeploy (tar+scp → `compose up -d --build` web/api/proxy); **never** touched `infra/.env` / alogex.
- Smoke: BFF login **200** → dashboard/exceptions/approvals **200** + VI markers; `/health` `/ready` OK.
- `dotnet test Cms.sln -c Release` → **97 passed**.

### Next
- **U4 can start / continue** (cloud [UI-4](bc-9c035d0b-b5a7-44ca-9511-2927f0d3c03d) already kicked off).

---

## 2026-09-12 — Coordinator: U3 merged → U4 kickoff

### User
Follow-up after [UI-3](bc-e514db5a-484e-4333-a372-f8d08e0cdb32) PR #26.

### Done
- CI #76 green; undraft + merge PR #26 → `657be51`.
- Kicked off [UI-4](bc-9c035d0b-b5a7-44ca-9511-2927f0d3c03d) Documents & AP/AR thin.

### Next
- Merge U4 PR → Pass UI COMPLETE.

---

## 2026-09-12 — Pass UI / Sprint U3 Control desk

### User
Ship U3: Dashboard tóm tắt + hàng đợi Exception / Approval (CP6.5 VI). DoD mỏng: `/` hoặc `/dashboard` từ `GET /api/dashboard/summary`; queues từ API Pass 2; link Bill khi có id; empty/loading/error thật. Chỉ `apps/web/**` + docs. PR vào main.

### Done
- `/` → redirect `/dashboard`; summary cards (exception/approval/overdue/bill/variance/close counts) + totals by currency + FX stub roll-up note.
- `/queues/exceptions` (+ `?overdueOnly=1`), `/queues/approvals` — table VI, link `Mở Bill` khi `billId` / `objectType=bill`.
- AppShell nav: Dashboard + Bill + hai hàng đợi (bỏ placeholder “sắp có”).
- Docs: `UI-3-DOD.md`; README Pass UI U3 Done; orchestration U3 Done / U4 Ready.

### Files / API
- UI: `apps/web/app/dashboard/**`, `apps/web/app/queues/**`, `apps/web/lib/control-desk.ts`, `AppShell`, `middleware`, `globals.css`
- API (unchanged): `GET /api/dashboard/summary`, `/api/queues/exceptions`, `/api/queues/approvals`, `/api/terminology`

### Verify
- `npm run build` trong `apps/web` (pre-merge)
- Sau merge/Actions: `http://194.233.89.26/dashboard` + `/queues/*` + `/health`

### Next
- Merge U3 PR → kickoff U4 Documents & AP/AR thin.
- Follow-up: approve/reject UI; deep-link cost/revenue từ approval object.

### Residual risks
- Approval `objectType` ≠ bill: không có màn chi tiết — hiện “—” (trung thực).
- Counts/totals là projection read-only (ADR-0011); không SoT.

---

## 2026-09-12 — Coordinator: U2 landed → U3 kickoff

### User
Follow-up after [UI-2](bc-5377dafd-4e8b-4761-801f-ae76de6079b7) completed (PR #24).

### Done
- Rebased UI-2 onto main (conflict handoff/orchestration); force-pushed branch; CI green then commits landed on `main` (`01d8a50` / `c6210b7`). PR #24 closed (not squash-merged — history already on main).
- Pass 2 already COMPLETE on main; kicked off [UI-3](bc-e514db5a-484e-4333-a372-f8d08e0cdb32) Control desk.

### Next
- Merge U3 PR when green; then U4.

---

## 2026-09-12 — Pass 2 Sprint 12 FULL Hardening & UAT (**Pass 2 COMPLETE**)

### User
Follow-up after Pass2 Sprint12 Hardening Full — merge final Pass 2 sprint.

### Done
- Merged `cursor/sprint-12-full-hardening-uat-cb6e` onto `main` (S11 already on main).
- Audit richer before/after JSON; cover finalize, document accept/match, AP/AR recognize, write-off; audit filters `from`/`to`.
- Integration recovery: mark-retried / dead-letter; duplicate 409 C-002; outbox enqueue + process-once.
- Rate-limit money paths; AC-007/008/009 smoke; VI gaps; `SPRINT-12-FULL-DOD.md` → **Pass 2 COMPLETE**.
- README Pass 2 board = all Done.

### Files / API
- `/api/integration-errors*`, `/api/outbox*`; migration `Sprint12Full_HardeningRecovery`
- DoD: `docs/sprint/SPRINT-12-FULL-DOD.md`

### Verify
- `dotnet test Cms.sln -c Release` → **97 passed** (S12 FULL +4 on main after S11)

### Next
- Pass UI continues (U3+). Residual backlog in SPRINT-12-FULL-DOD only.

---

## 2026-09-12 — Pass UI / Sprint U2 Cost & Revenue confirm

### User
Ship U2: trên Bill detail xác nhận Chi phí / Doanh thu (CTA rõ; maturity không silent overwrite). List + confirm/actualize dialog VI; refresh profile; 403/409 honest. Ship per 06. Chỉ `apps/web` + docs.

### Done
- Bill detail: list `GET /api/costs?billId=` + `GET /api/revenues?billId=`.
- Actions: Xác nhận (Expected→Confirmed) primary; Ghi nhận Thực tế (Confirmed→Actual) secondary; dialog VI giải thích không ghi đè lớp trước.
- BFF cookie→Bearer: `/bff/costs|revenues/[id]/confirm|actualize`; lỗi API VI; 403/409 khóa nút + refresh; `router.refresh` sau mutate.
- Docs: `UI-2-DOD.md`; README/orchestration U2 Done.
- Rebased onto main (after CI smoke redirect fix).

### Files / API
- UI: `BillCostRevenuePanel`, `bills/[id]/page.tsx`, `lib/costs-revenues*.ts`, `lib/bff-api.ts`, `lib/terminology.ts`, `app/bff/costs/**`, `app/bff/revenues/**`
- API unchanged (confirm/actualize đã có trên main)

### Verify
- `npm run build` (apps/web) xanh; `dotnet test` **93 passed** (pre-S12 push; S12 later → 97)
- VPS: login BFF 200 → `/bills` → Bill detail shows `maturity-actions` / Xác nhận / Ghi nhận; `POST /bff/costs/{id}/confirm` → **204**; `/health` OK
- Actions: U2 on `main` (`c6210b7`); run #72 cancelled by S12 concurrency — later deploy (post-S12 / UI-3 kickoff) rebuilt `cms-web` with U2

### Next
- U3 Control desk (already kicked off on Actions)
- Optional amount override trong dialog

### Follow-ups
- Permission codes riêng cho confirm (hiện 403 runtime)
- Shared allocation UI (non-goal U2)
- 409 cũng khóa nút + refresh (U2 polish)

---

## 2026-09-12 — Fix CI deploy smoke: follow `/` → `/login` redirect

### User
Những lỗi CI trên Actions (run đỏ liên tiếp) — tự fix luôn chứ?

### Answer
- VPS `/health` `/ready` OK; app chạy. Fail ở **deploy Health check**, không phải `dotnet test`.
- Root cause (từ UI-0): unauthenticated `/` trả **307 → `/login`**. Smoke dùng `curl -fsS` **không** follow redirect → body chỉ `/login` (6 bytes) → grep HTML fail → đỏ từ #61 trở đi (sau JWT fix #58–#60).
- Fix: `curl -fsSL` + kiểm tra marker `Đăng nhập|CMS` trên HTML sau redirect. JWT SigningKey trên host đã đủ từ trước.

### Files
- `.github/workflows/ci.yml` (Health check)
- `docs/ops/github-actions.md`

### Verify
- Local repro: `curl -fsS /` → no HTML; `curl -fsSL /` → login HTML.
- Actions: push main → CI `deploy` smoke xanh; http://194.233.89.26/health

### Next
- Re-run cũ không bắt buộc; run mới sau push là đủ.

---

## 2026-09-12 — Coordinator: S11 FULL merged → S12 FULL kickoff

### User
Follow-up after Pass2 Sprint11 Profile Full cloud agent completed.

### Done
- Merged Sprint 11 FULL (`cursor/sprint-11-full-financial-profile-reporting-31b9`); suite target **93**.
- README: S11 Done; S12 In progress (last Pass 2 FULL).
- Added `docs/sprint/PROMPT-SPRINT-12-FULL.md`.
- Launched cloud agent Pass2 Sprint12 Hardening Full.

### Next
- Merge Sprint 12 FULL PR → declare **Pass 2 COMPLETE**.

---

## 2026-09-12 — Sprint 11 FULL Financial Profile & Reporting (Pass 2)

### User
Pass 2 Sprint 11 FULL: asOf maturity reconstruct; dashboard FX roll-up + variance/overdue counts; queue filters; close snapshot P&L; tests + DoD + ADR + PR. Never secrets/alogex.

### Done
- Financial profile: asOf reconstructs Confirmed/Actual via `ConfirmedAt`/`ActualizedAt`; settlement outstanding from finalized allocations at asOf; residual limits on DTO note + DoD.
- Dashboard: `OpenVarianceCount`, `OverdueExceptionCount`; optional `BaseCurrencyRollUp` via Cost/Revenue FX stub (ADR-0011).
- Queues: exceptions (status/severity/overdue/objectType), approvals (status/objectType/requiredLevel), `GET /api/queues/reconciliations` stub.
- Close P&L: `GET /api/financial-closes/{id}/pnl` derived from immutable snapshot metrics only.
- Derived read-only — no totals written onto Bill. VI labels/errors.
- Tests: `Sprint11FullFinancialProfileReportingTests` (4); suite **93 passed**.

### Files / API
- Profile: `GetBillFinancialProfileQuery`
- Dashboard/queues: `GetDashboardSummaryQuery`, `ControlQueueQueries`, `DashboardReportingEndpoints`
- P&L: `GetFinancialClosePnlQuery` + `FinancialCloseEndpoints`
- ADR: `docs/adr/ADR-0011-dashboard-fx-rollup-close-pnl.md`
- DoD: `docs/sprint/SPRINT-11-FULL-DOD.md`

### Verify
- `dotnet test Cms.sln -c Release` → **93 passed**

### Deferred / Next
- Pass 2 Sprint 12 Hardening FULL
- Real FX table; Next.js dashboard UI

---

## 2026-09-12 — CI concurrency + Pass2∥UI ship rules

### User
Làm những gì tốt nhất cho agent Sprint và Agent U chạy song song.

### Done
- `.github/workflows/ci.yml`: `concurrency` per ref + `cancel-in-progress` — hết dual deploy race trên VPS.
- `README-AGENTS.md` + `PASS-UI-ORCHESTRATION.md`: quy tắc serialize merge/deploy; PR ưu tiên khi Pass 2 ∥ Pass UI.

### Verify
- Actions: run mới hủy run cũ cùng `main`; `/health` sau deploy.

### Next
- U2 cloud in progress: [UI-2](bc-5377dafd-4e8b-4761-801f-ae76de6079b7). S11 merge tuần tự khi PR sẵn (không đua push `main`).

---

## 2026-09-12 — Pass UI / Sprint U1 Bill hub

### User
Ship Bill hub: `/bills` list+search → `/bills/[id]` financial profile (Expected/Confirmed/Actual) + profitability. Enable Bill nav. DoD + handoff + ship. No Cost mutate (U2).

### Done
- `/bills` list + `?q=` search via existing `GET /api/bills`; auth Bearer from `lcms_at` cookie.
- `/bills/[id]` loads `financial-profile` + `profitability?view=best`; maturity table scannable; money `vi-VN`.
- Terminology labels; empty/error/loading; AppShell Bill link live; Dashboard still “sắp có”.
- No new backend — APIs already on main. Docs: `UI-1-DOD.md`, README/orchestration U1 Done.

### Files / API
- UI: `apps/web/app/bills/**`, `lib/bills.ts`, `lib/money.ts`, `components/AppShell.tsx`
- API (unchanged): `GET /api/bills`, `/api/bills/{id}`, `.../financial-profile`, `.../profitability`, `/api/terminology`

### Verify
- Local: `dotnet test` **89 passed**; `npm run build` (web) OK
- VPS after deploy (`4aeab7a`): login OK; `/bills` 307→login then 200; search `?q=`; detail maturity Dự kiến/Đã xác nhận/Thực tế; `/health` OK
- Actions: https://github.com/thanhquyen129/CMS/actions

### Next
- U2 Cost & Revenue confirm actions on Bill

---

## 2026-09-12 — Coordinator: S10 FULL merged → S11 FULL kickoff

### User
Follow-up after Pass2 Sprint10 Close Full cloud agent completed.

### Done
- Merged Sprint 10 FULL (`cursor/sprint-10-full-financial-close-4de8`); suite target **86**.
- README: S10 Done; S11 In progress.
- Added `docs/sprint/PROMPT-SPRINT-11-FULL.md`.
- Launched cloud agent Pass2 Sprint11 Profile Full.

### Next
- Merge Sprint 11 FULL PR → kickoff Sprint 12 Hardening FULL.

---

## 2026-09-12 — Sprint 10 FULL Financial Close (Pass 2)

### User
Pass 2 Sprint 10 FULL: eligibility checklist (critical exceptions / unmatched accepted docs / unsettled AP-AR threshold); period lock on confirm + allocate/finalize; Controlled vs Strict policy; reopen/reclose append-only; tests + SPRINT-10-FULL-DOD + ADR + PR. Never secrets/alogex. Vietnamese errors.

### Done
- Eligibility checklist (`FinancialClose:Eligibility`, default on): three gates with distinct VI Conflict reasons; Strict forces all gates.
- Period lock (`FinancialClose:EnforcePeriodLock` default true): Locked close blocks Cost/Revenue confirm and Payment/Collection allocate/finalize in scope; Strict always enforces.
- Policy `controlled` | `strict` stored on close + copied to snapshot; reopen/reclose never mutates old snapshots (C-010 / AC-008).
- ADR-0010; VI terms; `SPRINT-10-FULL-DOD.md`; Pass 1 DoD deferred marked done for eligibility/period lock.
- Tests: `Sprint10FullFinancialCloseTests` (3); suite **86 passed**.

### Files / API / Config
- Application: `FinancialCloseOptions`, `CloseEligibilityChecker`, `PeriodLockGate`; snapshot command uses checker; confirm/allocate/finalize call period lock.
- Config: `FinancialClose` section in `appsettings.json`
- ADR: `docs/adr/ADR-0010-financial-close-eligibility-period-lock.md`
- DoD: `docs/sprint/SPRINT-10-FULL-DOD.md`

### Verify
- `dotnet test Cms.sln -c Release` → **86 passed**

### Deferred / Next
- Pass 2 Sprint 11 Profile/Reporting FULL
- Per-tenant eligibility tables; Next.js close UI

---

## 2026-09-12 — CI #58–#60 deploy fail: missing JWT signing key on VPS

### User
Những lỗi CI trên main giải quyết thế nào? Fix ngay hay đợi build sau?

### Answer
- Fail ở job **deploy** / step **Compose up on host**, không phải job `test` (PR xanh vì không chạy deploy).
- Root cause: `/opt/cms/infra/.env` thiếu `Auth__Jwt__SigningKey` (compose bắt buộc kể từ Sprint 0 JWT). Đợi push sau **không** hết — cùng lỗi.
- Fixed ngay trên `cms-sg-01`: thêm SigningKey + khôi phục `LCMS_DB_PASSWORD`; `compose up -d --build`. Verify `/health` `/ready` `/api/terminology` OK; `/` → 307 (web).
- Secret chỉ trên host — không commit. Re-run Actions (optional) sẽ xanh sau khi `.env` đủ.

### Next
- Optional: Re-run failed workflow trên Actions tab để ghi nhận xanh.
- Operator: set `Auth__Bootstrap__*` trên host nếu cần login UI-0.

---

## 2026-09-12 — Pass UI / Sprint U0 Scaffold + login + proxy

### User
Ship Next.js to VPS: `/` = Vietnamese UI shell + login (not API JSON). Proxy `/api`→API. JWT per ADR-0002. BFF httpOnly cookie (ADR-0007).

### Done
- `apps/web` Next.js 15 App Router: `/login`, `/` shell; Bill/Dashboard “sắp có”; terminology VI labels.
- API: `POST /api/auth/login`; `users.password_hash`; env bootstrap `Auth:Bootstrap:*`; `JwtTokenIssuer`.
- Infra: `Dockerfile.web`, nginx proxy (`/`→web, `/api|/health|/ready|/metrics`→api); API no longer on host `:80`.
- ADR-0007 BFF cookie; CI post-deploy smoke includes HTML `/` + terminology.
- Tests: `Ui0AuthLoginTests` (3).

### Files / API / Config
- UI: `apps/web/**`; BFF `/bff/auth/*`
- API: `/api/auth/login`; migration `UI0_UserPasswordHash`
- Compose: `web` + `proxy` services; bootstrap env on host
- ADR: `docs/adr/ADR-0007-bff-httponly-jwt-cookie.md`
- DoD: `docs/sprint/UI-0-DOD.md`

### Host note
Set `Auth__Bootstrap__Email` / `Auth__Bootstrap__Password` in `infra/.env` on VPS so login works (never commit).
After U0 deploy: seeded `ops@cms.local` on host (password only in `infra/.env` — rotate as needed).

### Verify (prod)
- `/` → 307 → `/login` HTML (nginx → web)
- `/health` + `/ready` OK
- `/api/terminology` OK
- `POST /api/auth/login` + `/bff/auth/login` OK with bootstrap user

### Next
- U1 Bill hub (`PROMPT-UI-1.md`)
- Operator: confirm/rotate bootstrap password on host

---

## 2026-09-12 — Coordinator: S9 FULL merged → S10 FULL kickoff

### User
Follow-up after Pass2 Sprint9 Control Full cloud agent completed.

### Done
- Merged Sprint 9 FULL (`cursor/sprint-9-full-financial-control-6616`); suite target **83**.
- README: S9 Done; S10 In progress.
- Added `docs/sprint/PROMPT-SPRINT-10-FULL.md`.
- Launched cloud agent Pass2 Sprint10 Close Full.

### Next
- Merge Sprint 10 FULL PR → kickoff Sprint 11 Profile/Reporting FULL.

---

## 2026-09-12 — Sprint 9 FULL Financial Control (Pass 2)

### User
Pass 2 Sprint 9 FULL: reconciliation batch + auto variance severity thresholds; exception SLA/inbox + multi-step approval stub (approval ≠ permission); optional block Cost/Revenue confirm on critical open exception; tests + SPRINT-9-FULL-DOD + PR. Never secrets/alogex. Vietnamese errors.

### Done
- Batch recon: `POST /api/reconciliations/{id}/details/batch`; shared writer auto-creates Variance (not Exception) with severity from thresholds.
- Exception: object link, default SLA hours, escalate stub, inbox filters (`status`/`severity`/`objectType`/`overdueOnly`).
- Approval: `requiredLevel` 1|2 multi-step; reject requires reason; never touches Permission.
- Confirm block: `FinancialControl:BlockConfirmOnCriticalException` default true for Cost/Revenue.
- Migration `Sprint9Full_FinancialControl`; ADR-0009; VI terms.
- Tests: `Sprint9FullFinancialControlTests` (3); suite **83 passed**.

### Files / API / Config
- APIs: details/batch; exceptions escalate + filters; approvals requiredLevel
- Application: FinancialControlOptions, VarianceSeverityCalculator, CriticalExceptionConfirmGate, ReconciliationDetailWriter
- Config: FinancialControl section in appsettings.json
- Migration: 20260912023712_Sprint9Full_FinancialControl
- ADR: docs/adr/ADR-0009-financial-control-severity-sla-approval.md
- DoD: docs/sprint/SPRINT-9-FULL-DOD.md

### Verify
- `dotnet test Cms.sln -c Release` → **83 passed**

### Deferred / Next
- Pass 2 Sprint 10 FULL Financial Close
- Auto variance→exception; per-tenant SLA tables; Next.js inbox UI

---

## 2026-09-12 — Coordinator: S8 FULL merged → S9 FULL kickoff

### User
Follow-up after Pass2 Sprint8 Settle Full cloud agent completed.

### Done
- Merged Sprint 8 FULL onto `main`; verified `dotnet test` → **80 passed**.
- README Pass 2: S8 Done; S9 In progress.
- Added `docs/sprint/PROMPT-SPRINT-9-FULL.md`; pushed `325d80d`.
- Launched cloud agent Pass2 Sprint9 Control Full.

### Next
- Merge Sprint 9 FULL PR when ready → kickoff Sprint 10 Close FULL.

---

## 2026-09-12 — Sprint 8 FULL Settlement (Pass 2)

### User
Pass 2 Sprint 8 FULL: unapplied cash + multi-allocation; C-008 over-allocate reject; FX stub base_amount on settlement; write-off stub with reason; idempotent finalize; tests + SPRINT-8-FULL-DOD + PR. Never secrets/alogex. Vietnamese errors.

### Done
- Unapplied / AvailableToAllocate: multi-AP/AR allocate from one payment/collection; subsequent allocate until fully applied; over → 409 C-008 VI.
- FX stub: `Settlement:BaseCurrency` + `StubFxRatesToBase` fills `base_amount` on create/allocate; `fx_rate_id` null (ADR-0004/0008); missing rate → VI validation.
- Write-off: `POST /api/accounts-payable|accounts-receivable/{id}/write-off` — negative AdjustmentAmount + `[xóa nợ]` note; capped by `MaxWriteOffAmount`; never silent wipe / never invents Cost/Revenue.
- Idempotent finalize: re-finalize finalized allocation → 204 no-op; reversed → 409 VI.
- Migration `Sprint8Full_Settlement`; ADR-0008; VI terms WRITE_OFF, SETTLEMENT_BASE_AMOUNT, IDEMPOTENT_FINALIZE.
- Tests: `Sprint8FullSettlementTests` (3); suite **80 passed**.

### Files / API / Config
- APIs: write-off endpoints; GET payment/collection + baseAmount/fxRateId; finalize idempotent
- Application: Settlements/SettlementOptions, SettlementFxStub; create/allocate/finalize; WriteOff* commands
- Config: Settlement section in appsettings.json
- Migration: 20260912022427_Sprint8Full_Settlement
- ADR: docs/adr/ADR-0008-settlement-fx-writeoff.md; ADR-0004 extended

### Verify
- `dotnet test Cms.sln -c Release` → **80 passed**

### Deferred / Next
- Pass 2 Sprint 9 FULL Financial Control / reconciliation
- Bank feed; real fx_rates; write-off approval

---

## 2026-09-12 — Pass UI plan (parallel track)

### User
Lên kế hoạch chạy UI ngay — http://194.233.89.26/ hiện chỉ JSON API.

### Done
- Plan: docs/sprint/PLAN-UI.md — U0→U4 song song Pass 2 FULL; nginx proxy /→Next.js, /api→API; login JWT mỏng (ADR-0007 trong U0).
- Prompts: PROMPT-UI-0.md … PROMPT-UI-4.md.
- docs/sprint/README-AGENTS.md cập nhật Status Pass UI = Ready (start U0).
- Resolved merge conflict with Sprint 7 FULL (aging ADR-0006).

### Next
- Kickoff **U0**: paste PROMPT-UI-0.md vào New Chat / cloud agent riêng.
- Pass 2: paste PROMPT-SPRINT-8-FULL.md cho Settlement FULL.
- Sau U1: Bill list + financial profile trên VPS.

---

## 2026-09-12 — Sprint 7 FULL Exposure + AP/AR (Pass 2)

### User
Pass 2 Sprint 7 FULL: partial/multi recognition + recognized_amount; aging buckets on AP/AR; optional document→exposure link (no Cost/Revenue); outstanding respects settlements; tests + SPRINT-7-FULL-DOD + PR. Never secrets/alogex. Vietnamese errors.

### Done
- Multi-recognize: 
ecognized_amount recomputed from sum of AP/AR slices before each recognize; over-recognize / already-full → 409 VI; GET exposure returns 
ecognitions[].
- Aging (ADR-0006): daysPastDue + gingBucket on AP/AR DTOs; GET /api/accounts-payable/aging and …/accounts-receivable/aging with bucket summary.
- Document→exposure: create with inancialDocumentId + POST /api/{payable|receivable}-exposures/{id}/link-document; direction mismatch rejected; never invents Cost/Revenue.
- Outstanding: draft payment allocation unchanged; finalize reduces outstanding (AC-007 / C-015) — covered in FULL aging/settlement test.
- VI terms: AGING, AGING_BUCKET, DAYS_PAST_DUE, LINK_DOCUMENT, RECOGNIZED_AMOUNT.
- Tests: Sprint7FullExposureApArTests (3); suite **74 passed**.
- DoD: docs/sprint/SPRINT-7-FULL-DOD.md; prompt: PROMPT-SPRINT-7-FULL.md; ADR-0006.

### Files / API
- APIs: link-document; /api/accounts-payable/aging; /api/accounts-receivable/aging; exposure GET + recognitions; AP/AR aging fields
- Application: Exposures/AgingBuckets; link commands; recognize recompute; aging queries
- ADR: docs/adr/ADR-0006-ap-ar-aging-buckets.md
- No new EF migration (derived fields + existing inancial_document_id)

### Verify
- dotnet test Cms.sln -c Release → **74 passed**

### Deferred / Next
- Pass 2 Sprint 8 FULL Settlement
- Next.js aging UI (Pass UI U4+)

---

## 2026-09-12 — Sprint 6 FULL Financial Documents (Pass 2)

### User
Pass 2 Sprint 6 FULL: match methods line_to_line/line_to_cost/line_to_revenue (link-only C-003/C-004); tolerance policy; accept-before-match; duplicate control; reverse/cancel match detail; tests + SPRINT-6-FULL-DOD + PR. Never secrets/alogex. Vietnamese errors.

### Done
- Match methods enforce target shape; Cost/Revenue links never invent economic rows.
- Tolerance: Documents:DefaultToleranceAbsolute / DefaultTolerancePercent (+ per-match override); C-007 beyond effective tolerance → 409 VI.
- Accept-before-match gate (RequireAcceptBeforeMatch default true); soft cancel/void document + cancel match session; reverse match detail restores open amounts (RV-003).
- Duplicate control on active (type, document_no, counterparty) — IDX-006 / BR-FIN-023.
- GET /api/financial-documents/open-amounts; reverse/cancel APIs.
- Migration Sprint6Full_FinancialDocuments; ADR-0005.
- Tests: Sprint6FullFinancialDocumentTests (3); Pass 1 Sprint6 updated for methods/accept; suite **74 passed**.
- DoD: docs/sprint/SPRINT-6-FULL-DOD.md.

### Files / API / Config
- APIs: /api/document-matches (methods+tolerance), .../details/{id}/reverse, .../cancel; /api/financial-documents/open-amounts, .../{id}/cancel
- Application: DocumentOptions, match/receive/cancel/reverse commands; queries open amounts
- Config: Documents section in ppsettings.json
- Migration: 20260912020613_Sprint6Full_FinancialDocuments
- ADR: docs/adr/ADR-0005-document-match-tolerance-accept.md

### Verify
- dotnet test Cms.sln -c Release → **74 passed**

### Deferred / Next
- Pass 2 Sprint 7 FULL Exposure/AP/AR
- Auto-match engine; e-invoice; Next.js Documents UI

---

## 2026-09-11 — Sprint 5 FULL Revenue & Profitability (Pass 2)


### User
Pass 2 Sprint 5 FULL: Revenue FX stub + optional confirm approval threshold (parity Cost FULL); profitability view API + profile enhancements; C-004 hardened; tests green; SPRINT-5-FULL-DOD + PR. Never secrets/alogex. Vietnamese errors.

### Done
- FX stub: `Revenue:BaseCurrency` + `StubFxRatesToBase` fills `base_amount` on create/confirm/actualize/adjust (ADR-0004); `fx_rate_id` null until real FX table; missing rate → VI validation.
- Optional confirm gate: `Revenue:ConfirmApprovalThresholdBase` → pending + VI 409 until Sprint 9 `/api/approvals` approve (`objectType=revenue`).
- Profile: Expected vs Actual variance fields; allocated cost included in CostBestAvailable; multi-currency never summed raw.
- `GET /api/bills/{id}/profitability?view=expected|confirmed|actual|best` (default best); confirmed/actual layers missing → 0 for honesty.
- C-004 harden: reject `document` / `accounts_receivable` / aliases (`ar`, `doc`, `financial_document`); defense in depth in handler.
- GET revenue exposes `baseAmount`, `fxRateId`.
- VI terms: REVENUE_CONFIRM_APPROVAL_THRESHOLD, BILL_PROFITABILITY, PROFITABILITY_VIEW, VARIANCE_EXPECTED_VS_ACTUAL.
- Tests: `Sprint5FullRevenueProfitabilityTests` (3); suite **71 passed**.
- DoD: `docs/sprint/SPRINT-5-FULL-DOD.md`; prompt: `PROMPT-SPRINT-5-FULL.md`.

### Files / API / Config
- APIs: `/api/revenues`, `/api/revenues/{id}/confirm`, `/api/bills/{id}/financial-profile`, `/api/bills/{id}/profitability`
- Application: `Revenues/RevenueOptions`, `RevenueFxStub`, `RevenueApprovalGate`; maturity/create/adjust; `GetBillProfitabilityQuery`
- Config: `Revenue` section in `appsettings.json`
- ADR: `docs/adr/ADR-0004-cost-fx-stub-approval-threshold.md` (Cost+Revenue)
- No new EF migration (schema already had base_amount / fx_rate_id)

### Verify
- `dotnet test Cms.sln -c Release` → **71 passed**
### Deferred / Next
- Pass 2 Sprint 6 FULL Documents
- Real fx_rates table; per-tenant threshold; Next.js Revenue UI

---

## 2026-09-11 — Sprint 4 FULL Cost (Pass 2)

### User
Pass 2 Sprint 4 FULL: allocation bases equal/quantity/manual_ratio; C-005/C-006; reallocation supersedes history; Shared vs Direct rules; FX stub base_amount; optional approval threshold before confirm; tests + SPRINT-4-FULL-DOD + PR. Never secrets/alogex. Vietnamese errors.

### Done
- Allocation bases locked: `equal` | `quantity` | `manual_ratio` (C-006); conservation on finalize (C-005); reallocation marks prior finalized as `superseded` + `supersedes_allocation_id`.
- Shared vs Direct harden: Direct requires Bill; Shared Bill null; allocate Shared only.
- FX stub: `Cost:BaseCurrency` + `StubFxRatesToBase` fills `base_amount` on create/confirm/actualize/adjust/seed (ADR-0004); `fx_rate_id` null until real FX table.
- Optional confirm gate: `Cost:ConfirmApprovalThresholdBase` → pending + VI 409 until Sprint 9 approve.
- GET cost exposes `baseAmount`, `fxRateId`, allocation `supersedesAllocationId`.
- VI terms: ALLOCATION_BASIS_*, BASE_AMOUNT, FX_STUB_RATE, COST_CONFIRM_APPROVAL_THRESHOLD.
- Tests: `Sprint4FullCostTests` (3); suite **63 passed**.
- DoD: `docs/sprint/SPRINT-4-FULL-DOD.md`; prompt: `PROMPT-SPRINT-4-FULL.md`.

### Files / API / Config
- APIs: `/api/costs`, `/api/costs/{id}/confirm`, `/api/costs/{id}/allocations`, `/api/cost-allocations/{id}/finalize`
- Application: `Costs/CostOptions`, `CostFxStub`, `CostApprovalGate`; allocation/maturity/create/adjust/seed commands
- Config: `Cost` section in `appsettings.json`
- ADR: `docs/adr/ADR-0004-cost-fx-stub-approval-threshold.md`
- No new EF migration (schema already had base_amount / fx_rate_id / supersedes)

### Verify
- `dotnet test Cms.sln -c Release` → **63 passed**

### Deferred / Next
- Pass 2 Sprint 3 FULL (Rate) if not yet merged; Sprint 5 FULL Revenue
- Real fx_rates table; per-tenant threshold; Next.js Cost UI

---

## 2026-09-11 — Sprint 2 FULL Operational Reference (Pass 2)

### User
Pass 2 Sprint 2 FULL: transport_legs / transport_movements + bill_leg_links / leg_movement_links / bill_movement_links + migration; upsert APIs; expand bill graph; operational search within tenant; C-002 idempotency; JWT/data scope on lists; tests + SPRINT-2-FULL-DOD + handoff + PR. Never secrets/alogex. Vietnamese errors.

### Done
- Domain D03 depth: `TransportLeg` (Shipment 1:N), `TransportMovement`, bridges `BillLegLink`, `LegMovementLink`, `BillMovementLink`.
- Migration `Sprint2Full_TransportLegsMovements` (C-002 unique on leg/movement external identity).
- CQRS/API: upsert leg/movement; link bill↔leg, leg↔movement, bill↔movement (idempotent).
- `GET /api/bills/{id}/graph` now returns Legs + Movements (direct bridges ∪ shipment legs ∪ movements via legs).
- Search: `GET /api/search/operational?q=` + `GET /api/bills?q=` (bill_no / bill external_id / order external_id); respects `bill.read` Data Scope.
- VI validation/errors; terminology keys for operational graph.
- Tests: `Sprint2FullOperationalReferenceTests` (idempotency+graph; search isolation; JWT data-scope; cross-tenant 404).
- DoD: `docs/sprint/SPRINT-2-FULL-DOD.md`.

### Files / API
- APIs: `/api/transport-legs`, `/api/transport-movements`, `/api/search/operational`, expanded `/api/bills/{id}/graph`, `/api/bills?q=`
- Endpoints: `OperationalReferenceEndpoints`, `TenantBillEndpoints`
- Migration: `20260911203703_Sprint2Full_TransportLegsMovements`

### Verify
- `dotnet test Cms.sln -c Release` → **60 passed** (4 new Sprint 2 FULL; Sprint 1 FULL was 56)

### Deferred / Next
- Pass 2 Sprint 3 FULL (Rate & Pricing depth)
- Full-text search index; Order/Shipment list Data Scope; Next.js UI

---

## 2026-09-11 — Sprint 1 FULL Identity + Master Data (Pass 2)

### User
Pass 2 Sprint 1 FULL: Permission × Data Scope independent (`all`/`organization`/`own`) on Bill+Cost list/get; JWT `sub` actor; inactive users denied; party_roles + APIs; org tree/children; currency harden; tests + SPRINT-1-FULL-DOD + handoff + PR. Base after Sprint 0 FULL JWT. Never secrets/alogex. Vietnamese errors.

### Done
- Data Scope matrix on `role_permissions` (`all` > `organization` > `own`); enforced on Bill/Cost list+get (`bill.read` / `cost.read`).
- `organization_id` on users/bills/costs; org subtree for organization scope; `CreatedBy` stamped from JWT `sub` / Dev header.
- Inactive registered users → VI 403 «Tài khoản không còn hiệu lực.»
- `party_roles` + assign/list/revoke APIs; org `tree` + `children`; currency seed VND/USD/EUR + get-by-code + inactive reject + baseline decimals.
- `POST /api/roles/{id}/permissions` with dataScope; `PUT /api/users/{id}`.
- Migration `Sprint1Full_IdentityMasterData`; ADR-0003; `PROMPT-SPRINT-1-FULL.md` + `SPRINT-1-FULL-DOD.md`.
- Tests: `Sprint1FullIdentityMasterTests` (6) — suite **56 passed**.

### Files / API
- APIs: `GET /api/bills`; role permissions; user update; org tree/children; party roles; `GET /api/currencies/{code}`
- Identity: `PermissionService`, `OrganizationHierarchyService`, `DataScopeAccess`
- Migration: `20260911202508_Sprint1Full_IdentityMasterData`
- ADR: `docs/adr/ADR-0003-permission-data-scope.md`

### Verify
- `dotnet test Cms.sln -c Release` → **56 passed**

### Deferred / Next
- Data Scope on Revenue/Documents/AP-AR; OIDC; multi-org membership; Pass 2 Sprint 2 FULL

---

## 2026-09-11 — Sprint 0 FULL Foundation (Pass 2)

### User
Pass 2 Sprint 0 FULL: JWT Bearer (`tenant_id` + `sub`); Production disables header bootstrap; Dev `POST /api/dev/token` + optional `X-Tenant-Id`; observability enrichment + Prometheus `/metrics`; tests JWT + Production 401; SPRINT-0-FULL-DOD + handoff + PR. Never secrets/alogex. Vietnamese errors remain.

### Done
- JWT Bearer auth (`Auth` options + ADR-0002). Claims `tenant_id` + `sub` drive `ITenantContext` / `ICurrentUserContext`.
- Production defaults: `RequireJwt=true`, `AllowHeaderBootstrap=false`. Dev: headers allowed; `POST /api/dev/token`.
- Serilog LogContext + request log enrich TenantId/UserId/CorrelationId.
- `/metrics` = Prometheus text (`prometheus-net` HTTP metrics); anonymous.
- Vietnamese JWT 401 challenge JSON (`unauthorized`).
- Rate-limit remains in-process fixed window (Sprint 12) — single-node only; Redis deferred (documented).
- Tests: `Sprint0FullJwtAuthTests` (Dev JWT path, Production 401, Production JWT + header ignored).
- DoD: `docs/sprint/SPRINT-0-FULL-DOD.md`.

### Ops before Production deploy
- Set `Auth__Jwt__SigningKey` (≥32 chars) in host `infra/.env` (compose requires it). Never commit the real value.
- Dev token helper: `docs/ops/dev-jwt-token.md`.

### Files / API
- Auth: `src/LCMS.Api/Auth/*`, `TenantResolutionMiddleware`, `Program.cs`
- Dev: `POST /api/dev/token`
- Config: `appsettings*.json`, `infra/.env.example`, `infra/docker-compose.host.yml`
- ADR: `docs/adr/ADR-0002-jwt-bearer-tenant-claims.md`

### Verify
- `dotnet test Cms.sln -c Release` → **50 passed** (4 new Sprint 0 FULL; Sprint 12 was 46)

### Deferred / Next
- OIDC IdP; refresh/revocation; OTel exporter; Redis rate-limit; Pass 2 Sprint 1+ domain depth

---

## 2026-09-11 — Sprint 12 Hardening & UAT (E14/E15/E16) — **Pass 1 COMPLETE**

### User
Implement Sprint 12 Pass 1 final slice only: audit_events + writes on key money mutations + GET /api/audit-events; integration_records C-002 upsert stub; rate-limit + security headers smoke; Vietnamese terminology coverage + DoD declaring Pass 1 COMPLETE; tests green; handoff + PR to main. Non-goals: full outbox, load tests, JWT, Next.js UAT. Never secrets/alogex.


### Done
- Domain D12: `AuditEvent`, `IntegrationRecord`, `IntegrationError` (UUIDv7, soft-delete, tenant_id).
- Migration `Sprint12_HardeningAudit` (IDX-012/013).
- Audit writer on: cost create/confirm, revenue create, payment_allocation finalize, financial_close_snapshot create (actor/action/object/correlation).
- APIs: `GET /api/audit-events`; `POST|GET /api/integration-records` (duplicate → 409 C-002).
- Middleware: `SecurityHeadersMiddleware`, `RateLimitingMiddleware` (fixed window `/api/*`, config-driven); `/health` `/ready` + correlation still OK.
- VI terms extended (AUDIT_EVENT, INTEGRATION_RECORD, CORRELATION_ID, RATE_LIMIT, …); coverage test vs Sprints 1–11 keys.
- Tests: **46 passed** (3 new Sprint 12).
- DoD: `docs/sprint/SPRINT-12-DOD.md` declares **Pass 1 COMPLETE** + Pass 2 backlog pointers.
- Board: `docs/sprint/README-AGENTS.md` Sprint 0–12 all Done.

### Files / API
- APIs: `/api/audit-events`, `/api/integration-records`
- Endpoints: `AuditIntegrationEndpoints`
- Middleware: `SecurityHeadersMiddleware`, `RateLimitingMiddleware`
- Migration: `Sprint12_HardeningAudit`
- Config: `RateLimiting` in `appsettings.json`

### Verify
- `dotnet test Cms.sln -c Release` → 46 passed

### Deferred / Next (Pass 2)
- Full outbox/retry; load/soak; JWT/OIDC; Next.js UAT; AC-001… matrix hardening
- See Pass 2 pointers in `SPRINT-12-DOD.md`


---

## 2026-09-11 — Sprint 11 Financial Profile & Reporting (E13)

### User
Implement Sprint 11 Pass 1 only: enhance bill financial-profile read model (derived); GET /api/dashboard/summary + control queues exceptions/approvals; tenant isolation tests; SPRINT-11-DOD + handoff + PR to main. All tests green. No SoT derived totals on Bill. Non-goals: Next.js UI, Sprint 12 NFR, JWT. Never secrets/alogex.

### Done
- Enhanced `GET /api/bills/{id}/financial-profile`: maturity breakdown (Expected/Confirmed/Actual), allocated cost, AP/AR settlement outstanding by currency, `asOfTimestamp`, optional `?asOf=` filter with limitation note.
- `GET /api/dashboard/summary`: bill / open-exception / pending-approval / open-close counts + Best Available cost/revenue/profit by currency.
- Control queues: `GET /api/queues/exceptions` (open), `GET /api/queues/approvals` (pending).
- Derived read-only — Bill entity unchanged (no SoT totals).
- VI terminology keys for dashboard/queues; notes use `VietnameseUiTerms`.
- Tests: 43 passed (3 new Sprint 11 — profile maturity/allocated/settlement/asOf; dashboard isolation+counts; queues status filter).
- DoD: `docs/sprint/SPRINT-11-DOD.md`.

### Files / API
- APIs: `/api/bills/{id}/financial-profile`, `/api/dashboard/summary`, `/api/queues/exceptions`, `/api/queues/approvals`
- Endpoints: `DashboardReportingEndpoints`; profile query enhanced in `GetBillFinancialProfileQuery`
- Application: `Dashboard/Queries`, `Queues/Queries`

### Verify
- `dotnet test Cms.sln -c Release` → 43 passed

### Deferred / Next
- Full asOf maturity-history reconstruction (Pass 2)
- Sprint 12 hardening / NFR
- FX base roll-up; Next.js UI; JWT/OIDC; snapshot-based P&L

---

## 2026-09-11 — Sprint 10 Financial Close (E12)

### User
Implement Sprint 10 Pass 1 only: D11 financial_closes / financial_close_snapshots / financial_close_snapshot_details; C-010/AC-008 immutable snapshots; reopen/reclose new versions; tenant APIs start/snapshot/reopen/list + VI; isolation + immutability tests; DoD + PR to main. Non-goals: reporting UI (Sprint 11), hardening (Sprint 12), JWT, Next.js.

### Done
- Domain: `FinancialClose`, `FinancialCloseSnapshot`, `FinancialCloseSnapshotDetail`.
- Migration `Sprint10_FinancialClose` (three tables).
- CQRS + API: start close; snapshot+lock (metrics hash); reopen; list/get snapshots; optional supersede reclose.
- C-010/AC-008: snapshots insert-only (DbContext rejects Modified/Deleted); reopen keeps history; re-snapshot creates new `SnapshotVersion`; reclose creates new `VersionNo`.
- Eligibility stub: block snapshot when open/in_progress critical exception in scope.
- VI validation/errors; tenant filter + `X-Tenant-Id` / `X-User-Id`.
- Tests: 40 passed (3 new Sprint 10 — immutable+reopen history; eligibility; cross-tenant+reclose).
- DoD: `docs/sprint/SPRINT-10-DOD.md`.

### Files / API
- APIs: `/api/financial-closes`, `/api/financial-close-snapshots`
- Endpoints: `FinancialCloseEndpoints`
- Migration: `20260911193741_Sprint10_FinancialClose`

### Verify
- `dotnet test Cms.sln -c Release` → 40 passed

### Deferred / Next
- Reporting dashboard (Sprint 11)
- Hardening / UAT (Sprint 12)
- Full eligibility matrix; period lock on live ledger; JWT/OIDC; Next.js UI

---

## 2026-09-11 — Sprint 9 Financial Control (E11)

### User
Implement Sprint 9 Pass 1 only: D10 reconciliations/reconciliation_details/variances/exceptions/approvals; Variance ≠ Exception; Approval independent of Permission; tenant APIs reconcile/exception/approval + VI; isolation tests; DoD + PR to main. Non-goals: financial close (Sprint 10), JWT, Next.js.

### Done
- Domain: `Reconciliation`, `ReconciliationDetail`, `Variance`, `FinancialException` (table `exceptions`), `Approval`.
- Migration `Sprint9_FinancialControl` (five tables; IDX-011 on exceptions).
- CQRS + API: start reconciliation; add details (auto Variance when delta ≠ 0, never auto Exception); open/resolve/close exception; request/approve/reject approval on financial object ref.
- Variance ≠ Exception: control fact vs severity/owner/SLA work item; optional link only when escalated.
- Permission ≠ Approval: approval never calls `IPermissionService`; does not mutate permissions; works with role that has zero RolePermissions.
- Cost/Revenue `ApprovalStatus` updated by approval workflow (pending/approved/rejected) without RBAC.
- VI validation/errors; tenant filter + `X-Tenant-Id` / `X-User-Id`.
- Tests: 37 passed (3 new Sprint 9 — variance≠exception; approval≠permission; cross-tenant).
- DoD: `docs/sprint/SPRINT-9-DOD.md`.

### Files / API
- APIs: `/api/reconciliations`, `/api/variances`, `/api/exceptions`, `/api/approvals`
- Endpoints: `FinancialControlEndpoints`
- Migration: `20260911192820_Sprint9_FinancialControl`

### Verify
- `dotnet test Cms.sln -c Release` → 37 passed

### Deferred / Next
- Financial close snapshots (Sprint 10)
- Auto-escalate variance→exception; multi-step approval; bank feed; JWT/OIDC; Next.js UI

---

## 2026-09-11 — Sprint 8 Settlement (E10)

### User
Implement Sprint 8 Pass 1 only: D09 payments/collections/payment_allocations/collection_allocations; outstanding changes only via finalized allocation (AC-007/C-008); partial settle; reject over-allocation; reversal without hard delete; tenant APIs + VI; no Cost/Revenue from settlement (C-003/C-004); tests; DoD + PR to main. Non-goals: reconciliation (Sprint 9), close (Sprint 10), JWT, Next.js.

### Done
- Domain: `Payment`, `Collection`, `PaymentAllocation`, `CollectionAllocation`.
- Migration `Sprint8_Settlement` (four tables; IDX-009/010).
- CQRS + API: create/list/get payment & collection; draft allocate; finalize; reverse.
- AC-007: draft allocation does not change AP/AR outstanding; finalize updates `FinalizedSettledAmount` and derived Outstanding/SettlementStatus.
- C-008: over-allocation rejected (policy stub = 0) vs transaction amount and AP/AR ceiling.
- Partial settlement + unapplied/available-to-allocate on GET.
- Reversal: status → `reversed`; restores outstanding; no hard delete / silent overwrite (C-013).
- C-003/C-004: settlement never invents Cost/Revenue.
- VI validation/errors; tenant filter + `X-Tenant-Id`.
- Tests: 34 passed (3 new Sprint 8 — partial+finalize AC-007; over-allocate+reversal; collection+cross-tenant).
- DoD: `docs/sprint/SPRINT-8-DOD.md`.

### Files / API
- APIs: `/api/payments`, `/api/payment-allocations`, `/api/collections`, `/api/collection-allocations`
- Endpoints: `SettlementEndpoints`
- Migration: `20260911191645_Sprint8_Settlement`

### Verify
- `dotnet test Cms.sln -c Release` → 34 passed

### Deferred / Next
- Reconciliation / exceptions (Sprint 9)
- Financial close (Sprint 10)
- Bank feed; over-settlement policy; JWT/OIDC; Next.js UI

---

## 2026-09-11 — Sprint 7 Exposure + AP/AR (E09)

### User
Implement Sprint 7 Pass 1 only: D08 payable/receivable exposures + accounts_payable/receivable; Exposure ≠ Recognized AP/AR; outstanding derived (C-015); partial recognition; tenant APIs + VI; no Cost/Revenue on recognize (C-003/C-004); tests; DoD + PR to main. Non-goals: settlement (Sprint 8), JWT, Next.js.

### Done
- Domain: `PayableExposure`, `ReceivableExposure`, `AccountsPayable`, `AccountsReceivable`.
- Migration `Sprint7_ExposureApAr` (four tables; IDX-007/008).
- CQRS + API: create/list/get exposures; recognize → AP/AR; list/get AP/AR with derived Outstanding; adjust AP/AR.
- CP3/TD4: recognition creates separate AP/AR row — never merges into exposure status.
- C-015: Outstanding = recognized + adjustment − finalized_settled (settled=0 Pass 1); no user-entered SoT.
- C-003/C-004: recognize does not invent Cost/Revenue.
- VI validation/errors; tenant filter + `X-Tenant-Id`.
- Tests: 31 passed (3 new Sprint 7 — exposure≠AP + no invent; outstanding derived + partial; AR + cross-tenant).
- DoD: `docs/sprint/SPRINT-7-DOD.md`.

### Files / API
- APIs: `/api/payable-exposures`, `/api/receivable-exposures`, `/api/accounts-payable`, `/api/accounts-receivable`
- Endpoints: `ExposureApArEndpoints`
- Migration: `20260911190832_Sprint7_ExposureApAr`

### Verify
- `dotnet test Cms.sln -c Release` → 31 passed

### Deferred / Next
- Settlement payments/collections (Sprint 8)
- Dedicated AP/AR adjustment ledger; aging UI; JWT/OIDC; Next.js UI

---

## 2026-09-11 — Sprint 6 Financial Documents (E08)

### User
Implement Sprint 6 Pass 1 only: D07 financial_documents / lines / document_matches / match_details; Received ≠ Accepted ≠ Matched; tenant APIs receive/accept/lines/match; C-007 no over-match; C-003/C-004 no Cost/Revenue from documents; tests; DoD + PR to main. Non-goals: AP/AR, settlement, JWT, Next.js.

### Done
- Domain: `FinancialDocument` (three independent status dims), `FinancialDocumentLine`, `DocumentMatch`, `DocumentMatchDetail`.
- Migration `Sprint6_FinancialDocuments` (four tables; IDX-006).
- CQRS + API: receive, list/get, accept, add lines, start match, add match details (line↔line or line↔cost/revenue stub).
- AC-005: ReceiptStatus / AcceptanceStatus / MatchingStatus never collapsed to one enum.
- C-007: over-match rejected (tolerance stub = 0). C-003/C-004: receive/match do not invent Cost/Revenue.
- VI validation/errors; tenant filter + `X-Tenant-Id`.
- Tests: 28 passed (3 new Sprint 6 — state separation+no invent, over-match C-007, cross-tenant).
- DoD: `docs/sprint/SPRINT-6-DOD.md`.

### Files / API
- APIs: `/api/financial-documents`, `/api/document-matches`
- Endpoints: `FinancialDocumentEndpoints`
- Migration: `20260911185745_Sprint6_FinancialDocuments`

### Verify
- `dotnet test Cms.sln -c Release` → 28 passed

### Deferred / Next
- AP/AR recognition (Sprint 7)
- Settlement; auto-match; JWT/OIDC; Next.js UI

---

## 2026-09-12 — Sprint 5 Revenue & Profitability (E07)

### User
Implement Sprint 5 only: full revenues TD1; revenue_adjustments; maturity no-overwrite (C-009); Single Economic Revenue (C-004); APIs create/list/get/confirm/actualize/adjust; `GET /api/bills/{id}/financial-profile` derived read model; tests; DoD + PR to main. Non-goals: Documents/AP/AR, settlement, JWT, Next.js.

### Done
- Domain: expanded `Revenue` (layer amounts + customer/source/audit); `RevenueAdjustment`.
- Migration `Sprint5_Revenue` (alters `revenues`; creates `revenue_adjustments`).
- CQRS + API: create/list/get revenue; confirm/actualize; adjust; Bill financial profile.
- C-004/C-009: Single Economic Revenue; no silent maturity overwrite; document/AR source rejected.
- Financial profile: Best Available (Actual→Confirmed→Expected); profit = rev − cost per currency; no SoT totals on Bill (TD1-DB-003/004).
- VI validation/errors; tenant filter + `X-Tenant-Id`.
- Tests: 25 passed (3 new — maturity+adjust, profile/currency, isolation+idempotent source).
- DoD: `docs/sprint/SPRINT-5-DOD.md`.

### Files / API
- APIs: `/api/revenues`, `/api/bills/{id}/financial-profile`
- Endpoints: `RevenueEndpoints`
- Migration: `20260911185027_Sprint5_Revenue`

### Verify
- `dotnet test Cms.sln -c Release` → 25 passed

### Deferred / Next
- Documents / AP / AR (Sprint 6–7)
- Settlement; full approval; JWT/OIDC; Next.js UI

---

## 2026-09-12 — Sprint 4 Cost (E05/E06)


### User
Implement Sprint 4 only: full costs TD1 fields; cost_adjustments; allocations+finalize conservation; maturity no-overwrite; seed Expected from ratings; tenant APIs; tests; DoD + PR to main.

### Done
- Domain: expanded `Cost` (layer amounts + attribution/maturity/source/audit); `CostAdjustment`; `CostAllocation`; `CostAllocationDetail`.
- Migration `Sprint4_Cost` (alters `costs`; creates adjustment/allocation tables).
- CQRS + API: create/list/get cost; confirm/actualize; adjust; allocate/finalize; seed from rating.
- C-003/C-005/C-006/C-009: Single Economic Cost; conservation; basis gate; no silent maturity overwrite.
- VI validation/errors; tenant filter + `X-Tenant-Id`.
- Tests: 22 passed (3 new — maturity, allocation conservation, isolation+idempotent seed).
- DoD: `docs/sprint/SPRINT-4-DOD.md`.

### Files / API
- APIs: `/api/costs`, `/api/cost-allocations/{id}/finalize`, `/api/ratings/{id}/seed-expected-costs`
- Endpoints: `CostEndpoints`
- Migration: `20260911184233_Sprint4_Cost`

### Verify
- `dotnet test Cms.sln -c Release` → 22 passed

### Deferred / Next
- Revenue lifecycle (Sprint 5)
- Documents/AP/AR; full approval; JWT/OIDC; Next.js UI

---

## 2026-09-12 — Sprint 3 Rate & Pricing (E04)

### User
Implement Sprint 3 only: D04 rate cards/versions/rules/components + ratings snapshot; published version immutable (C-011); tenant APIs; tests; DoD + PR to main.

### Done
- Domain: `RateCard`, `RateVersion`, `PricingRule`, `PricingRuleComponent`, `Rating`, `RatingDetail` (UUIDv7, tenant_id, soft-delete, row_version).
- Migration `Sprint3_RatePricing` (does not alter Sprint 0–2).
- CQRS + API: rate card CRUD; draft→publish versions; rules/components on draft only; `POST /api/ratings` Expected seed snapshot (`fixed` / `unit_rate × qty`).
- C-011: published version rejects rule/component mutations; new version instead of overwrite; re-rating appends history.
- VI validation/errors; tenant filter + `X-Tenant-Id`.
- Tests: 19 passed (3 new — immutable publish, tenant isolation, rating snapshot).
- DoD: `docs/sprint/SPRINT-3-DOD.md`.

### Files / API
- APIs: `/api/rate-cards`, `/api/rate-versions`, `/api/pricing-rules/{id}/components`, `/api/ratings`
- Endpoints: `RatePricingEndpoints`
- Migration: `20260911183223_Sprint3_RatePricing`

### Verify
- `dotnet test Cms.sln -c Release` → 19 passed

### Deferred / Next
- Cost Expected lifecycle from rating (Sprint 4)
- Full formula engine (deferred)
- JWT/OIDC; Next.js UI

---

## 2026-09-12 — Wire CMS_DEPLOY_SSH_KEY + green deploy

### User
Làm luôn: gắn secret `CMS_DEPLOY_SSH_KEY` từ private key operator.

### Done
- GitHub Actions secret `CMS_DEPLOY_SSH_KEY` = operator `~/.ssh/id_ed25519_a1` (không commit key).
- Re-run failed `deploy` trên run `34632386398` → **success**.

### Verify
- Actions: https://github.com/thanhquyen129/CMS/actions/runs/34632386398 (`test` + `deploy` green)
- http://194.233.89.26/health · `/ready` OK

### Next
- Cost Expected on Bill (Sprint 4); JWT/OIDC thay header bootstrap

---

## 2026-09-12 — Sprint 2 Operational Reference (E03/E14)

### User
Implement Sprint 2: orders/shipments + N:N links to Bill; idempotent order upsert; bill graph; isolation/idempotency/graph tests; DoD + ship via PR.

### Done
- Domain: `Order`, `Shipment`, `OrderBillLink`, `BillShipmentLink` (UUIDv7, tenant_id, soft-delete, row_version).
- Migration `Sprint2_OperationalReference` (`orders`, `shipments`, `order_bill_links`, `bill_shipment_links`).
- CQRS + API: order upsert/list/get; shipment upsert; link Order↔Bill / Bill↔Shipment; `GET /api/bills/{id}/graph`.
- Idempotent upsert by `(tenant_id, source_system, external_id)`; VI validation; tenant filter + `X-Tenant-Id`.
- Tests: 16 passed (4 new Sprint 2 — isolation, idempotency, graph, graph cross-tenant).
- DoD: `docs/sprint/SPRINT-2-DOD.md`.

### Files / API
- APIs: `/api/orders`, `/api/shipments`, `/api/orders/{orderId}/bills/{billId}`, `/api/bills/{billId}/shipments/{shipmentId}`, `/api/bills/{id}/graph`
- Migration: `20260911182340_Sprint2_OperationalReference`
- Endpoints: `OperationalReferenceEndpoints`

### Verify
- `dotnet test Cms.sln -c Release` → 16 passed

### Deferred / Next
- transport_legs / movements (deferred)
- Cost Expected on Bill (Phase 2)
- JWT/OIDC replace header bootstrap

---

## 2026-09-12 — Sprint 1 Identity + Master Data (E01/E02)


### User
Implement Sprint 1: Tenant/user/access; Organization/BusinessParty/Currency; permission skeleton; isolation tests; ship.

### Done
- Domain: `Role`, `Permission`, `RolePermission`, `UserRole`, `Currency`; User polished.
- Migration `Sprint1_IdentityMaster` (roles, permissions, role_permissions, user_roles, currencies).
- CQRS + API: users, roles (+ assign), organizations CRUD, business-parties CRUD, currencies list/upsert.
- `CreateTenant` seeds Admin + core Action permissions; `IPermissionService` gates Bill create.
- Bootstrap: `X-Tenant-Id` + optional `X-User-Id` (JWT deferred). Soft allow when no user header.
- Tests: 12 passed (cross-tenant User/Org/Party; permission 403 VI + correlation id).
- DoD: `docs/sprint/SPRINT-1-DOD.md`.

### Files / API
- APIs: `/api/users`, `/api/roles`, `/api/users/{id}/roles/{roleId}`, `/api/organizations`, `/api/business-parties`, `/api/currencies`
- Identity: `PermissionService`, `TenantAccessSeeder`, `PermissionCodes`
- Migration: `20260911181636_Sprint1_IdentityMaster`

### Verify
- `dotnet test Cms.sln -c Release` → 12 passed
- Actions: https://github.com/thanhquyen129/CMS/actions
- Health: http://194.233.89.26/health (post-deploy)

### Next
- JWT/OIDC replace header bootstrap
- Cost Expected on Bill
- Full Data Scope matrix / Approval (deferred)

---

## 2026-09-12 — Fix CI Health smoke race (exit 7)

### User
Screenshot: Actions CI failed on `test` (exit 7); `deploy` skipped.

### Cause
`curl` hit `/health` ~1s before `dotnet run` finished migrate+listen (`curl: (7) Failed to connect`).

### Done
- CI Health smoke: poll `/health` up to 60s, `--no-build`, dump smoke log on failure; `ASPNETCORE_ENVIRONMENT=Production`.

### Files
- `.github/workflows/ci.yml`

### Verify
- Re-run after push: https://github.com/thanhquyen129/CMS/actions

### Next
- Confirm `CMS_DEPLOY_SSH_KEY` so `deploy` can run after green `test`

---

## 2026-09-12 — Close Sprint 0 (TD6 Foundation)

### User
Close Sprint 0: observability skeleton (E14), terminology contract (E16), real Actions deploy (E15), env example, DoD checklist; ship.

### Done
- Serilog JSON console + `CorrelationIdMiddleware` + `RequestLoggingMiddleware`; `/metrics` process placeholder.
- `VietnameseUiTerms` (CP6.5) + `GET /api/terminology`.
- CI `deploy` job: SSH rsync → `/opt/cms`, compose up (excludes `infra/.env`); docs for `CMS_DEPLOY_SSH_KEY`.
- `infra/.env.example`, `docs/sprint/SPRINT-0-DOD.md`.
- Tests: 6 passed (terminology + correlation ID coverage).

### Files / API
- Middleware: `CorrelationIdMiddleware`, `RequestLoggingMiddleware`
- Domain: `src/LCMS.Domain/Terminology/VietnameseUiTerms.cs`
- Endpoints: `/api/terminology`, `/metrics`
- Ops: `.github/workflows/ci.yml`, `docs/ops/github-actions.md`, `docs/ops/vps-bootstrap.md`

### Verify
- `dotnet test Cms.sln -c Release` → 6 passed
- Operator fallback deploy (tar/scp + compose) OK: `/health`, `/ready`, `/api/terminology`, `/metrics`, correlation header
- Actions: https://github.com/thanhquyen129/CMS/actions — require secret `CMS_DEPLOY_SSH_KEY` for automated deploy job

### Next
- Confirm GitHub secret `CMS_DEPLOY_SSH_KEY` so Actions `deploy` succeeds without operator fallback
- Sprint 1: User/Permission/JWT (replace `X-Tenant-Id`)
- Cost Expected on Bill

---

## 2026-09-12 — Bill thật trên DB (migration + API Tenant/Bill)

### User
Làm bước 1–2: EF migration InitialTd1 + API Tenant/Bill thật + test cô lập tenant.

### Done
- Migration `InitialTd1` (`src/LCMS.Infrastructure/Persistence/Migrations/`).
- Compose: `infra/docker-compose.dev.yml` (Postgres local), host compose thêm `db` + migrate on startup.
- CQRS: `CreateTenant` / `GetTenantById` / `CreateBill` / `GetBillById` → `ILcmsDbContext`.
- API: `POST/GET /api/tenants`, `POST/GET /api/bills` (Bill cần `X-Tenant-Id`).
- `/ready` kiểm tra kết nối DB; `Database:MigrateOnStartup`.
- Tests: `tests/LCMS.Api.Tests` — 4 passed (cross-tenant 404, thiếu tenant 401, duplicate 409).
- CI: Postgres service + `dotnet test` + health smoke với migrate.

### Verify
- `dotnet test Cms.sln -c Release` → 4 passed
- Local DB: `docker compose -f infra/docker-compose.dev.yml up -d` rồi chạy API
- Actions: https://github.com/thanhquyen129/CMS/actions

### Next
- Wire deploy job → `/opt/cms` (compose có Postgres)
- JWT claims thay `X-Tenant-Id`
- Cost Expected trên Bill (Phase 2)

---

## 2026-09-12 — TD1 Clean Architecture scaffold (Tenant + Bill)

### User
Khởi tạo Solution .NET Clean Architecture 4 project theo TD1; mẫu Entity Tenant/Bill + DbContext PostgreSQL; CQRS/MediatR; FluentValidation tiếng Việt; EF global tenant filter; exception middleware + Correlation ID.

### Done
- Solution `Cms.sln`: `LCMS.Domain`, `LCMS.Application`, `LCMS.Infrastructure`, `LCMS.Api` (.NET 8). Removed bootstrap `Cms.Api`.
- Domain: `Tenant`, `Bill` (TD1 baseline fields), stubs `Organization`/`User`/`BusinessParty`/`Cost`/`Revenue`; `UuidV7`, `row_version`, soft-delete (C-013), no hard delete.
- Infrastructure: `LcmsDbContext` + Fluent configs + snake_case + PostgreSQL; global query filter C-001 + soft-delete.
- Application: MediatR + FluentValidation (VI messages) + sample `CreateBillCommand`.
- API: DI, `ExceptionHandlingMiddleware` (JSON VI + correlationId), `TenantResolutionMiddleware` (`X-Tenant-Id`), `/health` `/ready`.
- ADR: `docs/adr/ADR-0001-td1-identity-tenancy-postgres.md`
- Docker/CI updated to `LCMS.Api`.

### Files / schema
- Tables mapped: `tenants`, `bills`, `organizations`, `users`, `business_parties`, `costs`, `revenues`
- Conn string: `ConnectionStrings:LcmsDb`
- Migration chưa tạo — follow-up: `dotnet ef migrations add InitialTd1`

### Verify
- `dotnet build Cms.sln -c Release` (local OK)
- After deploy: http://194.233.89.26/health , `/ready`
- Actions: https://github.com/thanhquyen129/CMS/actions

### Next
- EF migration + Postgres on host
- Wire CreateBill handler to DbContext
- Identity/JWT claims → replace header tenant bootstrap

---

## 2026-09-12 — Wipe VPS + deploy CMS bootstrap

### User
Wipe `194.233.89.26`; update all IPs in rules to `194.233.89.26`.

### Done
- Rules: removed other IPv4 (`217.216…`); CMS production IP consistently `194.233.89.26` (IPv6 NIC kept).
- VPS wipe: stopped/removed `a1-api`/`a1-postgres`, volume `infra_a1_pgdata`, deleted `/opt/sanlogistics`, created `/opt/cms`.
- Deployed CMS API bootstrap via Docker Compose on host; verified `/health` and `/ready`.

### Verify
- http://194.233.89.26/health
- http://194.233.89.26/ready
- Actions: https://github.com/thanhquyen129/CMS/actions

### Next
- Phase 1: Identity/Tenant/Master + Bill anchor
- Wire self-hosted runner / Actions deploy for `/opt/cms`

---

## 2026-09-12 — Kickoff CMS

### User
Kickoff: đặt tên Cost Management System (CMS); update rules từ alogex → CMS; đọc BA docs; lập kế hoạch; tạo repo GitHub CMS; clear + deploy lên VPS theo hailybato.

### Done
- Rules: renamed product to CMS; host `194.233.89.26` → `/opt/cms`; repo `thanhquyen129/CMS`; expert stance → financial control (Bill-centric).
- BA indexed; implementation plan written; GitHub private repo created.
- VPS wipe + deploy completed in follow-up turn after PO confirm.
