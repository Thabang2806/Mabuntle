# Swyftly API Contracts

## Health

```http
GET /health
```

Response:

```json
{
  "status": "Healthy",
  "applicationName": "Swyftly.Api",
  "timestampUtc": "2026-05-18T10:00:00.0000000+00:00"
}
```

## Readiness

```http
GET /health/ready
```

Response when healthy:

```json
{
  "status": "Healthy",
  "applicationName": "Swyftly.Api",
  "timestampUtc": "2026-05-18T10:00:00.0000000+00:00",
  "totalDurationMilliseconds": 12.34,
  "checks": {
    "postgresql": {
      "status": "Healthy",
      "description": null,
      "error": null,
      "durationMilliseconds": 12.34
    }
  }
}
```

The readiness endpoint returns HTTP `503` with the same response shape when PostgreSQL is unavailable.

## Current Scope

Only health/readiness endpoints exist in the initial scaffold. Auth, catalog, checkout, payments, AI, and admin APIs are intentionally not implemented yet.

## API Rules

- Keep endpoints thin.
- Validate ownership and roles at the API/application boundary.
- Do not expose provider secrets or internal ledger implementation details.
- Use idempotency for webhooks and other external event handlers.
