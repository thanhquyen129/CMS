# P15 — Bank feed CSV import DoD

**Ngày:** 2026-09-13 · ADR-0013

## Done
1. `ImportBankFeedCsvCommand` + `POST /api/bank-feed/lines/import-csv`.
2. Header: `valueDate,amount,currencyCode[,direction,bankReference,counterpartyName,description]`.
3. UI `ImportBankFeedCsvForm` trên `/bank-feed` + BFF `/bff/bank-feed/lines/import`.

## Verify
- `dotnet test` filter `SprintP14P20` — `BankFeed_ImportCsv_CreatesLines` passed.

## Non-goals
- Bank API sync / SFTP (follow-up).
