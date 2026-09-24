# ADR-0034 — Production FX does not use config stub rates

- Status: Accepted
- Date: 2026-09-24
- Amends: ADR-0004 (config fallback remains for Development and tests)
- Relates: CRP-03

## Context

Dated `fx_rates` is the auditable conversion. `StubFxRatesToBase` still filled `BaseAmount` with `FxRateId` null when the dated row was missing. That is acceptable in dev. On a commercial tenant it records money without a rate source.

## Decision

1. `Cost` / `Revenue` / `Settlement` option `AllowStubFxFallback` defaults to true.
2. `appsettings.Production.json` sets it false on all three.
3. When false and no dated rate applies, the command returns a Vietnamese validation error and does not convert. Same-currency still copies the amount with `FxRateId` null.
4. Dashboard base roll-up skips a currency that has no rate and says so, instead of failing the whole summary.

## Consequences

- Production cross-currency cost, revenue, and settlement require a row on the FX ledger for the effective date.
- Dev and API tests keep the stub map.
- Historical rows already stored via the stub are not rewritten.

## Alternatives rejected

- Delete the stub immediately — breaks Development and the existing test host.
- Silent 1:1 — hides a missing rate.
