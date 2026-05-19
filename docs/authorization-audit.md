# Authorization Audit

Last reviewed: 2026-05-19

## Scope

Prompt 57 reviewed the minimal API endpoint groups currently registered by `Program.cs`.

## Public Endpoints

The following endpoints are intentionally public:

- `GET /health`
- `GET /health/ready`
- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/payments/webhook/{provider}`
- Public catalog and storefront reads under `/api/products`, `/api/categories`, and `/api/sellers/{storeSlug}`
- Anonymous ad tracking under `/api/ads/impressions` and `/api/ads/clicks`

Payment webhooks remain anonymous by transport design, but provider signature verification and idempotency are required at the payment service boundary.

## Protected Endpoint Rules

- Seller endpoints require the `Seller` role and must resolve ownership from the authenticated seller profile.
- Buyer endpoints require the `Buyer` role and must resolve ownership from the authenticated buyer profile.
- Admin endpoints require `Admin` or `SuperAdmin`, except support workflows that also allow `SupportAgent` where appropriate.
- Cross-tenant misses should return `404` where revealing record existence would leak another buyer or seller's data.

## Added Test Coverage

`AuthorizationAuditTests` covers:

- Anonymous users cannot access seller product endpoints.
- Buyers cannot access seller product endpoints.
- Sellers cannot access admin marketplace reports.
- Sellers cannot read another seller's product.
- Buyers cannot read another buyer's order.
- Sellers cannot read another seller's order.

## Residual Follow-Up

- Keep adding explicit authorization tests when new endpoint groups are introduced.
- Prompt 58 should attach rate limits to public and abuse-prone endpoints.
- Prompt 59 should tighten webhook security documentation and tests around signature enforcement and duplicate processing.
