# PROMPT — Pass 2 / Sprint 1 FULL Identity + Master Data

<<<<<<< HEAD
Bạn đang làm **Pass 2 — Sprint 1 FULL** trong `c:\A1\git\cms`. Pass 1 S1 + Pass 2 S0 (JWT) đã có trên `main`.

## Mục tiêu TD6 E01/E02 đủ hơn thin Pass 1
Đóng Identity/Access + Master Data theo PO: Data Scope tách Permission; JWT claims; party roles; currency/org hoàn thiện.

## Must ship
1. **AuthZ:** Permission × **Data Scope** độc lập (Action permission không suy ra data boundary). Implement scope codes tối thiểu: `all`, `organization`, `own` — enforce trên ít nhất Bill list/get + Cost list (filter by creator/org when scope≠all).
2. Wire **JWT `sub`** as required actor for mutating APIs when `RequireJwt`; seed Admin role permissions include scopes.
3. **Master data:** `party_roles` table (UNIQUE tenant+party+role_code); API assign/list. Currency ISO validation harden. Organization hierarchy get tree/list children.
4. **User lifecycle:** deactivate soft-delete; cannot login/issue meaningful access when inactive (check on permission service).
5. Tests: scope isolation (own cannot see other's bills); inactive user forbidden; party_roles unique; JWT path.
6. `docs/sprint/SPRINT-1-FULL-DOD.md` + handoff + PR → main. `dotnet test` xanh.

## Non-goals
Full ABAC, Approval matrix (Sprint 9), OIDC IdP, Next.js.

## Ràng buộc
Never secrets/alogex. Vietnamese errors. Read `SPRINT-1-DOD.md` deferred list.
=======
Bạn đang làm **Pass 2 — Sprint 1 FULL** (không còn thin stub) trong repo CMS.
Base: **main after Pass 2 Sprint 0 FULL (JWT)**.

## Mục tiêu
Đóng đủ Identity + Master Data theo TD6 E01/E02 + Pass 1 `SPRINT-1-DOD` deferred:
Permission × Data Scope độc lập; JWT actor; party_roles; org tree; currency harden.

## Must ship (Pass 2 depth)
1. **Permission × Data Scope độc lập** (`all` / `organization` / `own`) — enforce tối thiểu trên Bill + Cost **list/get**.
2. **JWT `sub` = actor**; user inactive ⇒ deny (VI 403).
3. **`party_roles`** + APIs; org **tree** / **list children**; **currency harden** (catalog seed, get-by-code, inactive reject, baseline decimals).
4. Tests xanh + `docs/sprint/SPRINT-1-FULL-DOD.md` + handoff + PR → main.

## Non-goals
OIDC IdP; Next.js UI; full ABAC policy engine; Approval workflow (đã có Sprint 9 thin).

## Ràng buộc
Never commit secrets. Never alogex. Vietnamese errors. One sprint chat only.
>>>>>>> 9c6e508 (Ship Sprint 1 FULL Identity + Master Data (Pass 2).)
