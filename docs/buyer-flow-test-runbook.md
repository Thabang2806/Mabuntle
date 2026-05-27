# Buyer Flow Test Runbook

Use this runbook to test the local buyer journey with realistic development data.

## Seed Data

Run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\seed-dev-users.ps1 -Password "UseYourOwnDevPassword1!" -ApplyMigrations -SeedSampleProducts
```

The seed is idempotent. It reuses the existing development users and matches sample products by seller id plus slug.

## Seeded Accounts

| Email | Role | Notes |
|---|---|---|
| `buyer@swyftly.local` | Buyer | Includes a default saved delivery address when sample products are seeded. |
| `seller@swyftly.local` | Seller | Verified seller with published storefront, seller balance, standard delivery method, and sample products. |
| `admin@swyftly.local` | SuperAdmin, Admin | Available for admin checks if needed. |

Use the password you passed to the seed script.

## Seeded Product Routes

| Product | Route |
|---|---|
| Rose Linen Midi Dress | `/product/rose-linen-midi-dress` |
| Ivory Silk Wrap Blouse | `/product/ivory-silk-wrap-blouse` |
| Black Structured Leather Tote | `/product/black-structured-leather-tote` |
| Champagne Mini Crossbody Bag | `/product/champagne-mini-crossbody-bag` |
| Gold Polished Hoop Earrings | `/product/gold-polished-hoop-earrings` |
| Silver Stacking Ring Set | `/product/silver-stacking-ring-set` |
| Hydrating Cream Cleanser | `/product/hydrating-cream-cleanser` |
| Soft Matte Foundation | `/product/soft-matte-foundation` |

Seller storefront route: `/seller/swyftly-dev-store`.

## Manual Buyer Flow Checklist

1. Register a new buyer at `/register/buyer`; confirm the success screen links back to `/login`.
2. Login as `buyer@swyftly.local`.
3. Browse `/`, `/shop`, product detail pages, category pages, and `/seller/swyftly-dev-store`.
4. Save at least one product to wishlist from a product card or product detail.
5. Open `/account/wishlist`, select a variant, and move a saved product to cart.
6. Open `/cart`, update quantity, remove an item, and save one cart item for later.
7. Start checkout from `/cart`.
8. Confirm the seeded saved address is selected by default.
9. Check delivery options and select the seeded seller delivery method.
10. Start checkout and verify payment initiation redirects through the configured fake/provider checkout URL.
11. Visit `/checkout/success?orderId=...` or `/checkout/failed?orderId=...` and confirm copy states that paid status is webhook-confirmed.
12. Visit `/account`, `/account/settings`, `/account/orders`, `/account/wishlist`, and `/account/notifications`.

## API Smoke Checks

With the API running locally:

```powershell
curl https://localhost:7268/api/products/search
curl https://localhost:7268/api/products/rose-linen-midi-dress
```

Checkout shipping options require an authenticated buyer session, so verify that path through the Angular UI or Swagger after logging in.

## Expected Boundaries

- The seed does not create carts, orders, payments, returns, reviews, or wishlists.
- Fake payment remains local/provider-neutral. Paid status still requires the existing signed webhook flow.
- Product images are local static SVG assets under `frontend/swyftly-web/public/assets/sample-products`.
