# PROMPT — Pass 2 / Sprint 6 FULL Financial Documents

Bạn đang làm **Pass 2 — Sprint 6 FULL** trong `c:\A1\git\cms`. Pass 1 S6 + Pass 2 S0–S5 đã trên `main`.

## Mục tiêu TD6 E08 đủ hơn thin
Chứng từ tài chính: matching N:N đầy đủ hơn, tolerance policy, link sang Cost/Revenue không tạo bản ghi kinh tế mới, acceptance workflow.

## Must ship
1. Match methods: `line_to_line`, `line_to_cost`, `line_to_revenue` (link only — C-003/C-004 không tạo Cost/Revenue).
2. Tolerance policy config (default 0; optional absolute/percent stub) for C-007 over-match.
3. Document lifecycle harden: Received→Accepted gate before match; cancel/void via soft status (no hard delete).
4. Duplicate document control: unique (tenant, document_type, document_no, counterparty) when configured.
5. API: list open match amounts; match detail reverse/cancel; Vietnamese errors.
6. Tests: over-match; tolerance; no cost creation on receive; tenant isolation; state gates.
7. `SPRINT-6-FULL-DOD.md` + handoff + PR. `dotnet test` xanh (không regress suite).

## Non-goals
AP/AR recognition changes (Sprint 7 FULL), e-invoice provider, Next.js.

## Ràng buộc
Never secrets/alogex. Read `SPRINT-6-DOD.md` deferred.
