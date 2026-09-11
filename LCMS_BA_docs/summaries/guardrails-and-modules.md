# Guardrails extract (from Final Handover + CP7)

- LCMS/CMS = Financial Control layer; Operational System remains SoT for operations.
- Bill = minimum operational reference + central Financial Anchor.
- Single Economic Cost / Single Economic Revenue — no double count via documents/AP/AR/settlement.
- Expected / Confirmed / Actual are separate maturity layers — no overwrite shortcut.
- Received ≠ Accepted ≠ Matched ≠ Recognized ≠ Settled.
- Independent state dimensions (lifecycle, maturity, approval, allocation, matching, settlement, reconciliation, close).
- Financial history non-destructive; Close snapshots immutable.
- Permission = User/Role × Action × Data Scope; Approval is separate.
- Integration idempotent; mapping failure must not fabricate financial facts.
- Vietnamese UI Term → Canonical English → Code Key (CP6.5).

# Modules (TD3)
M01 Identity/Tenant/Access … M15 Audit — Modular Monolith baseline.
