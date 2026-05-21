# Swyftly Database Schema

The database is PostgreSQL with EF Core migrations.

## Current State

The scaffold includes a `SwyftlyDbContext` and default schema name `swyftly`.

EF Core migrations live in `backend/src/Swyftly.Infrastructure/Persistence/Migrations` and are generated through the Infrastructure project with the API project as startup.

Current migrations create:

- ASP.NET Core Identity tables for users, roles, claims, logins, and tokens.
- `buyer_profiles`, linked one-to-one to Identity users.
- `buyer_wishlist_items`, linked to buyer profiles and products, enforcing one wishlist item per buyer/product.
- `seller_profiles`, linked one-to-one to Identity users, with seller profile fields and `PendingVerification` as the initial seller status.
- `seller_storefronts`, linked one-to-one to sellers, with a unique storefront slug.
- `seller_addresses`, linked one-to-one to sellers for onboarding and fulfilment location data.
- `seller_payout_profiles`, linked one-to-one to sellers, storing payout provider references/placeholders only, not sensitive bank data.
- `seller_verifications`, linked many-to-one to sellers for verification review history.
- `refresh_tokens`, linked to Identity users and storing hashed refresh tokens only, token-family ids for replay revocation, replacement hashes, and revocation reasons.
- `audit_logs`, currently used for admin seller approval/rejection/suspension actions, admin product approval/rejection/change-request actions, and admin review moderation actions.
- `categories`, with parent-child hierarchy, slug, display order, and active status.
- `category_attributes`, linked to categories, with data type, required flag, display order, active status, and JSONB allowed values for select-style attributes.
- `products`, linked to sellers and optionally categories/brands, with seller-scoped slug uniqueness, draft/review/publication statuses including `NeedsAdminReview` and `ChangesRequested`, rejection/change-request reason, publish timestamp, and JSONB tags.
- `product_variants`, linked to products, with SKU, size, colour, price, compare-at price, stock, reserved quantity, barcode, and Active/Inactive/OutOfStock status. Check constraints prevent negative reserved quantity and reserved quantity above stock.
- `product_images`, linked to products, with URL, storage key, alt text, sort order, primary flag, and created timestamp. Image binary data is not stored in PostgreSQL.
- `product_attribute_values`, linked to products, storing category-specific attribute values as JSONB.
- `product_reviews`, linked to buyers, sellers, products, orders, and order items, storing verified-buyer review ratings, optional title/body, moderation status, moderation reason/actor/timestamp metadata, and audit timestamps. A unique index enforces one review per order item.
- `ai_product_suggestions`, linked to seller/product drafts, storing AI listing suggestions, prompt/model metadata, quality score, status, JSONB suggested attributes/tags/missing fields/risk flags, and accepted/applied timestamps.
- `ai_suggestion_field_audits`, linked to AI suggestions, storing AI vs seller-final field values and accepted/edited flags.
- `ai_usage_logs`, recording AI feature usage metadata, token/cost estimates, latency, success/failure, and non-secret error details.
- `ai_prompt_versions`, storing prompt templates and version metadata for AI features. Provider API keys or secrets are not stored here.
- `ai_moderation_results`, linked to seller/product listings, storing business-rule moderation risk level, admin-review flag, reason, detected terms, missing fields, and flags as JSONB.
- `product_embeddings`, linked to products, storing AI semantic-search source text, model name, creation timestamp, and a PostgreSQL `vector(1536)` embedding.
- `carts`, linked to buyer profiles and optionally one seller, enforcing one active cart per buyer for the MVP.
- `cart_items`, linked to carts, products, and product variants, capturing quantity, unit price, product title, SKU, size, and colour at add time.
- `inventory_reservations`, linked to product variants, buyer profiles, and carts, storing checkout inventory holds with Active/Confirmed/Expired/Cancelled status and expiry timestamps. Reservation services use conditional database updates for PostgreSQL stock holds/releases.
- `orders`, linked to buyer profiles, seller profiles, and source carts, storing checkout order headers with PendingPayment/Paid/Processing/ReadyToShip/Shipped/Delivered/ReturnRequested/Refunded/Cancelled/Disputed/Completed statuses plus shipping, platform-fee, discount, and audit timestamps.
- `order_items`, linked to orders, products, and product variants, snapshotting product title, SKU, size, colour, unit price, and quantity at order creation time.
- `order_status_history`, linked to orders, recording every status transition including the initial PendingPayment creation entry.
- `shipments`, linked to orders, sellers, and buyers, storing manual fulfilment status, carrier/tracking details, shipped/delivered timestamps, and audit timestamps.
- `shipment_events`, linked to shipments, recording manual fulfilment and tracking events with status snapshots.
- `return_requests`, linked to orders, buyers, and sellers, storing return status, reason, seller response, buyer dispute metadata, and request/audit timestamps.
- `return_items`, linked to return requests and source order items, storing returned quantity, reason, opened/unsealed flag, and item notes.
- `return_messages`, linked to return requests, storing buyer/seller return workflow messages.
- `payments`, linked to orders and buyer profiles, storing local payment records, configured provider name/reference, retryable checkout URL, amount, currency, status, and paid/failed timestamps. A filtered unique index allows only one active non-failed/non-cancelled payment per order.
- `payment_events`, linked optionally to payments, storing sanitized provider webhook JSON, provider event id, event type, received/processed timestamps, raw-payload redaction timestamp, processing status, and error details. JSON webhooks and form-encoded PayFast ITNs are normalized into JSON before persistence, with signatures/tokens/secrets/card fields redacted. `(Provider, ProviderEventId)` is unique for webhook idempotency. The worker redacts retained raw webhook payload JSON after the configured retention window while preserving event metadata for reconciliation.
- `refunds`, linked to orders, payments, buyers, sellers, and optionally return requests, storing requested/approved/processing/refunded/failed/rejected status, amount, provider refund reference, reason, requester/approver actor details for dual control, audit timestamps, and a concurrency token for approval processing.
- `refund_events`, linked to refunds, recording refund request, approval, processing, completion, and failure events.
- `disputes`, linked to orders, buyers, sellers, and optionally return requests, storing dispute status, opening reason, resolution reason, and audit timestamps.
- `dispute_messages`, linked to disputes, storing buyer/seller dispute conversation messages.
- `dispute_evidence`, linked to disputes, storing text/file/image evidence references and descriptions. File binary data is not stored in PostgreSQL.
- `support_tickets`, linked optionally to buyer profiles, seller profiles, orders, products, sellers, and payments, storing support category, status, subject, description, assignment, and lifecycle timestamps.
- `support_messages`, linked to support tickets, storing buyer/seller/support/admin messages plus an `is_internal` flag for private support notes that are hidden from buyers and sellers.
- `notifications`, linked to Identity users as recipients, storing in-app notification type, title, message, related entity metadata, read timestamp, and creation timestamp.
- `ad_campaigns`, linked to sellers, storing seller ad campaign type, status, run dates, review lifecycle timestamps, and rejection reason.
- `ad_campaign_products`, linked to ad campaigns and promoted products.
- `ad_budgets`, linked one-to-one to ad campaigns, storing daily budget, total budget, max CPC, spent amount, and currency.
- `ad_impressions`, `ad_clicks`, `ad_conversions`, and `ad_charges`, storing ad event/reporting data captured by public tracking endpoints and backend paid-order attribution.
- `seller_ad_credits`, storing seller advertising credit balances per currency.
- `ledger_entries`, append-only internal ledger rows linked optionally to orders, order items, sellers, buyers, and payments, with type, amount, currency, direction, description, and creation timestamp.
- `seller_balances`, storing seller pending, available, and held balances per currency.
- `commission_rules`, storing commission and provider-fee rule metadata for future admin configuration. Current ledger processing uses configured default fee options.
- `seller_payouts`, linked to sellers, storing net payout amount, currency, Pending/OnHold/Available/Processing/PaidOut/Reversed/Failed status, hold/release/availability/processing/provider audit details, provider references/status, terminal timestamps, and a concurrency token.
- `seller_payout_items`, linked to seller payouts and source ledger entries, tying payout records back to order/payment ledger activity and tracking refund-adjusted amounts.
- `seller_payout_adjustments`, linked to refunds, seller payouts, payout items where applicable, and refund ledger entries, recording payout reductions or recovery-required signals without mutating paid-out provider records.

Public product search currently reads from PostgreSQL tables directly when no external/local search index data is available. Prompt 32 added a search indexing abstraction and local in-memory placeholder; it did not add a database table or external search dependency.

Prompt 33 adds pgvector support through the PostgreSQL `vector` extension. Local PostgreSQL must have pgvector installed before applying the `ProductEmbeddingsPgvector` migration; the repository Docker Compose file uses the `pgvector/pgvector:pg16` image for that purpose.

Orders start as `PendingPayment`. Payment initiation, provider webhook events, stored checkout URLs for retry, successful-payment ledger entries, paid-cart cleanup, seller pending-balance crediting, seller payout records, conditional inventory reservation holds, reservation confirmation on payment success, reservation release on payment failure, seller manual fulfilment actions, seller delivery confirmation, buyer shipment tracking, return requests, seller return responses, verified-buyer reviews, buyer review moderation, buyer wishlist items, in-app notifications, notification wiring for review/return/order/support/delivery workflows, refund records, provider refund abstraction calls, ledger reversal entries, seller-balance refund debits, payout item adjustments, fake-provider payout lifecycle transitions, standalone disputes, dispute messages/evidence, dispute-triggered payout holds, buyer-favoured dispute refund request creation, admin dispute resolution, support tickets with private internal notes, seller ad campaign draft/review-submission/admin-review persistence, ad event tracking, and paid-order ad attribution now exist. Real external payout provider integration, production payment reconciliation, and carrier integration are future work.

## Planned Modules

- Carrier integration and delivery exception handling.
- Real external payout provider integration and finance operations UI.
- Production provider reconciliation.
- External notification delivery channels, real-time notifications, and broader admin audit workflows.
- Production AI provider execution, buyer semantic search, and production search indexes.

## Rules

- Use relational modeling for financial, order, inventory, and payout data.
- Use JSONB only where flexible category attributes require it.
- Keep payment and ledger records append-oriented.
- Sellers cannot become `Verified` until required profile, storefront, address, and admin-approved payout placeholder data are complete.
- Do not store full sensitive bank data unless it is encrypted/tokenized; current payout storage is placeholder/provider-reference only.
- Add migrations through the Infrastructure project.
- Sensitive admin workflows must write `audit_logs` through the shared audit logging service.
