# PROMPT — UI-0 Scaffold + shell + login (paste vào New Chat / Cloud Agent)

Bạn đang làm **Pass UI / Sprint U0** trong `c:\A1\git\cms`. Pass 2 API FULL tiếp tục song song — **không** đụng logic money Pass 2 trừ auth login mỏng.

Đọc trước: `docs/sprint/PLAN-UI.md`, `docs/adr/ADR-0002-jwt-bearer-tenant-claims.md`, `infra/docker-compose.host.yml`.

## Mục tiêu
Ship Next.js lên VPS: `http://194.233.89.26/` = UI tiếng Việt (shell), không còn JSON API root. Login lấy JWT theo ADR-0002. Proxy `/api` → API.

## DoD mỏng nhưng đủ
1. **`apps/web`**: Next.js App Router + TypeScript. Pages: `/login`, `/` (shell sau login). Nav placeholder: Bill, Dashboard (link disabled hoặc “sắp có” — không fake data).
2. **Terminology:** load `GET /api/terminology`; dùng nhãn VI cho shell.
3. **Auth:** `POST /api/auth/login` (email + password → Bearer JWT `tenant_id` + `sub`). Password hash trên user (thin). Seed/bootstrap **qua env** trên host — không commit secret. Ghi **ADR-0007** (BFF httpOnly cookie vs client Bearer — ưu tiên BFF cookie same-origin). ADR-0006 đã dùng cho AP/AR aging.
4. **Infra:** `Dockerfile.web`; compose `web`; **nginx/Caddy** reverse proxy: `/`→web, `/api|/health|/ready|/metrics`→api. API không publish host `:80` nữa.
5. **CI/deploy:** Actions build/deploy web+proxy; verify `/` HTML UI, `/health` OK, `/api/terminology` OK.
6. Empty/loading/error login thật. Copy VI chuyên nghiệp B2B.
7. `docs/sprint/UI-0-DOD.md` + `docs/handoff.md` + PR → main. Tests API login xanh; `dotnet test` không đỏ.

## Non-goals
Bill list/profile (U1), Cost confirm (U2), OIDC IdP, design system lớn, dark mode, purple gradients.

## Ràng buộc
Never secrets/alogex. Tenant isolation. UI ẩn ≠ authz. Ship theo `06-ship-after-task.mdc`.
