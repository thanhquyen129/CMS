# P05 — Rate card / Rating / seed Expected (UI) DoD

**Ngày:** 2026-09-12  
**PO:** TD6 E04; C-011 · Checklist `PO-GAP-CHECKLIST.md`

## Done
1. Màn `/rate-cards` — list bảng giá; `/rate-cards/new` tạo card (customer|vendor).
2. Chi tiết `/rate-cards/[id]`: tạo phiên bản nháp → thêm quy tắc (fixed / unit_rate / percent_of_base) → **phát hành** (≥1 rule).
3. Bill panel **Tính giá / Rating**: chọn card + phiên bản published → quantity (+ lọc optional) → tick seed Expected → POST rating.
4. Lịch sử rating trên Bill + CTA **Seed chi phí dự kiến** (idempotent).
5. BFF: `rate-cards`, `versions`, `publish`, `rules`, `ratings`, `seed-expected-costs`.
6. Nav + dashboard shortcut.

## Verify
- `npm run build` apps/web.

## Non-goals (follow-up)
- UI component breakdown trên pricing rule (API hỗ trợ; seed vẫn OK từ rule không component — nature=cost).
- Edit/soft-delete rate card từ UI.
- min_max_clamp trên form (API có; UI MVP 3 method chính).
