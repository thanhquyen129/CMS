# PROMPT — UI-5 Settlement + Close (paste vào New Chat / Cloud Agent)

Base: Pass UI U0–U4 trên `main`. Đọc `docs/sprint/PLAN-UI.md` + `UI-4-DOD.md` follow-ups.

## Mục tiêu
Operator tất toán AP/AR qua Thanh toán / Thu tiền (phân bổ → chốt); mở và khóa Chốt tài chính (Bill/kỳ) + đọc P&L snapshot. Copy CP6.5: Payment ≠ Cost; Collection ≠ Revenue; snapshot bất biến.

## DoD mỏng nhưng đủ
1. `/settlements` — list Payment/Collection; tạo; detail allocate + finalize qua BFF (API S8 sẵn).
2. `/financial-closes` — list; mở close; snapshot (primary); reopen (secondary); P&L đọc được (API S10 sẵn).
3. Bill panel CTA: tạo payment/collection + chốt theo Bill.
4. `docs/sprint/UI-5-DOD.md` + handoff + PR. Chỉ `apps/web/**` + docs.

## Non-goals
Bank feed, reverse allocation UX đầy đủ, write-off/recognize UI, match UI, invent money API.
