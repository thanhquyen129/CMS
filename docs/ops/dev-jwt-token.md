# Dev JWT token helper (Sprint 0 FULL)

## Endpoint (Development only)

`POST /api/dev/token` is mapped only when `ASPNETCORE_ENVIRONMENT=Development`.

```bash
curl -sS -X POST http://localhost:5080/api/dev/token \
  -H 'Content-Type: application/json' \
  -d '{"tenantId":"<guid>","userId":"<guid>"}'
```

Response:

```json
{ "accessToken": "<jwt>", "tokenType": "Bearer", "expiresIn": 7200 }
```

Use the token:

```bash
curl -sS -X POST http://localhost:5080/api/bills \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"billNo":"B1","billType":"freight"}'
```

Claims issued: `tenant_id`, `sub` (user id), `jti`.

## Header bootstrap (Dev)

When `Auth:AllowHeaderBootstrap=true` (Development default), you may still send:

- `X-Tenant-Id`
- `X-User-Id`

JWT claims win when present. Production defaults disable header bootstrap.

## Production signing key

Set `Auth__Jwt__SigningKey` (≥32 chars) in host `infra/.env`. Never commit real secrets. See `infra/.env.example`.

## Rate limiting (single-node)

Sprint 12 ships an **in-process** fixed-window limiter on `/api/*` (`RateLimiting` in appsettings). Limits are **per process / single node** — not shared across replicas. Redis-backed distributed rate-limit is deferred (Pass 2 / later). `/health`, `/ready`, `/metrics` are exempt from the API window.
