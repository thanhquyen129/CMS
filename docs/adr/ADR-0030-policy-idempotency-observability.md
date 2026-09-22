# ADR-0030 — Policy registry, money-path idempotency, integration observability

**Status:** Accepted  
**Date:** 2026-09-22  
**Relates:** ADR-0029, MASTER 09, FR-011, FR-012, POL-01, M21, M28, NFR-02, D12-02, D12-03

## Context

Packages A–G shipped operable finance. Package H closes platform hardening: versioned policy ownership, one-create-one-row idempotency beyond documents/payments, and integration metrics without leaking secrets. 09B DEV Status was blank.

## Decision

1. Table `policies` stores the 13 MASTER Policy Registry keys (`OPERATIONAL_SOURCE_OWNERSHIP` … `REPORTING_FX_POLICY`) with owner, version, effective dates, status, and `body_json`. `POST /api/policies/ensure-catalog` seeds missing keys; upsert may create a new version and supersede prior actives. UI: `/settings/policies`.
2. Shared `IIdempotencyGate` + header `Idempotency-Key` on create cost, revenue, collection, document, payment, document-match start, AP/AR recognize, and financial-close start (scopes in `IdempotencyScopes`).
3. `SecretRedactor` strips bearer/password/api-key patterns from integration error message/detail before persist. `GET /api/integration-errors/job-health` returns pending outbox/error counts and actionable next steps. Prometheus gauges updated on that sample. `/metrics` is open in Development; Production without `Metrics:ScrapeToken` returns 404; with token requires `X-Metrics-Token`.
4. 09B Final + Detailed Traceability DEV Status filled to DONE with evidence pointers (this ADR + HardeningPackageHTests).

## Consequences

- Migration `PolicyRegistryPackageH` (create `policies` only — no DropIndex).
- Tenant financial JSON knobs remain until consumers read policy body; registry is SoT for ownership/versioning.
- Scrapers must set scrape token in Production.
