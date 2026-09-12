# Pass UI — Next.js financial control (parallel track)

**Goal:** Operator vào `http://194.233.89.26/` thấy UI tiếng Việt (CP6.5), đăng nhập, mở Bill → financial profile → xác nhận chi phí/doanh thu — không còn JSON API root.

**Constraint:** Song song Pass 2 FULL API. Không dừng S7–S12 FULL. Một chat/agent = một UI sprint.

**Stack (locked):** Next.js App Router (TypeScript) + API hiện có (ASP.NET). Không SPA framework thứ hai.

---

## Why `/` looks empty today

Port 80 = `cms-api` only. Root returns product JSON. **Không có `apps/web`.** Pass 1–2 cố ý API-first.

---

## Hosting decision (proposed → ADR when coding UI-0)

| Piece | Choice |
|-------|--------|
| Same VPS | `cms-sg-01` `/opt/cms` |
| Edge | **nginx** (or Caddy): `/` → `web:3000`; `/api`, `/health`, `/ready`, `/metrics` → `api:8080` |
| API listen | Internal `8080` only (stop publishing API on host `:80`) |
| CORS | Same-origin via proxy — no browser CORS gymnastics |
| Auth | JWT Bearer (ADR-0002). UI-0 thêm **login mỏng** (email/password → JWT) vì Production `RequireJwt=true` và `/api/dev/token` chỉ Dev |

Irreversible fork: public surface + login model → ghi **ADR-0007** trong UI-0 (ADR-0006 = AP/AR aging).

---

## Parallel with Pass 2

```
Pass 2 FULL API:  S7 → S8 → … → S12   (cloud agents hiện có)
Pass UI:          U0 → U1 → U2 → U3 → U4  (chat/agent riêng)
```

Merge độc lập. UI gọi API đã có trên `main`; thiếu field thì ghi follow-up API, không block Pass 2.

---

## Sprint map (thin → operable)

| Sprint | Outcome (user xong việc) | Primary APIs | Non-goals |
|--------|--------------------------|--------------|-----------|
| **U0** Scaffold + shell | Mở site → login → shell VI + terminology | `POST /api/auth/login` (mới), `GET /api/terminology` | Business screens |
| **U1** Bill hub | Tìm Bill → xem financial profile + profitability | `/api/bills`, `/api/bills/{id}/financial-profile`, `/profitability` | Cost mutate |
| **U2** Cost & Revenue | Trên Bill: xác nhận Cost / Revenue (1 CTA chính / màn) | `/api/costs/*`, `/api/revenues/*` | Documents |
| **U3** Control desk | Dashboard + hàng đợi exception / approval | `/api/dashboard/summary`, `/api/queues/*` | Full reporting |
| **U4** Documents & cash thin | Nhận/chấp nhận chứng từ + outstanding AP/AR đọc được | documents + exposure/AP/AR read | Auto-match, bank feed |
| **U5** Settlement + Close | Thanh toán/Thu tiền allocate→finalize; chốt Bill/kỳ + P&L snapshot | payments/collections + financial-closes | Bank feed, write-off UI |

Pass 1 DoD mỗi U: **shippable trên VPS**, empty/loading/error thật, copy VI CP6.5, không control giả.

---

## U0 detail (first ship)

1. `apps/web` — Next.js 15 App Router, TS, minimal CSS (trust B2B; không purple-glow).
2. `infra/Dockerfile.web` + service `web` trong `docker-compose.host.yml`.
3. `infra/nginx.conf` (+ optional `Dockerfile.proxy`) — reverse proxy as above.
4. API: thin `POST /api/auth/login` (email + password → JWT `tenant_id`+`sub`); seed/bootstrap user qua env hoặc migration demo **không** commit secret.
5. UI: `/login`, layout shell (nav: Bill / Dashboard placeholder), fetch terminology, Bearer in httpOnly cookie **hoặc** memory+secure cookie via Next BFF route — chọn 1 trong ADR-0006 (ưu tiên **BFF cookie**, token không lộ `localStorage`).
6. CI: build web image; Actions deploy compose includes `web` + proxy.
7. DoD: `docs/sprint/UI-0-DOD.md`; `/` = UI; `/api/terminology` vẫn qua proxy.

---

## UX bar (mọi UI sprint)

- Một hành động chính mỗi màn.
- Số tiền + maturity (Dự kiến / Đã xác nhận / Thực tế) đọc lướt được.
- Nhãn từ `GET /api/terminology` — không lộ enum thô.
- Trạng thái trống / lỗi / loading là phần thiết kế.
- Quyền: UI ẩn nút chỉ cosmetic; API vẫn enforce.

---

## Success metric (first visible win)

Sau **U1** trên VPS: finance/ops login → search Bill → thấy Expected vs Confirmed vs Actual cost/revenue trên một màn — không Postman.

Sau **U2**: xác nhận được chi phí trên Bill từ UI.

---

## Risks

| Risk | Mitigation |
|------|------------|
| Pass 2 merge conflict compose | UI-0 chạm compose/nginx một lần; Pass 2 tránh đụng `infra/` trừ khi cần |
| No password on users today | UI-0 adds hash + login; ADR |
| JWT in browser XSS | Prefer BFF httpOnly cookie |
| Scope creep full TMS UI | Bill-centric only; no GPS/e-POD |

---

## How to run

1. New Chat / cloud agent riêng.
2. Paste `PROMPT-UI-N.md`.
3. PR → merge → deploy (Actions). Verify `http://194.233.89.26/` = UI.

Kickoff đầu: **`PROMPT-UI-0.md`**.
