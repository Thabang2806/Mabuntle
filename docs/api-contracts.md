# Swyftly API Contracts

## Health

Responses include an `X-Correlation-ID` header. Callers may provide the same header on the request; otherwise the API generates one.

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

The readiness endpoint returns HTTP `503` with the same response shape when PostgreSQL is unavailable. It also includes provider placeholder checks for search, storage, and payment dependencies until real external providers are selected.

## Authentication

Auth endpoints are under:

```http
/api/auth
```

Public registration supports only `Buyer` and `Seller`. `Admin`, `SuperAdmin`, and `SupportAgent` are reserved roles and cannot be self-assigned.

```http
POST /api/auth/register
```

Request:

```json
{
  "email": "buyer@example.com",
  "password": "Password123",
  "role": "Buyer"
}
```

Response:

```json
{
  "userId": "00000000-0000-0000-0000-000000000000",
  "email": "buyer@example.com",
  "role": "Buyer",
  "sellerVerificationStatus": null,
  "emailVerificationRequired": false
}
```

Seller registration returns `"sellerVerificationStatus": "PendingVerification"`.

```http
POST /api/auth/login
POST /api/auth/refresh
POST /api/auth/logout
GET /api/auth/me
```

Login and refresh return a JWT access token plus refresh token:

```json
{
  "userId": "00000000-0000-0000-0000-000000000000",
  "email": "buyer@example.com",
  "roles": ["Buyer"],
  "accessToken": "<jwt>",
  "accessTokenExpiresAtUtc": "2026-05-18T10:30:00+00:00",
  "refreshToken": "<refresh-token>",
  "refreshTokenExpiresAtUtc": "2026-06-01T10:00:00+00:00"
}
```

The API never returns password hashes. Refresh tokens are stored server-side as hashes.

Scaffold-only policy check endpoints exist for test coverage:

```http
GET /api/auth/policy-checks/admin
GET /api/auth/policy-checks/seller
```

## Seller Onboarding

Seller onboarding endpoints require a seller JWT role and always operate on the authenticated seller profile.

```http
GET /api/seller/onboarding
PUT /api/seller/onboarding/profile
PUT /api/seller/onboarding/storefront
PUT /api/seller/onboarding/address
PUT /api/seller/onboarding/payout
POST /api/seller/onboarding/submit-verification
```

`PUT /api/seller/onboarding/payout` stores only a payout provider reference placeholder. It does not store bank details or integrate a payment provider.

`POST /api/seller/onboarding/submit-verification` returns `400` until profile, storefront, address, and payout placeholder fields are complete. A successful submission changes the seller verification status to `UnderReview`; admin approval is not part of this endpoint set.

## Admin Seller Approval

Admin seller approval endpoints require an `Admin` or `SuperAdmin` JWT role.

```http
GET /api/admin/sellers/pending
GET /api/admin/sellers/{sellerId}
POST /api/admin/sellers/{sellerId}/approve
POST /api/admin/sellers/{sellerId}/reject
POST /api/admin/sellers/{sellerId}/suspend
```

`POST /approve` verifies a seller when required onboarding data and the payout placeholder exist. `POST /reject` and `POST /suspend` require a JSON body:

```json
{
  "reason": "Documents are not clear."
}
```

Admin actions write audit-log entries and seller detail responses include `auditTrail`.

## Admin Audit Logs

Admin audit-log endpoints require an `Admin` or `SuperAdmin` JWT role.

```http
GET /api/admin/audit-logs
```

Supported query filters:

- `actionType`
- `entityType`
- `entityId`
- `actorUserId`
- `fromUtc`
- `toUtc`
- `pageNumber`
- `pageSize`, capped at `100`

Response:

```json
{
  "items": [
    {
      "id": "00000000-0000-0000-0000-000000000000",
      "actorUserId": "00000000-0000-0000-0000-000000000000",
      "actorRole": "Admin",
      "actionType": "ProductApproved",
      "entityType": "Product",
      "entityId": "00000000-0000-0000-0000-000000000000",
      "previousValueJson": "{\"status\":\"PendingReview\"}",
      "newValueJson": "{\"status\":\"Published\"}",
      "reason": "Manual review complete.",
      "ipAddress": "127.0.0.1",
      "createdAtUtc": "2026-05-18T10:00:00+00:00"
    }
  ],
  "pageNumber": 1,
  "pageSize": 50,
  "totalCount": 1
}
```

Seller approval/rejection/suspension, product approval/rejection/change-request, payout hold/release, refund approval, dispute resolution, and ad-campaign approval/rejection workflows write audit logs through the shared audit logging service. Future role-change and sensitive admin actions should use the same service.

## Admin Dashboard

Admin dashboard endpoints require an `Admin` or `SuperAdmin` JWT role.

```http
GET /api/admin/dashboard/summary
```

Response:

```json
{
  "pendingSellerApprovals": 2,
  "pendingProductReviews": 4,
  "newOrdersToday": 6,
  "openDisputes": 1,
  "pendingRefunds": 3,
  "pendingPayouts": 5,
  "totalGrossSalesPlaceholder": 0,
  "platformCommissionPlaceholder": 0
}
```

## Admin Marketplace Reports

Admin marketplace report endpoints require an `Admin` or `SuperAdmin` JWT role. Reports use `fromUtc` and `toUtc` query filters; when omitted, the API defaults to the last 30 days.

```http
GET /api/admin/reports/marketplace?fromUtc=2026-05-01T00:00:00.000Z&toUtc=2026-05-19T00:00:00.000Z
GET /api/admin/reports/marketplace/export.csv?fromUtc=2026-05-01T00:00:00.000Z&toUtc=2026-05-19T00:00:00.000Z
```

Response:

```json
{
  "fromUtc": "2026-05-01T00:00:00+00:00",
  "toUtc": "2026-05-19T00:00:00+00:00",
  "generatedAtUtc": "2026-05-19T10:00:00+00:00",
  "currency": "ZAR",
  "finance": {
    "grossMerchandiseValue": 1200.00,
    "platformCommissionEarned": 120.00,
    "paymentProcessingFees": 36.00,
    "refunds": 150.00,
    "sellerPendingBalances": 300.00,
    "sellerAvailableBalances": 900.00,
    "sellerHeldBalances": 50.00,
    "payoutsProcessed": 500.00,
    "failedPayouts": 100.00
  },
  "operations": {
    "orderCount": 4,
    "refundCount": 1,
    "payoutsProcessedCount": 2,
    "failedPayoutCount": 1,
    "disputeCount": 1,
    "activeDisputeCount": 1
  },
  "topSellers": [
    {
      "sellerId": "00000000-0000-0000-0000-000000000000",
      "sellerDisplayName": "Seller Store",
      "orderCount": 2,
      "grossMerchandiseValue": 700.00,
      "itemsSold": 3
    }
  ],
  "topCategories": [
    {
      "categoryId": "00000000-0000-0000-0000-000000000000",
      "categoryName": "Dresses",
      "quantitySold": 3,
      "revenue": 700.00
    }
  ],
  "csvExportUrl": "/api/admin/reports/marketplace/export.csv?fromUtc=..."
}
```

`grossMerchandiseValue` is derived from paid-or-later order item subtotals created inside the range, excluding shipping, discounts, and platform fee adjustments. Platform commission and payment processing fees are derived from ledger entries inside the range. Seller pending, available, and held balances are current balance snapshots, not historical balance snapshots. Processed and failed payouts are filtered by `UpdatedAtUtc` because the payout aggregate does not yet have dedicated terminal timestamps. The CSV export contains aggregate summary rows only and does not expose buyer-level or raw ledger rows.

## Admin AI Usage Analytics

Admin AI usage analytics require an `Admin` or `SuperAdmin` JWT role. Filters are optional: `fromUtc`, `toUtc`, `featureName`, and `sellerId`. When dates are omitted, the API defaults to the last 30 days.

```http
GET /api/admin/analytics/ai-usage?fromUtc=2026-05-01T00:00:00.000Z&toUtc=2026-05-19T00:00:00.000Z&featureName=ListingAssistant&sellerId=00000000-0000-0000-0000-000000000000
```

Response:

```json
{
  "fromUtc": "2026-05-01T00:00:00+00:00",
  "toUtc": "2026-05-19T00:00:00+00:00",
  "generatedAtUtc": "2026-05-19T10:00:00+00:00",
  "featureName": "ListingAssistant",
  "sellerId": "00000000-0000-0000-0000-000000000000",
  "totals": {
    "requests": 3,
    "successfulRequests": 2,
    "failedRequests": 1,
    "failureRate": 0.3333,
    "inputTokens": 250,
    "outputTokens": 210,
    "estimatedCost": 0.04,
    "averageLatencyMs": 150
  },
  "suggestions": {
    "productSuggestionsGenerated": 2,
    "productSuggestionsAccepted": 1,
    "suggestionAcceptanceRate": 0.5,
    "productSuggestionsApplied": 1,
    "productsTouchedByAi": 2,
    "productsImprovedWithAi": 1,
    "averageListingQualityScore": 70,
    "averageQualityScoreImprovement": null,
    "qualityScoreImprovementNote": "Pre-AI baseline quality scores are not captured yet; improvement is unavailable until baseline capture is added.",
    "fieldAuditCount": 2,
    "fieldValuesAccepted": 1,
    "fieldValuesEdited": 1
  },
  "moderation": {
    "moderationChecks": 1,
    "adminReviewFlags": 1,
    "lowRiskFlags": 0,
    "mediumRiskFlags": 0,
    "highRiskFlags": 1
  },
  "featureUsage": [],
  "modelUsage": [],
  "topSellers": []
}
```

The endpoint aggregates existing `AiUsageLog`, `AiProductSuggestion`, `AiSuggestionFieldAudit`, and `AiModerationResult` data only. It does not call an AI provider. `averageQualityScoreImprovement` is intentionally nullable because Swyftly does not yet persist a pre-AI baseline listing quality score.

## Buyer AI Shopping Intent

Prompt 61 added backend intent extraction contracts. Prompt 62 adds the buyer-only recommendation endpoint that uses those contracts and returns real Swyftly products only.

Application contracts:

- `IAiShoppingIntentService`
- `IAiShoppingIntentProvider`
- `ShoppingIntentExtractionRequest`
- `ShoppingIntent`

The fake provider extracts structured intent fields from buyer text, including category, subcategory, budget, size, colour, occasion, style, material, brand, beauty skin type, beauty concern, and search text. Vague requests return `isVague: true` with a clarification prompt instead of inventing products.

Buyer assistant endpoint:

```http
POST /api/buyer/ai/shopping-assistant
```

Request:

```json
{
  "message": "Show me a black dress in size medium under R1,500."
}
```

Response:

```json
{
  "intent": {
    "category": "Dresses",
    "subcategory": null,
    "budgetMax": 1500,
    "budgetMin": null,
    "size": "M",
    "colour": "Black",
    "occasion": null,
    "style": null,
    "material": null,
    "brand": null,
    "beautySkinType": null,
    "beautyConcern": null,
    "searchText": "Show me a black dress in size medium under R1,500.",
    "isVague": false,
    "clarificationPrompt": null
  },
  "products": [
    {
      "productId": "00000000-0000-0000-0000-000000000000",
      "title": "Black Wedding Dress",
      "slug": "black-wedding-dress",
      "sellerDisplayName": "Assistant Seller",
      "imageUrl": "https://example.test/black-dress.jpg",
      "price": 999,
      "currency": "ZAR",
      "matchReasons": ["Available in Black.", "Available in size M."]
    }
  ],
  "summary": "These matches come only from published Swyftly products returned by the backend search.",
  "safetyNote": null
}
```

The endpoint requires a `Buyer` JWT role. It searches published products with active sellable stock and returns only product ids found by backend queries. It does not invent products, prices, sellers, delivery promises, or stock availability. Beauty requests include a product-discovery safety note and avoid medical advice.

Angular buyer route:

```http
/assistant
```

## Buyer AI Visual Search

Visual search requires a `Buyer` JWT role. The MVP accepts either an image reference or base64 image data from an upload. Uploaded image data is processed only for the request and is not persisted by the API.

```http
POST /api/buyer/ai/visual-search
```

Request:

```json
{
  "imageReference": "black formal maxi dress flatlay",
  "imageDataBase64": null,
  "fileName": "black-dress.jpg",
  "contentType": "image/jpeg"
}
```

Response:

```json
{
  "attributes": {
    "category": "Dresses",
    "colour": "Black",
    "style": "Formal",
    "shape": "Maxi",
    "pattern": null,
    "materialGuess": null,
    "materialConfidence": null,
    "confidence": 0.72,
    "searchText": "Dresses Black Formal Maxi",
    "warnings": [
      "Material and brand are not inferred unless visible context is explicit."
    ]
  },
  "products": [
    {
      "productId": "00000000-0000-0000-0000-000000000000",
      "title": "Black Formal Maxi Dress",
      "slug": "black-formal-maxi-dress",
      "sellerDisplayName": "Visual Seller",
      "imageUrl": "https://example.test/black-formal-maxi-dress.jpg",
      "price": 999,
      "currency": "ZAR",
      "matchReasons": ["Matches visual category Dresses.", "Available in Black."]
    }
  ],
  "summary": "These matches use extracted visual attributes against published Swyftly products only.",
  "imageRetentionNote": "Uploaded image data is processed for this request only and is not persisted by the visual search MVP."
}
```

The fake vision provider is deterministic for local development and tests. It extracts category, colour, style, shape, pattern, and low-confidence material guesses from image references or file names. The endpoint searches only published products with active sellable stock and returns only product ids found by backend queries. It does not infer exact brand or verified material from an image.

Angular buyer route:

```http
/visual-search
```

The admin dashboard landing page returns aggregate operational counts only. It intentionally does not expose buyer or seller detail records on the landing page. Dedicated finance and AI analytics are exposed through the admin reports routes above.

Angular admin routes now include:

```http
/admin
/admin/sellers
/admin/products
/admin/orders
/admin/payments
/admin/reports
/admin/ai-usage
/admin/refunds
/admin/disputes
/admin/payouts
/admin/ads
/admin/ads/:id
```

Routes without a dedicated workflow page yet use protected foundation placeholders.

## Admin Categories

Category metadata endpoints require an `Admin` or `SuperAdmin` JWT role.

```http
GET /api/admin/categories
```

The response is a flat category list with parent ids and attribute definitions:

```json
[
  {
    "categoryId": "20000000-0000-0000-0000-000000000003",
    "parentCategoryId": "20000000-0000-0000-0000-000000000002",
    "name": "Dresses",
    "slug": "women-clothing-dresses",
    "displayOrder": 10,
    "isActive": true,
    "attributes": [
      {
        "attributeId": "30000000-0000-0000-0000-000000000001",
        "name": "Size",
        "key": "size",
        "dataType": "Select",
        "isRequired": true,
        "allowedValues": ["XS", "S", "M", "L", "XL"],
        "displayOrder": 10,
        "isActive": true
      }
    ]
  }
]
```

## Public Catalog And Product Search

Public catalog endpoints do not require authentication.

```http
GET /api/products/search
GET /api/products/{slug}
GET /api/categories
GET /api/sellers/{storeSlug}
```

`GET /api/products/search` uses PostgreSQL as the first search backend and returns only `Published` products. Supported query filters:

- `query`
- `categoryId`
- `categorySlug`
- `sellerId`
- `minPrice`
- `maxPrice`
- `size`
- `colour`
- `brandId`
- `material`
- `inStock`
- `sort`: `newest`, `price_asc`, `price_desc`, `relevance`
- `page`
- `pageSize`, capped at `60`

Response:

```json
{
  "items": [
    {
      "productId": "00000000-0000-0000-0000-000000000000",
      "sellerId": "00000000-0000-0000-0000-000000000000",
      "sellerStoreName": "Seller Store",
      "sellerStoreSlug": "seller-store",
      "categoryId": "20000000-0000-0000-0000-000000000003",
      "categoryPath": "Women > Clothing > Dresses",
      "brandId": null,
      "title": "Summer Dress",
      "slug": "summer-dress",
      "shortDescription": "A lightweight summer dress.",
      "primaryImageUrl": "https://example.test/summer-dress.jpg",
      "primaryImageAltText": "Summer dress",
      "priceMin": 499.99,
      "compareAtPriceMin": 699.99,
      "inStock": true,
      "tags": ["summer"],
      "publishedAtUtc": "2026-05-18T10:00:00+00:00"
    }
  ],
  "page": 1,
  "pageSize": 24,
  "totalCount": 1,
  "sort": "newest"
}
```

Out-of-stock handling is explicit through `inStock`: by default, published products can appear even if all active variants are unavailable; `inStock=true` restricts results to products with at least one active variant where stock exceeds reserved quantity.

`GET /api/products/{slug}` returns public product detail with images, variants, attributes, and the product card payload. Current product slugs are seller-scoped in persistence, so duplicate public slugs are still a known future routing issue.

Search indexing is prepared behind `ISearchIndexService` and `IProductSearchIndexer`. Published products are indexed after admin approval into the current local in-memory placeholder. If the search index has no usable data, public search falls back to PostgreSQL.

Product embeddings are prepared behind `IAiEmbeddingService` and `IProductEmbeddingGenerator`. Published products generate or replace a private `product_embeddings` row after admin approval using the current fake embedding provider. No public semantic-search API exists yet.

Inventory reservation support is prepared behind `IInventoryReservationService`. Order creation calls this service when checkout starts. The service creates active reservations from cart items, increments variant reserved quantities inside a database transaction, expires reservations after the configured duration, and releases stock on expiry. No public standalone reservation endpoint exists.

Angular public routes using these endpoints:

```http
/shop
/category/:slug
/product/:slug
/seller/:storeSlug
```

Product detail pages include buyer add-to-cart controls. Unauthenticated or non-buyer users are routed to sign in before adding items.

## Buyer Cart

Cart endpoints require a buyer JWT role. A buyer has at most one active cart, and the MVP cart can contain products from only one seller.

```http
GET /api/cart
POST /api/cart/items
PUT /api/cart/items/{itemId}
DELETE /api/cart/items/{itemId}
DELETE /api/cart
```

`POST /api/cart/items` adds to an existing variant quantity when the variant is already in the cart. `PUT /api/cart/items/{itemId}` sets the item quantity. Quantity must be positive and cannot exceed the product variant's available stock, calculated as stock minus reserved quantity. Cart item unit price is captured for display only; final order pricing must be confirmed during checkout.

Add item request:

```json
{
  "productVariantId": "00000000-0000-0000-0000-000000000000",
  "quantity": 2
}
```

Cart response:

```json
{
  "cartId": "00000000-0000-0000-0000-000000000000",
  "buyerId": "00000000-0000-0000-0000-000000000000",
  "sellerId": "00000000-0000-0000-0000-000000000000",
  "sellerStoreName": "Seller Store",
  "items": [
    {
      "cartItemId": "00000000-0000-0000-0000-000000000000",
      "productId": "00000000-0000-0000-0000-000000000000",
      "productVariantId": "00000000-0000-0000-0000-000000000000",
      "productTitle": "Summer Dress",
      "sku": "SKU-1",
      "size": "M",
      "colour": "Black",
      "unitPrice": 499.99,
      "quantity": 2,
      "lineTotal": 999.98
    }
  ],
  "totalQuantity": 2,
  "subtotal": 999.98
}
```

Trying to add a product from a different seller returns a validation problem. `DELETE /api/cart` clears the active cart and returns `204`.

Angular buyer cart route:

```http
/cart
```

The cart page shows cart items, quantities, seller name, single-seller checkout notice, subtotal, quantity update, remove item, and checkout navigation.

## Orders

Order endpoints use JWT roles. Buyer endpoints operate only on the authenticated buyer's orders. Seller endpoints operate only on orders for the authenticated seller.

```http
POST /api/orders/from-cart
GET /api/orders
GET /api/orders/{orderId}
GET /api/buyer/orders
GET /api/buyer/orders/{orderId}
GET /api/seller/orders
GET /api/seller/orders/{orderId}
POST /api/seller/orders/{orderId}/mark-processing
POST /api/seller/orders/{orderId}/tracking
POST /api/seller/orders/{orderId}/mark-shipped
```

`POST /api/orders/from-cart` creates a `PendingPayment` order from the authenticated buyer's active cart and reserves inventory for the cart items. Repeating the request for the same active cart returns the existing pending-payment order instead of creating a duplicate. The cart is not cleared during this prompt because payment confirmation and reservation confirmation are still future work.

Request:

```json
{
  "cartId": null,
  "reservationMinutes": null
}
```

`cartId` is optional; when omitted, the buyer's active cart is used. `reservationMinutes` is optional and defaults to 15 minutes.

Response:

```json
{
  "orderId": "00000000-0000-0000-0000-000000000000",
  "buyerId": "00000000-0000-0000-0000-000000000000",
  "sellerId": "00000000-0000-0000-0000-000000000000",
  "cartId": "00000000-0000-0000-0000-000000000000",
  "status": "PendingPayment",
  "items": [
    {
      "orderItemId": "00000000-0000-0000-0000-000000000000",
      "productId": "00000000-0000-0000-0000-000000000000",
      "productVariantId": "00000000-0000-0000-0000-000000000000",
      "productTitle": "Summer Dress",
      "sku": "SKU-1",
      "size": "M",
      "colour": "Black",
      "unitPrice": 499.99,
      "quantity": 2,
      "lineTotal": 999.98
    }
  ],
  "itemsSubtotal": 999.98,
  "shippingAmount": 0,
  "platformFeeAmount": 0,
  "discountAmount": 0,
  "totalAmount": 999.98,
  "statusHistory": [
    {
      "statusHistoryId": "00000000-0000-0000-0000-000000000000",
      "previousStatus": null,
      "newStatus": "PendingPayment",
      "changedAtUtc": "2026-05-18T10:00:00+00:00",
      "reason": "OrderCreated"
    }
  ],
  "shipments": [
    {
      "shipmentId": "00000000-0000-0000-0000-000000000000",
      "status": "InTransit",
      "carrierName": "Courier One",
      "trackingNumber": "TRACK-123",
      "trackingUrl": "https://tracking.example/TRACK-123",
      "shippedAtUtc": "2026-05-18T10:30:00+00:00",
      "deliveredAtUtc": null,
      "events": [
        {
          "shipmentEventId": "00000000-0000-0000-0000-000000000000",
          "status": "InTransit",
          "eventType": "ShipmentInTransit",
          "message": "Shipment was marked as shipped.",
          "carrierName": "Courier One",
          "trackingNumber": "TRACK-123",
          "occurredAtUtc": "2026-05-18T10:30:00+00:00"
        }
      ]
    }
  ]
}
```

Shipping, platform fee, and discount values are placeholders for later checkout/payment prompts and currently default to zero.

Manual fulfilment starts after payment has moved the order to `Paid`. `POST /mark-processing` changes the order to `Processing` and creates an `AwaitingFulfilment` shipment if one does not exist. `POST /tracking` accepts:

```json
{
  "carrierName": "Courier One",
  "trackingNumber": "TRACK-123",
  "trackingUrl": "https://tracking.example/TRACK-123",
  "note": "Collected by courier."
}
```

Tracking can be added to paid or fulfilment orders and always writes a shipment event. `POST /mark-shipped` changes the order to `Shipped`, moves the current shipment to `InTransit`, and writes a shipment event. `GET /api/buyer/orders` and `GET /api/buyer/orders/{orderId}` are buyer-specific aliases for the existing buyer order reads.

`Delivered` exists in the order and shipment status model, but no delivered endpoint is exposed yet. That is intentional because delivery confirmation, return eligibility windows, and payout availability need to be designed together in the next prompts.

## Returns

Return endpoints use JWT roles. Buyers can request returns only for their own delivered orders. Sellers can view/respond only to returns for their own orders. Admin return endpoints require `Admin` or `SuperAdmin`.

```http
POST /api/buyer/orders/{orderId}/returns
GET /api/buyer/returns
GET /api/buyer/returns/{returnRequestId}
POST /api/buyer/returns/{returnRequestId}/dispute
GET /api/seller/returns
GET /api/seller/returns/{returnRequestId}
POST /api/seller/returns/{returnRequestId}/approve
POST /api/seller/returns/{returnRequestId}/reject
GET /api/admin/returns/disputed
```

Create return request:

```json
{
  "reason": "DamagedItem",
  "details": "The item arrived damaged.",
  "items": [
    {
      "orderItemId": "00000000-0000-0000-0000-000000000000",
      "quantity": 1,
      "reason": "DamagedItem",
      "isOpenedOrUnsealed": false,
      "note": "Torn seam."
    }
  ]
}
```

Return response:

```json
{
  "returnRequestId": "00000000-0000-0000-0000-000000000000",
  "orderId": "00000000-0000-0000-0000-000000000000",
  "buyerId": "00000000-0000-0000-0000-000000000000",
  "sellerId": "00000000-0000-0000-0000-000000000000",
  "status": "AwaitingSellerResponse",
  "reason": "DamagedItem",
  "details": "The item arrived damaged.",
  "requestedAtUtc": "2026-05-18T10:00:00+00:00",
  "sellerRespondedAtUtc": null,
  "sellerResponseReason": null,
  "disputedAtUtc": null,
  "disputeReason": null,
  "items": [],
  "messages": []
}
```

Creating a valid return changes the order to `ReturnRequested` and places linked pending/available seller payouts on hold. The current opened/unsealed rule blocks changed-mind returns for opened or unsealed items; category-specific beauty policy remains a later refinement.

Seller approve/reject requests use:

```json
{
  "message": "Return approved."
}
```

Reject requires a message. Buyers can dispute only rejected returns:

```json
{
  "reason": "Please review the listing photos."
}
```

Disputing a rejected return changes the return to `Disputed` and the order to `Disputed`. The standalone dispute workflow below should be used when messages, evidence, and admin final decisions are needed.

## Disputes

Dispute endpoints use JWT roles. Buyers can open disputes for their own eligible orders or returns. Sellers can respond only to disputes for their own orders. Admin dispute endpoints require `Admin` or `SuperAdmin`.

```http
POST /api/buyer/orders/{orderId}/disputes
POST /api/buyer/returns/{returnRequestId}/disputes
GET /api/buyer/disputes
POST /api/buyer/disputes/{disputeId}/messages
POST /api/buyer/disputes/{disputeId}/evidence
GET /api/seller/disputes
POST /api/seller/disputes/{disputeId}/messages
POST /api/seller/disputes/{disputeId}/evidence
GET /api/admin/disputes
POST /api/admin/disputes/{disputeId}/resolve
```

Open dispute request:

```json
{
  "reason": "Item appears counterfeit.",
  "evidence": [
    {
      "evidenceType": "Image",
      "storageReference": "uploads/disputes/photo.jpg",
      "description": "Logo mismatch."
    }
  ]
}
```

Message request:

```json
{
  "message": "Supplier certificate attached."
}
```

Evidence request:

```json
{
  "evidenceType": "Document",
  "storageReference": "uploads/disputes/certificate.pdf",
  "description": "Supplier certificate."
}
```

Resolve request:

```json
{
  "outcome": "SellerFavoured",
  "reason": "Seller evidence accepted."
}
```

Supported resolution outcomes are `BuyerFavoured` and `SellerFavoured`. Opening an active dispute changes the order to `Disputed` and holds linked pending/available seller payouts. Seller-favoured resolution releases linked held payouts back to pending. Buyer-favoured resolution keeps payout funds held so a refund/manual recovery workflow can complete.

Dispute response:

```json
{
  "disputeId": "00000000-0000-0000-0000-000000000000",
  "orderId": "00000000-0000-0000-0000-000000000000",
  "returnRequestId": null,
  "buyerId": "00000000-0000-0000-0000-000000000000",
  "sellerId": "00000000-0000-0000-0000-000000000000",
  "status": "AwaitingSeller",
  "reason": "Item appears counterfeit.",
  "openedAtUtc": "2026-05-18T10:00:00+00:00",
  "resolvedAtUtc": null,
  "resolutionReason": null,
  "messages": [],
  "evidence": []
}
```

## Support Tickets

Support ticket endpoints use JWT roles. Buyers and sellers can create and read their own tickets. Support agents, admins, and super admins can read all tickets, respond publicly, add private internal notes, resolve tickets, and close tickets.

```http
POST /api/buyer/support-tickets
GET /api/buyer/support-tickets
GET /api/buyer/support-tickets/{ticketId}
POST /api/buyer/support-tickets/{ticketId}/messages
POST /api/seller/support-tickets
GET /api/seller/support-tickets
GET /api/seller/support-tickets/{ticketId}
POST /api/seller/support-tickets/{ticketId}/messages
GET /api/support/tickets
GET /api/support/tickets/{ticketId}
POST /api/support/tickets/{ticketId}/messages
POST /api/support/tickets/{ticketId}/internal-notes
POST /api/support/tickets/{ticketId}/resolve
POST /api/support/tickets/{ticketId}/close
```

Create ticket request:

```json
{
  "category": "OrderIssue",
  "subject": "Order arrived damaged",
  "description": "The box arrived damaged.",
  "linkedOrderId": null,
  "linkedProductId": null,
  "linkedSellerId": null,
  "linkedPaymentId": null
}
```

Supported categories are `OrderIssue`, `PaymentIssue`, `ReturnIssue`, `SellerIssue`, `ProductIssue`, `TechnicalIssue`, and `Other`.

Message and internal-note requests use:

```json
{
  "message": "Please upload a photo of the damaged item."
}
```

Ticket response:

```json
{
  "supportTicketId": "00000000-0000-0000-0000-000000000000",
  "createdByUserId": "00000000-0000-0000-0000-000000000000",
  "createdByRole": "Buyer",
  "buyerId": "00000000-0000-0000-0000-000000000000",
  "sellerId": null,
  "category": "OrderIssue",
  "status": "WaitingForCustomer",
  "subject": "Order arrived damaged",
  "description": "The box arrived damaged.",
  "linkedOrderId": null,
  "linkedProductId": null,
  "linkedSellerId": null,
  "linkedPaymentId": null,
  "assignedSupportUserId": "00000000-0000-0000-0000-000000000000",
  "openedAtUtc": "2026-05-19T10:00:00+00:00",
  "resolvedAtUtc": null,
  "closedAtUtc": null,
  "messages": [
    {
      "supportMessageId": "00000000-0000-0000-0000-000000000000",
      "senderUserId": "00000000-0000-0000-0000-000000000000",
      "senderRole": "SupportAgent",
      "message": "Please upload a photo of the damaged item.",
      "isInternal": false,
      "createdAtUtc": "2026-05-19T10:05:00+00:00"
    }
  ]
}
```

Internal notes are included only on `/api/support/tickets` responses. Buyer and seller ticket responses filter out messages where `isInternal` is `true`. Linked order/payment records are ownership-checked for buyer and seller creation; linked product/seller ids are stored as references only and no linked object details are exposed in the support response.

## Seller Ad Campaigns

Seller ad campaign endpoints require the `Seller` JWT role and always operate on campaigns owned by the authenticated seller.

```http
POST /api/seller/ad-campaigns
GET /api/seller/ad-campaigns
GET /api/seller/ad-campaigns/{id}
PUT /api/seller/ad-campaigns/{id}
POST /api/seller/ad-campaigns/{id}/submit-review
POST /api/seller/ad-campaigns/{id}/pause
POST /api/seller/ad-campaigns/{id}/resume
POST /api/seller/ad-campaigns/{id}/cancel
```

Create/update request:

```json
{
  "name": "Launch campaign",
  "campaignType": "FeaturedProduct",
  "startsAtUtc": "2026-05-20T00:00:00+00:00",
  "endsAtUtc": "2026-06-03T00:00:00+00:00",
  "productIds": ["00000000-0000-0000-0000-000000000000"],
  "budget": {
    "currency": "ZAR",
    "dailyBudget": 100.00,
    "totalBudget": 1000.00,
    "maxCostPerClick": 5.00
  }
}
```

Supported campaign types are `FeaturedProduct`, `SponsoredSearch`, `FeaturedStorefront`, and `CategorySpotlight`.

Response:

```json
{
  "adCampaignId": "00000000-0000-0000-0000-000000000000",
  "sellerId": "00000000-0000-0000-0000-000000000000",
  "name": "Launch campaign",
  "campaignType": "FeaturedProduct",
  "status": "Draft",
  "startsAtUtc": "2026-05-20T00:00:00+00:00",
  "endsAtUtc": "2026-06-03T00:00:00+00:00",
  "submittedAtUtc": null,
  "approvedAtUtc": null,
  "pausedAtUtc": null,
  "completedAtUtc": null,
  "cancelledAtUtc": null,
  "rejectionReason": null,
  "productIds": ["00000000-0000-0000-0000-000000000000"],
  "budget": {
    "currency": "ZAR",
    "dailyBudget": 100.00,
    "totalBudget": 1000.00,
    "maxCostPerClick": 5.00,
    "spentAmount": 0
  },
  "eligibility": {
    "isEligible": true,
    "sellerReasons": [],
    "products": [
      {
        "productId": "00000000-0000-0000-0000-000000000000",
        "isEligible": true,
        "qualityScore": 100,
        "reasons": []
      }
    ]
  }
}
```

Campaign creation and updates validate seller/product eligibility. The current eligibility rules require a verified seller, no seller suspension, no dispute currently under admin review, seller-owned published products, sellable stock, no unresolved moderation flags, and a deterministic product completeness score of at least `80`. This quality score is a local completeness score until a later prompt introduces a richer advertising quality model.

`POST /submit-review` moves an eligible draft/rejected campaign to `PendingReview`. Campaigns become `Active` only through the admin ad campaign review workflow below. Pause/resume endpoints are present for the campaign state model but only work once a campaign is active/paused.

## Admin Ad Campaign Review

Admin ad campaign review endpoints require an `Admin` or `SuperAdmin` JWT role.

```http
GET /api/admin/ad-campaigns/pending
GET /api/admin/ad-campaigns/{id}
POST /api/admin/ad-campaigns/{id}/approve
POST /api/admin/ad-campaigns/{id}/reject
```

`GET /pending` returns campaigns in `PendingReview`. Campaign detail responses include seller details, promoted products, budget, current eligibility results, and campaign audit trail entries.

`POST /approve` re-runs campaign eligibility before activation. If the seller or promoted products no longer qualify, the endpoint returns a validation problem and leaves the campaign in `PendingReview`. Successful approval moves the campaign to `Active`, records the approving admin user id, and writes an `AdCampaignApproved` audit-log entry.

`POST /reject` requires a reason:

```json
{
  "reason": "Promoted products do not meet ad policy."
}
```

Rejecting moves the campaign to `Rejected`, stores the rejection reason, and writes an `AdCampaignRejected` audit-log entry.

Angular admin routes:

```http
/admin/ads
/admin/ads/:id
```

## Ad Tracking And Campaign Metrics

Public ad tracking endpoints are anonymous and return `202 Accepted` whether an event is stored or ignored. This avoids making the buyer flow depend on ad-tracking success.

```http
POST /api/ads/impressions
POST /api/ads/clicks
```

Impression request:

```json
{
  "adCampaignId": "00000000-0000-0000-0000-000000000000",
  "productId": "00000000-0000-0000-0000-000000000000",
  "placement": "shop-grid",
  "anonymousVisitorId": "visitor-session-id"
}
```

Click request:

```json
{
  "adCampaignId": "00000000-0000-0000-0000-000000000000",
  "productId": "00000000-0000-0000-0000-000000000000",
  "anonymousVisitorId": "visitor-session-id"
}
```

Response:

```json
{
  "recorded": true,
  "eventId": "00000000-0000-0000-0000-000000000000",
  "status": "ClickRecorded",
  "reason": null
}
```

The tracking service records events only for active campaigns within their flight window, campaign-linked published products, and products with sellable stock. Repeated impressions are de-duplicated over a short visitor window; repeated clicks are de-duplicated over a shorter buyer/visitor window. Clicks create ad charges using the campaign max CPC and respect daily and total campaign budget limits.

Successful payment webhook processing runs backend-only conversion attribution. It attributes paid order items to the latest buyer ad click for the same product inside the attribution window and stores conversions without exposing buyer personal data in reports.

Seller-owned campaign metrics are available for the future seller ads dashboard:

```http
GET /api/seller/ad-campaigns/{id}/metrics
```

Response:

```json
{
  "adCampaignId": "00000000-0000-0000-0000-000000000000",
  "sellerId": "00000000-0000-0000-0000-000000000000",
  "status": "Active",
  "impressions": 100,
  "clicks": 5,
  "clickThroughRate": 0.05,
  "spend": 25.00,
  "ordersGenerated": 1,
  "revenueGenerated": 499.99,
  "returnOnAdSpend": 19.9996,
  "currency": "ZAR"
}
```

Angular seller advertising routes:

```http
/seller/ads
/seller/ads/new
/seller/ads/:id
```

The seller ads UI lists campaigns, creates draft campaigns, selects products to promote, submits campaigns for admin review, shows eligibility warnings returned by the API, displays campaign metrics, and exposes pause/resume/cancel actions where the current campaign status allows them.

## Seller Analytics

Seller analytics endpoints require the `Seller` JWT role and return only aggregate seller-owned data. Buyer identities and personal details are not included.

```http
GET /api/seller/analytics/summary
```

Response:

```json
{
  "sellerId": "00000000-0000-0000-0000-000000000000",
  "totalSales": 998.00,
  "orderCount": 1,
  "averageOrderValue": 998.00,
  "conversionRatePlaceholder": 0,
  "productsSold": 2,
  "totalRefunded": 100.00,
  "refundRate": 1.0,
  "returnRate": 1.0,
  "topProducts": [
    {
      "productId": "00000000-0000-0000-0000-000000000000",
      "productTitle": "Seller One Product",
      "quantitySold": 2,
      "revenue": 998.00
    }
  ],
  "lowStockProducts": [
    {
      "productId": "00000000-0000-0000-0000-000000000000",
      "title": "Seller One Product",
      "status": "Published",
      "availableQuantity": 3,
      "lowStockVariantCount": 1
    }
  ],
  "adPerformance": {
    "campaignCount": 1,
    "impressions": 100,
    "clicks": 5,
    "clickThroughRate": 0.05,
    "spend": 25.00,
    "ordersGenerated": 1,
    "revenueGenerated": 499.00,
    "topCampaigns": []
  },
  "aiUsage": {
    "requests": 3,
    "successfulRequests": 2,
    "failedRequests": 1,
    "estimatedCost": 0.02,
    "averageLatencyMs": 100
  }
}
```

`totalSales` is gross sales from paid-or-later seller order states, excluding pending-payment and cancelled/refunded orders. Refund and return rates are count-based against seller paid-or-later order count. `conversionRatePlaceholder` remains zero until a dedicated storefront/session conversion model exists.

Angular seller analytics route:

```http
/seller/analytics
```

## Refunds

Refund endpoints require `Admin` or `SuperAdmin`.

```http
POST /api/admin/orders/{orderId}/refunds
POST /api/admin/returns/{returnRequestId}/refunds
GET /api/admin/refunds
POST /api/admin/refunds/{refundId}/approve
```

Create refund request:

```json
{
  "amount": 500.00,
  "reason": "Approved partial refund."
}
```

Approve refund request:

```json
{
  "reason": "Return approved by admin."
}
```

Refund response:

```json
{
  "refundId": "00000000-0000-0000-0000-000000000000",
  "orderId": "00000000-0000-0000-0000-000000000000",
  "paymentId": "00000000-0000-0000-0000-000000000000",
  "buyerId": "00000000-0000-0000-0000-000000000000",
  "sellerId": "00000000-0000-0000-0000-000000000000",
  "returnRequestId": null,
  "amount": 500.00,
  "currency": "ZAR",
  "status": "Refunded",
  "reason": "Approved partial refund.",
  "providerRefundReference": "fake_refund_reference",
  "failureReason": null,
  "requestedAtUtc": "2026-05-18T10:00:00+00:00",
  "approvedAtUtc": "2026-05-18T10:05:00+00:00",
  "refundedAtUtc": "2026-05-18T10:05:00+00:00",
  "events": []
}
```

Approving a refund calls the configured payment-provider abstraction, marks the payment `PartiallyRefunded` or `Refunded`, writes `RefundIssued` and `RefundReversal` ledger entries, adjusts seller balances proportionally to the original seller-pending amount, and writes a `RefundApproved` audit log. If seller balances are already insufficient, the pending balance can go negative as an explicit manual-recovery signal. Full refunds mark the order `Refunded`; partial refunds leave the order in its current status.

## Angular Checkout

Angular checkout routes:

```http
/checkout
/checkout/success
/checkout/failed
```

The checkout page displays the current cart summary, collects a non-persisted shipping address placeholder, and starts checkout by calling `POST /api/orders/from-cart`. It does not call a payment provider directly.

## Payment Provider Abstraction

Payment provider integration is prepared behind Application-layer contracts:

- `IPaymentProvider`
- `IPaymentInitiationService`
- `PaymentInitiationRequest`
- `PaymentInitiationResult`
- `PaymentVerificationRequest`
- `PaymentVerificationResult`
- `PaymentWebhookEvent`
- `PaymentProviderOptions`

The current Infrastructure implementation is `FakePaymentProvider`. It can initialize a fake checkout session, verify a fake provider reference, parse a fake webhook payload, process a fake refund, and verify an HMAC webhook signature. Prompt 38 added the abstraction only; Prompts 39-41 added the local payment, webhook, and ledger persistence described below.

Configuration keys:

```text
PaymentProvider__ProviderName
PaymentProvider__DefaultCurrency
PaymentProvider__SuccessRedirectUrl
PaymentProvider__FailureRedirectUrl
PaymentProvider__WebhookSigningSecret
PaymentProvider__FakeOutcome
```

`PaymentProvider__FakeOutcome` supports `Success` or `Failure` for development/test simulation.

## Payments And Webhooks

Buyer payment initiation requires a buyer JWT role and operates only on the authenticated buyer's pending-payment order.

```http
POST /api/payments/initiate
```

Request:

```json
{
  "orderId": "00000000-0000-0000-0000-000000000000"
}
```

Response:

```json
{
  "paymentId": "00000000-0000-0000-0000-000000000000",
  "orderId": "00000000-0000-0000-0000-000000000000",
  "provider": "Fake",
  "providerReference": "fake_reference",
  "amount": 999.98,
  "currency": "ZAR",
  "status": "Pending",
  "checkoutUrl": "http://localhost:4200/checkout/success?providerReference=fake_reference"
}
```

Payment initiation creates a local `Payment` row before calling the configured provider abstraction. If the fake provider is configured to fail, the local payment is marked `Failed`.

Payment webhooks are anonymous because provider callbacks cannot carry buyer JWTs. They still verify the provider route and require provider signature verification before parsing or persisting any webhook event. The fake provider expects the `X-Swyftly-Fake-Signature` header to contain the lowercase hex HMAC-SHA256 of the raw request body using `PaymentProvider__WebhookSigningSecret`.

```http
POST /api/payments/webhook/{provider}
```

Fake webhook payload:

```json
{
  "eventId": "evt_1",
  "eventType": "payment.paid",
  "providerReference": "fake_reference",
  "status": "Paid",
  "occurredAtUtc": "2026-05-18T12:01:00Z"
}
```

Webhook handling stores the raw event payload in `payment_events` and uses `(Provider, ProviderEventId)` to avoid duplicate processing. A successful payment webhook marks the payment and order as paid, confirms active cart reservations, creates successful-payment ledger entries, and credits seller pending balance. A failed payment webhook marks payment failed, cancels the order, cancels active reservations, and releases reserved stock.

Invalid signatures return `401` and do not create `payment_events` rows. Duplicate provider event ids return the existing processing result and do not create duplicate ledger entries. Webhook processing is wrapped in a database transaction before storing the event and applying payment/order/ledger changes.

Current successful-payment ledger entries:

- `BuyerPaymentReceived`
- `PlatformCommissionRecorded`
- `PaymentProviderFeeRecorded`
- `SellerPendingBalanceCredited`

Ledger fee configuration keys:

```text
Ledger__PlatformCommissionRatePercent
Ledger__PaymentProviderFeeRatePercent
Ledger__PaymentProviderFixedFee
```

Real payment provider SDKs, production webhook signatures, external payout release, and detailed finance admin UI are still future work.

## Seller Balances And Payouts

Seller balance and payout endpoints use JWT roles. Sellers can read only their own balances and payouts. Admin payout endpoints require `Admin` or `SuperAdmin`.

```http
GET /api/seller/balance
GET /api/seller/payouts
GET /api/admin/payouts/pending
POST /api/admin/payouts/{id}/hold
POST /api/admin/payouts/{id}/release
```

`GET /api/seller/balance` returns pending, available, and held balances per currency:

```json
{
  "sellerId": "00000000-0000-0000-0000-000000000000",
  "balances": [
    {
      "currency": "ZAR",
      "pendingBalance": 875.00,
      "availableBalance": 0,
      "heldBalance": 0
    }
  ]
}
```

Successful payment ledger processing creates a `Pending` seller payout and a payout item linked to the seller-pending ledger entry. Payouts do not become externally payable yet because delivery, returns, disputes, and payout provider processing are future prompts.

Admin hold/release request:

```json
{
  "reason": "Dispute review required."
}
```

Holding a payout changes it to `OnHold`, moves the payout amount from pending balance to held balance, and writes a `PayoutHeld` audit log. Releasing a held payout changes it back to `Pending`, moves the amount back from held to pending balance, and writes a `PayoutReleased` audit log. Releasing to `Available`, processing provider payouts, and paid-out settlement are intentionally left for later fulfilment/payout prompts.

## Admin Product Review

Admin product review endpoints require an `Admin` or `SuperAdmin` JWT role.

```http
GET /api/admin/products/pending-review
GET /api/admin/products/{productId}
POST /api/admin/products/{productId}/approve
POST /api/admin/products/{productId}/reject
POST /api/admin/products/{productId}/request-changes
```

`GET /pending-review` returns products in `PendingReview` or `NeedsAdminReview`. Product detail responses include seller status, attributes, variants, images, AI moderation results, and product audit trail entries.

`POST /approve` publishes the product only when the seller is verified. Products with unresolved high-risk moderation results require an override reason:

```json
{
  "overrideReason": "Supplier documents reviewed manually."
}
```

`POST /reject` and `POST /request-changes` require a reason:

```json
{
  "reason": "Add clearer size measurements."
}
```

Rejecting moves the product to `Rejected`; requesting changes moves it to `ChangesRequested`, which remains seller-editable so the seller can fix and resubmit the listing. Every approval, rejection, and change request writes an audit-log entry.

## Seller Product Drafts

Seller product endpoints require a seller JWT role and always operate on products owned by the authenticated seller. Pending sellers may create and edit drafts, but only verified sellers can submit products for review.

```http
GET /api/seller/catalog/categories
POST /api/seller/products
GET /api/seller/products
GET /api/seller/products/{id}
PUT /api/seller/products/{id}
POST /api/seller/products/{id}/variants
PUT /api/seller/products/{id}/variants/{variantId}
DELETE /api/seller/products/{id}/variants/{variantId}
POST /api/seller/products/{id}/images
DELETE /api/seller/products/{id}/images/{imageId}
POST /api/seller/products/{id}/submit-review
```

Product drafts support category-specific attributes as a JSON object. Stored product image records reference an uploaded image by URL/storage key only; image binary data is not stored in PostgreSQL.

Submission requires:

- Verified seller status.
- Category, title, slug, short description, and full description.
- Required category attributes.
- At least one product image.
- At least one active variant with available stock.

Submission runs business-rule moderation before the product enters the admin review queue. Counterfeit-risk wording, risky beauty claims, and missing beauty safety fields are stored in `ai_moderation_results`. Products with high-risk moderation flags move to `NeedsAdminReview`; otherwise they move to `PendingReview`.

## AI Listing Assistant

Prompt 22 added backend schema and Application DTOs for future AI listing suggestions. Prompt 23 added the backend service abstraction, prompt builder, suggestion validator, usage logger, and local fake provider.

Seller AI suggestion generation requires a seller JWT role and always operates on products owned by the authenticated seller. The product must be `Draft` or `Rejected`.

```http
POST /api/seller/products/{productId}/ai-suggestions
```

Request:

```json
{
  "sellerNotes": "Lightweight summer dress with a relaxed fit.",
  "productTypeHint": "Dress",
  "selectedCategoryId": "00000000-0000-0000-0000-000000000000",
  "knownAttributes": {
    "occasion": "summer"
  },
  "imageIds": ["00000000-0000-0000-0000-000000000000"]
}
```

Response:

```json
{
  "suggestionId": "00000000-0000-0000-0000-000000000000",
  "recommendedTitle": "AI-assisted product title",
  "titleSuggestions": ["AI-assisted product title"],
  "shortDescription": "A concise marketplace-ready product summary.",
  "fullDescription": "A draft listing description.",
  "suggestedCategoryId": null,
  "suggestedCategoryPath": null,
  "attributes": {},
  "tags": ["draft", "ai-assisted"],
  "seo": {},
  "imageAltText": {},
  "missingFields": ["brand", "material", "exact sizing"],
  "riskFlags": [],
  "qualityScore": 65
}
```

The AI assistant endpoint does not apply suggestions directly to a product. It persists a draft suggestion and records usage logs for successful, invalid, or failed provider responses. Provider secrets are not stored in the schema and Angular must not call an AI provider directly.

`seo` and `imageAltText` are response placeholders until later prompts expand the AI suggestion schema and apply workflow. A rate-limit placeholder is documented on the endpoint metadata; a concrete rate-limit policy is still future work.

Sellers can apply selected AI suggestion fields after review:

```http
POST /api/seller/products/{productId}/ai-suggestions/{suggestionId}/apply
```

Request:

```json
{
  "fieldsToApply": ["title", "shortDescription", "attributes", "tags", "imageAltText"],
  "editedValues": {
    "title": "Seller reviewed title",
    "attributes": {
      "size": "M",
      "colour": "Black"
    },
    "tags": ["summer", "reviewed"],
    "imageAltText": {
      "00000000-0000-0000-0000-000000000000": "Model wearing a black summer dress"
    }
  },
  "confirmRiskFlags": false
}
```

Supported `fieldsToApply` values are `title`, `shortDescription`, `fullDescription`, `category`, `attributes`, `tags`, and `imageAltText`. The endpoint validates product ownership, suggestion ownership, product editability, category existence, category attribute values, and product image ownership. If a suggestion has risk flags, `confirmRiskFlags` must be `true` before applying fields. Each applied field writes an `ai_suggestion_field_audits` row showing the AI value and seller-final value.

## Current Scope

Health/readiness, identity foundation, seller onboarding, admin seller approval, admin product review, admin dashboard summary, admin category metadata, admin marketplace finance reports, public product search, buyer-facing shop/category/product/seller/cart/checkout/assistant/visual-search pages, seller product draft endpoints, buyer cart endpoints, inventory reservation services, order creation from cart, payment provider abstractions with a fake provider, local payment persistence, idempotent payment webhook handling, successful-payment ledger entries, seller balance/payout read APIs, admin payout hold/release, manual seller fulfilment/tracking, return requests with seller response and payout holds, refund workflow with ledger reversals, dispute workflow with evidence/messages/admin resolution, support ticket workflow with private internal notes, seller ad campaign draft/submission API and Angular dashboard, seller analytics dashboard, admin ad campaign review, ad event tracking and seller campaign metrics, product moderation, AI suggestion persistence/DTOs, the backend AI listing assistant service abstraction, seller AI suggestion generation/apply endpoints, the Angular seller AI assistant UI, buyer AI shopping intent extraction/recommendations, buyer visual search with a fake vision provider, and private product embedding generation exist. Real payment provider integration, external payout processing, delivery-confirmation workflow, production vision AI, and full finance operations UI are intentionally not implemented yet.

## API Rules

- Keep endpoints thin.
- Validate ownership and roles at the API/application boundary.
- Do not expose provider secrets or internal ledger implementation details.
- Use idempotency for webhooks and other external event handlers.

## Rate Limiting

Swyftly uses ASP.NET Core fixed-window rate limiting with named policies configured under `SwyftlyRateLimits`.

Current policies:

- `Auth`: login and public registration.
- `Ai`: seller AI listing suggestion generation, stricter than normal browsing.
- `ProductWrite`: seller product creation.
- `Payment`: buyer payment initiation.
- `AdClick`: anonymous ad click tracking.
- `Search`: public product search.

When a policy is exceeded, the API returns HTTP `429` with a small ProblemDetails-style JSON response:

```json
{
  "title": "RateLimit.Exceeded",
  "status": 429,
  "detail": "Too many requests. Please wait before trying again."
}
```

Development settings keep limits relaxed enough for local work. Tests verify the `Search` policy returns `429` when its configured limit is exceeded.
