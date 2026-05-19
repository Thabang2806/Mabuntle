# Security Review

Last reviewed: 2026-05-19

## Critical Issues

None identified in the focused review.

## High Issues

- Production defaults include usable secrets. `backend/src/Swyftly.Api/appsettings.json` contains default database credentials and a static JWT signing key. Move secrets to environment variables or a secret store, and fail startup when production uses a missing, default, or weak `Jwt:SigningKey`.
- Login does not use Identity lockout protections. `AuthEndpoints.LoginAsync` calls `CheckPasswordAsync` directly, so account-level credential stuffing protection is missing. Use `SignInManager` or explicit failed-attempt tracking and lockout.
- Anonymous ad impressions are an unthrottled write endpoint. Add rate limiting to impression tracking, stronger dedupe for requests without `AnonymousVisitorId`, and abuse controls before production traffic.
- Payment webhooks are anonymous, unthrottled, and read the full request body into memory. Add webhook-specific rate limiting, request-size limits, and content-type validation before parsing.

## Medium Issues

- Refresh token rotation does not revoke the replacement chain when a revoked token is replayed, and `/refresh` plus `/logout` are not rate-limited.
- Angular stores access and refresh tokens in `sessionStorage`, which is XSS-sensitive. Prefer HttpOnly secure refresh-token cookies and short-lived in-memory access tokens for production.
- Public seller storefront lookup does not currently require `IsPublished` or verified/non-suspended seller status.
- Raw payment webhook payloads are persisted without redaction, retention, or encryption-at-rest policy.
- Refund and payout execution use broad `AdminOnly` authorization. Finance-specific roles, SuperAdmin-only execution, or dual control should be considered.

## Low Issues

- The Angular auth interceptor attaches bearer tokens to every `HttpClient` request. Restrict it to the configured Swyftly API origin before third-party HTTP calls are introduced.

## Suggested Remediation Tasks

- Replace production secrets in `appsettings.json` with placeholders and add startup validation for JWT signing key strength/default values.
- Add Identity lockout configuration and login tests for lockout behavior.
- Add rate-limit policies for refresh/logout, ad impressions, and payment webhooks.
- Add webhook request-size and content-type checks.
- Add storefront publication and seller-status filters to public seller pages.
- Define payment webhook payload redaction and retention rules.
- Add finance-specific authorization policies for refunds and payouts.

## Tests To Add

- Login lockout after repeated failed attempts.
- Refresh token replay revokes token family.
- Public seller storefront hides unpublished storefronts and suspended/unverified sellers.
- Payment webhook rejects oversized or invalid-content-type requests.
- Ad impression rate limit returns `429`.
- Auth interceptor does not attach tokens to non-Swyftly URLs.
