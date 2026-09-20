# ADR-0020 — Party transactability and credit control

**Status:** Accepted (Architecture Agreed — 2026-09-20)  
**Date:** 2026-09-20  
**Relates:** H-003, D02 `business_parties`, ADR-0019 (customer typeahead, not TMS)

## Context

`BusinessParty` is the canonical customer/vendor master (roles, not split entities). The previous master stored a credit ceiling as **display-only**. Ops still attached inactive parties to Bills/costs/documents, and never saw AR/AP outstanding on the partner card. A finance-control product cannot treat the partner as a name list.

## Decision

1. **One canonical party.** Keep `BusinessParty` + `party_roles` (`customer` / `vendor` / `payer` / `payee`). Do not split Customer vs Vendor tables.
2. **Transactability.** A party may be linked to a **new** Bill / Cost / Revenue / chứng từ only when `IsActive` and **not** `IsBlocked`. Block is a credit/compliance hold, independent of “ngừng dùng”.
3. **Role gate on new links** (existing rows are not rewritten):
   - Bill `CustomerPartyId` → role `customer`
   - Revenue / chứng từ phải thu → `customer` hoặc `payer`
   - Cost / chứng từ phải trả → `vendor` hoặc `payee`
4. **Credit control is per-party** (`credit_control_mode`):
   - `advisory` — show utilization only
   - `warn` — same writes; UI must surface “sắp/đã vượt hạn mức”
   - `block` — reject **AR recognition** when outstanding AR (same currency as the limit) + amount would exceed `credit_limit`
5. **Credit limit is the customer AR ceiling** (how much they may owe us). Vendor AP outstanding is shown, not used to hard-block. Cross-currency vs the limit does **not** hard-block (no silent FX invention).
6. Soft-delete is refused while open AP or AR outstanding remains. Inactivate or block instead.

## Consequences

- Partner 360° (Bills, chứng từ, AP/AR, utilization) is a first-class workspace, with View Cost ≠ View Revenue gating.
- Lookup/typeahead by mã / tên / MST / SĐT is the capture path for Bill and chứng từ (ADR-0019).
- Hard credit block is an irreversible money rule: changing the default tenant-wide policy later needs a new ADR. Per-party mode remains configurable without a new ADR.

## Alternatives rejected

- Hard-block on Revenue create — Revenue ≠ AR (C-004 / CP6.5).
- Auto FX conversion to compare mixed currencies — would invent a financial fact.
- Separate Customer/Vendor aggregates — duplicates legal identity (MST) and bank accounts.
