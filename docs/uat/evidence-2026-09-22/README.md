# Evidence pack — UAT Pixel Wave 2 (2026-09-22)

**Host:** `http://194.233.89.26`  
**Runner:** Auto browser  
**User:** `admin@cms.local` (password local-only — not stored here)  
**CI fix:** https://github.com/thanhquyen129/CMS/actions/runs/35717990220 (`0a2df35` success)

## Session notes

1. Prod web was 500 (`billId` ≠ `id` Next.js slug). Fixed + redeployed before UAT.
2. Walked UI-01…15 + Wave 2 import/create/close-gate routes after login.
3. Screenshots captured in Cursor browser session (dashboard, bills, rate-cards, costs, revenues, documents, AP/AR, settlements, control, closes, reports, admin, settings, workflow, imports, bills/new, legs/new).
4. Open close detail used: `/financial-closes/01a093dc-fb3d-714e-a019-146ede6f4c9f` — five gate **Chặn** items visible.
5. Post `cb7f1b7` (CI success): `cost@cms.local` — dashboard ẩn DT/biên; `/revenues` alerts 403; API profit+revenues 403, costs 200.

## Residuals (not Fail on executed UX-01 rows)

- EN footer “Best Available” on some KPI cards (UX-02 polish) — later pass used VI “Giá trị tốt nhất hiện có”.
- UX-07 / UX-10 / UX-13 live double-submit / party merge / Budget OUT.
- Nav/CTA Doanh thu vẫn hiện với `cost@` (license UI; backend 403 đủ H-009).
