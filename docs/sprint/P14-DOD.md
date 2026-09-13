# P14 — UI recovery tích hợp DoD

**Ngày:** 2026-09-13 · Checklist `PO-GAP-CHECKLIST.md`

## Done
1. `/integration-errors` — list theo `recoveryStatus` (pending / retried / dead_letter).
2. BFF `GET /bff/integration-errors`, `POST …/mark-retried`, `POST …/dead-letter`.
3. `IntegrationErrorActions` — CTA thử lại / dead-letter (VI).
4. Nav AppShell → **Lỗi tích hợp**.

## Verify
- Manual: mở `/integration-errors`, mark-retried / dead-letter.

## Non-goals
- Broker replay thật (P23).
