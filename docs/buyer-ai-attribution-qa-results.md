# Phase 11K Buyer AI Attribution QA Results

Date: 2026-05-29

## Scope

Phase 11K validates the reporting path added in Phase 11J: sanitized buyer AI discovery events should attribute to later cart, checkout, order, and paid-order outcomes without direct database mutation.

Out of scope: personalization, AI ranking changes, payment-provider changes, carrier work, SMS/push, marketing automation, and raw prompt/image storage.

## Helper Added

Command:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\create-buyer-ai-attribution-demo.ps1 -Password "UseYourOwnDevPassword1!" -SkipCertificateCheck
```

The helper uses only existing APIs:

| Step | API family |
|---|---|
| Buyer/admin login | `/api/auth/login` |
| Product lookup | `/api/products/{slug}` |
| AI discovery | `/api/buyer/ai/shopping-assistant`, `/api/buyer/ai/visual-search` |
| Telemetry | `/api/buyer/growth-events` |
| Cart and checkout | `/api/cart/items`, `/api/cart/shipping-options`, `/api/orders/from-cart` |
| Payment | `/api/payments/initiate`, `/api/payments/webhook/Fake` |
| Reporting | `/api/admin/reports/buyer-growth` |

The helper prints order/payment/provider-reference evidence and aggregate outcome counts for product opens, cart adds, checkout starts, orders, and paid orders. It does not write `buyer_growth_outcomes` directly.

## Reporting Polish

The admin buyer-growth trend table now labels the outcome column as `Attributed opens` and displays `attributedProductOpenCount`, so raw product-open telemetry is not confused with attributed outcome rows.

## Manual Evidence Status

Live API/browser execution is pending for this phase in the current workspace session. To sign off manually:

1. Seed sample products with reset passwords.
2. Start the API with the local `Fake` payment provider.
3. Run the helper for Assistant and VisualSearch.
4. Open `/admin/reports` at desktop `1440px` and mobile `390px` or `430px`.
5. Confirm the outcome cards and source-tool rows are readable and aggregate-only.

## Deferred Follow-Ups

- Negative-control QA should use a clean database or a buyer without recent AI telemetry because the attribution window is 7 days and shop handoff can intentionally attribute a later ordinary cart/order.
- Browser screenshots for `/assistant`, `/visual-search`, `/cart`, `/checkout`, `/checkout/success`, and `/admin/reports` can be added during the next live QA pass.
