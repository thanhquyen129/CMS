# ADR-0042 — Charge profit, pricing partner, declared VAT

**Status:** Accepted  
**Date:** 2026-10-09  
**Relates:** ADR-0025, ADR-0027, ADR-0040, ADR-0041, CP-01…CP-03

## Context

Buy and sell prices for the same economic charge (for example packaging) were not paired. Rate cards and surcharges did not carry a supplier or customer. VAT was not declared, and historical prices must not be rewritten as 0%.

## Decision

1. `economic_charge_types` plus `charge_type_mappings` pair cost and revenue. Profit by charge is a read model. It uses the net reporting amount of one maturity basis. A missing side, missing FX, or mixed maturity is incomplete or “so sánh hỗn hợp”. It is not an actual loss. Gross and settlement amounts are not economic profit. Shared cost enters only through finalized allocation details.
2. A buy card may store `supplier_party_id`. A sell card may store `customer_party_id` or `customer_group_code` (the existing party `group_code`), not both. Surcharge scope keeps the same optional partner fields. Rating stores `partner_suggested_id`. Cost `vendor_party_id` and revenue `customer_party_id` stay the actual partner. Changing the actual partner requires a reason, writes audit, and sets `partner_override_requires_rerate`. It does not change the economic amount or the published rating.
3. The only new pricing field is `vat_rate` on a draft rate version and a draft surcharge version. Null means undeclared, not 0%. A published version is not updated. New rating details snapshot net, VAT, and gross. `rating_details.amount` and cost/revenue maturity amounts stay net, so existing profitability, AP/AR face amounts, and reporting FX are unchanged. Document lines may store their own net/VAT/gross and a variance against the rating snapshot. They do not rewrite the rating. Input VAT deductibility is out of scope.

## Consequences

- Old rows keep a null VAT rate until someone declares it on a new draft version and re-rates. Configured unit prices on new ratings are treated as before VAT.
- A customer group is the party group code already on the partner, not a second identity table.
