# ADR-0036: If-Match on money mutations

- Status: Accepted
- Date: 2026-09-24
- Relates: ADR-0001 (row_version concurrency token), AC-010

## Context
`row_version` is an EF concurrency token and is stamped on save. A second HTTP request loads the row after the first commit, so the token does not stop a stale screen from applying another cost adjustment, confirm, allocation, or close snapshot.

## Decision
1. GET of cost, revenue, payment, collection, allocation, financial close, document match, and AP/AR returns `rowVersion` (base64).
2. Adjust, confirm, and actualize of cost/revenue, payment/collection allocate, finalize, and reverse, cost-allocation finalize, document-match confirm, AP/AR adjust, write-off, and reverse-recognize, revenue-mapping finalize, close snapshot, and reopen close read `If-Match`. The check runs after load and before the business status guard, so a stale token returns 409 `concurrency_conflict` and does not write. A matching retry of the same idempotency key still returns the first id. Creating a revenue mapping returns `rowVersion` so the following finalize can send it. Calculate, submit, and cancel of a cost allocation, cancel of a revenue mapping or document match, recognize of a payable or receivable exposure, and add, resolve, or reverse of a document-match detail read the same header. Adding or reversing a detail touches the match row version, so the next detail must reload the session.
3. Creating a payment or collection allocation touches the cash row version, so the next allocate must send the new token.
4. `Concurrency:RequireIfMatch` is false in Development (existing clients and tests omit the header) and true in Production. A missing token in Production is 409.

## Consequences
- Two users who both opened the old cost can no longer both apply a delta. The second reloads.
- Already-finalized payment or collection allocation stays a no-op and does not require the old token.
- Concurrent saves of the same accounts-payable row still rely on the database token when both requests loaded it before either saved.

## Alternatives rejected
- Requiring If-Match in every environment immediately. That fails the existing API suite and every script that does not reload the row.
- Client-only disable of the button. A second tab or a direct API call would still overwrite.
