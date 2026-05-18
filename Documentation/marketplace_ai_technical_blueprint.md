# Transactional Fashion & Beauty Marketplace Blueprint

**Project:** Transactional ecommerce marketplace for fashion, clothing, jewellery, accessories, beauty products, and related lifestyle categories  
**Preferred stack:** Angular, ASP.NET Core / .NET, PostgreSQL, AI-assisted seller and buyer features  
**Document type:** Product, technical, AI, and architecture planning document  
**Prepared:** 17 May 2026

---

## 1. Executive summary

The platform should be designed as a **transactional multi-vendor ecommerce marketplace**, not a classified ads platform. Sellers should be able to register, create storefronts, publish products, manage variants and stock, receive orders, fulfil deliveries, handle returns, and receive payouts. Buyers should be able to browse, search, add products to cart, pay online, track orders, request returns, and leave reviews.

Because the platform focuses on fashion, jewellery, accessories, and beauty products, the product catalog must support:

- Product variants, especially size, colour, stock, and price variations.
- Category-specific attributes.
- Product images and image quality rules.
- Beauty-specific compliance information such as ingredients, batch number, expiry date, volume, and warnings.
- Jewellery-specific information such as material, plating, stone type, ring size, chain length, and authenticity claims.
- Admin moderation for risky products, counterfeit-risk wording, unsupported beauty claims, and seller trust issues.

The recommended backend architecture is a **modular monolith** using **Clean Architecture principles**, **vertical slices for use cases**, **Domain-Driven Design for business-critical modules**, and **selective repository usage**. Avoid building generic repositories for every entity. Entity Framework Core already provides repository-like and unit-of-work-like behaviour through `DbSet` and `DbContext`. Use repositories only where they add real value, such as complex aggregate roots, domain-specific data access, or persistence isolation for payments, orders, payouts, and seller risk.

The first AI feature should be the **AI Fashion Product Listing Assistant**, which helps sellers generate better product listings faster. It should suggest titles, descriptions, categories, attributes, tags, image alt text, missing fields, compliance warnings, and listing quality scores. The seller must always review and confirm AI suggestions before publishing.

The recommended AI roadmap is:

1. AI Product Listing Assistant.
2. AI moderation and compliance checks.
3. AI-assisted search and product discovery.
4. Buyer AI shopping/style assistant.
5. Size/fit assistance, visual search, and personalization.
6. Seller analytics, pricing support, and advanced automation.

---

## 2. Platform positioning

A suitable product positioning statement would be:

> A multi-vendor fashion, jewellery, accessories, and beauty marketplace where independent sellers can create storefronts, publish products, sell through platform checkout, and use AI to improve product listings, product discovery, styling, and buyer trust.

The platform should feel premium, modern, safe, and transaction-ready. It should not simply allow sellers to post ads; it should support proper ecommerce workflows.

---

## 3. Main user types

### 3.1 Buyer

Buyers should be able to:

- Register and log in.
- Browse products.
- Search and filter products.
- View product details.
- View seller storefronts.
- Add products to wishlist.
- Add products to cart.
- Checkout and pay online.
- View order history.
- Track delivery.
- Request returns or refunds.
- Review products and sellers.
- Receive notifications.
- Use AI shopping and style assistance later.

### 3.2 Seller

Sellers should be able to:

- Register as a seller.
- Complete seller onboarding.
- Set up a seller storefront.
- Add products.
- Upload product images.
- Create variants by size, colour, stock, and price.
- Use the AI Product Listing Assistant.
- Manage inventory.
- View and fulfil orders.
- Upload tracking details.
- Respond to return requests.
- View seller balance.
- View payout history.
- Access seller analytics later.

### 3.3 Admin

Admins should be able to:

- Approve or reject sellers.
- Approve or reject products.
- Manage categories, brands, and attributes.
- Review AI moderation flags.
- View orders, payments, refunds, returns, and disputes.
- Manage seller payouts.
- Suspend sellers or buyers.
- Handle support tickets.
- View audit logs.
- Configure commission rules.
- Manage platform policies.

### 3.4 Support agent

Support agents should be able to:

- Search users, sellers, products, and orders.
- View payment, shipping, and refund status.
- Add internal notes.
- Respond to buyer and seller tickets.
- Escalate disputes to admins.

---

## 4. Recommended technology stack

### 4.1 Frontend

Recommended:

- Angular.
- TypeScript.
- Angular Router.
- Angular Reactive Forms.
- Angular SSR/hybrid rendering for public pages.
- Tailwind CSS.
- Angular Material or PrimeNG.

Use SSR or hybrid rendering for public ecommerce pages:

- Home page.
- Category pages.
- Product listing pages.
- Product detail pages.
- Seller storefront pages.
- Marketing pages.

Use normal client-side rendering for private dashboards:

- Buyer account.
- Seller dashboard.
- Admin dashboard.
- Checkout flow.
- Support dashboard.

### 4.2 Backend

Recommended:

- ASP.NET Core Web API.
- .NET 10 LTS where practical for a new long-term project.
- C#.
- Entity Framework Core.
- Npgsql PostgreSQL provider.
- ASP.NET Core Identity.
- JWT access tokens and refresh tokens.
- Role-based authorization.
- Modular monolith architecture.
- Clean Architecture principles.
- Vertical Slice Architecture for application use cases.
- DDD for complex marketplace modules.

### 4.3 Database

Recommended:

- PostgreSQL.
- EF Core migrations.
- pgvector for embeddings and semantic search.
- JSONB for flexible category attributes where appropriate.
- Strong relational modeling for financial, inventory, and order data.

PostgreSQL should store:

- Users.
- Sellers.
- Buyers.
- Products.
- Product variants.
- Inventory.
- Orders.
- Payments.
- Ledger entries.
- Refunds.
- Returns.
- Payouts.
- Reviews.
- AI suggestions.
- Moderation results.
- Product embeddings.

### 4.4 Search

Recommended options:

- Typesense as first choice for strong ecommerce search and filters.
- Meilisearch as a simpler search option.
- Algolia if budget allows and a managed premium search product is preferred.

Search should support:

- Keyword search.
- Faceted filters.
- Category filters.
- Price filters.
- Size filters.
- Colour filters.
- Brand filters.
- Material filters.
- Rating filters.
- Availability filters.
- Sort by newest, price, popularity, and rating.

### 4.5 AI

Recommended:

- OpenAI API.
- OpenAI .NET SDK.
- Responses API.
- Structured Outputs.
- Vision-capable model for product image analysis.
- Moderation API.
- Embeddings.
- pgvector for vector storage.

AI should be called from the .NET backend, not from Angular directly.

### 4.6 Background jobs

Recommended:

- Hangfire for MVP.
- ASP.NET Core Worker Service later for dedicated workers.

Use background jobs for:

- Payment webhook processing.
- AI product suggestions.
- AI moderation.
- Product embedding generation.
- Search indexing.
- Email notifications.
- Seller payout release.
- Delivery tracking updates.
- Abandoned cart cleanup.
- Expired inventory reservation cleanup.

### 4.7 File and image storage

Recommended options:

- Cloudinary for product images and transformations.
- Azure Blob Storage for Microsoft/Azure-aligned infrastructure.
- AWS S3 for AWS-aligned infrastructure.

For fashion and beauty, Cloudinary is attractive because image transformation, compression, resizing, thumbnails, and CDN delivery are useful.

### 4.8 Payments

Recommended options:

- Paystack for South Africa/Africa-focused launches.
- Stripe Connect where directly supported.
- Other local providers depending on launch market.

The platform must maintain its own internal ledger. Do not rely only on the payment provider dashboard.

### 4.9 Shipping

MVP:

- Manual seller fulfilment.
- Seller uploads tracking number.
- Buyer receives shipping updates.
- Admin can monitor delivery issues.

Later:

- Courier rate calculation.
- Courier booking.
- Waybill generation.
- Tracking webhooks.
- Return shipping labels.

### 4.10 Monitoring and observability

Recommended:

- Serilog.
- OpenTelemetry.
- Application Insights, Grafana, or another monitoring platform.
- Sentry for frontend/backend errors.
- Structured logging.
- Correlation IDs.
- Audit logs for admin and finance actions.

---

## 5. Recommended .NET architecture and design pattern

### 5.1 Recommended pattern summary

For this project, the recommended architecture is:

> **Modular Monolith + Clean Architecture + Vertical Slice Architecture + Domain-Driven Design for complex modules + selective repositories + explicit domain events/outbox for side effects.**

This approach is more suitable than a simple repository-only architecture because the platform has many business workflows:

- Seller onboarding.
- Product approval.
- Product variants and inventory.
- Checkout.
- Payment webhooks.
- Order fulfilment.
- Returns.
- Refunds.
- Seller balances.
- Seller payouts.
- Disputes.
- AI suggestions.
- Moderation.

A generic CRUD repository approach will likely become too thin, repetitive, and difficult to use for complex business behaviour.

---

## 6. Repository pattern recommendation

### 6.1 Should the project use the Repository pattern?

Yes, but **selectively**.

Do not create a generic repository for every entity such as:

```csharp
IRepository<Product>
IRepository<Order>
IRepository<User>
IRepository<Category>
IRepository<Cart>
```

This often creates unnecessary abstraction and can make EF Core harder to use, especially for queries, projections, includes, pagination, filtering, and performance tuning.

Entity Framework Core already gives you:

- `DbContext` as a unit of work.
- `DbSet<TEntity>` as a repository-like abstraction.
- LINQ for querying.
- Change tracking.
- Transactions.
- Migrations.

For simple data access, use EF Core directly inside application handlers or query services.

### 6.2 Where repositories are useful

Use specific repositories for complex aggregate roots or business-critical persistence logic.

Good candidates:

```txt
IOrderRepository
IPaymentRepository
ISellerPayoutRepository
IProductRepository
IReturnRepository
IDisputeRepository
ISellerRiskRepository
```

These repositories should be business-focused, not generic CRUD wrappers.

Example:

```csharp
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct);
    Task<Order?> GetWithItemsAndPaymentAsync(OrderId id, CancellationToken ct);
    Task AddAsync(Order order, CancellationToken ct);
}
```

Avoid this style as the main pattern:

```csharp
public interface IRepository<T>
{
    Task<T?> GetByIdAsync(Guid id);
    Task<List<T>> GetAllAsync();
    Task AddAsync(T entity);
    void Update(T entity);
    void Delete(T entity);
}
```

A generic repository like this rarely captures marketplace-specific behaviour and often hides useful EF Core features.

### 6.3 Practical rule

Use this rule:

```txt
For simple reads and CRUD: use DbContext/query services directly.
For complex domain aggregates: use specific repositories.
For cross-aggregate workflows: use application services/command handlers.
For external provider calls: use adapter interfaces.
```

---

## 7. Unit of Work recommendation

Do not create a heavy custom Unit of Work unless there is a clear reason.

EF Core `DbContext.SaveChangesAsync()` already acts as the unit of work for a request or command. You can still expose a thin abstraction if you want the application layer to avoid depending directly on the concrete infrastructure context.

Example:

```csharp
public interface IApplicationDbContext
{
    DbSet<Product> Products { get; }
    DbSet<Order> Orders { get; }
    DbSet<Payment> Payments { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
```

For sensitive workflows such as checkout, payment webhooks, refunds, and payouts, use explicit database transactions.

Example transaction-sensitive flows:

- Confirming payment.
- Reducing inventory.
- Creating order ledger entries.
- Reversing ledger entries after refund.
- Releasing seller payout.
- Cancelling expired unpaid order.

---

## 8. Clean Architecture structure

A suitable solution structure:

```txt
Marketplace.Api
Marketplace.Application
Marketplace.Domain
Marketplace.Infrastructure
Marketplace.Worker
Marketplace.Tests
```

### 8.1 Domain layer

Contains business entities, value objects, aggregates, domain events, and business rules.

Examples:

```txt
Product
ProductVariant
Order
OrderItem
Payment
LedgerEntry
SellerProfile
SellerBalance
SellerPayout
ReturnRequest
Dispute
```

The domain layer should not depend on EF Core, OpenAI, Paystack, Stripe, Cloudinary, or any external service.

### 8.2 Application layer

Contains use cases, commands, queries, validators, DTOs, and interfaces.

Examples:

```txt
CreateProductCommand
GenerateAiProductListingSuggestionCommand
ApproveProductCommand
CreateCheckoutSessionCommand
HandlePaymentWebhookCommand
RequestReturnCommand
ReleaseSellerPayoutCommand
```

### 8.3 Infrastructure layer

Contains integrations and implementations:

```txt
PostgreSQL EF Core DbContext
Paystack payment adapter
Stripe payment adapter
OpenAI AI adapter
Cloudinary image adapter
Email provider
Search indexing service
Hangfire job implementation
```

### 8.4 API layer

Contains controllers/endpoints, authentication, authorization, request mapping, and API response formatting.

### 8.5 Worker layer

Runs background jobs:

```txt
ProcessOutboxMessages
GenerateProductEmbeddings
RunAiModeration
SyncSearchIndex
ReleaseEligibleSellerPayouts
ExpireInventoryReservations
SendNotifications
```

---

## 9. Modular monolith structure

Instead of starting with microservices, use a modular monolith. This means one deployable backend, but separated into clear modules.

Recommended modules:

```txt
Auth
Buyers
Sellers
Catalog
Inventory
Cart
Checkout
Orders
Payments
Ledger
Shipping
Returns
Refunds
Payouts
Reviews
AI
Search
Notifications
Admin
Support
```

Each module should own its application logic. Keep boundaries clear even though everything is deployed together.

Example folder structure:

```txt
src/
  Marketplace.Api/
    Controllers/
    Middleware/
    Auth/

  Marketplace.Application/
    Catalog/
      Commands/
      Queries/
      Validators/
      DTOs/
    Orders/
      Commands/
      Queries/
      Validators/
      DTOs/
    Payments/
    AI/
    Admin/

  Marketplace.Domain/
    Catalog/
      Product.cs
      ProductVariant.cs
      ProductStatus.cs
    Orders/
      Order.cs
      OrderItem.cs
      OrderStatus.cs
    Payments/
    Ledger/
    Sellers/
    Returns/

  Marketplace.Infrastructure/
    Persistence/
      MarketplaceDbContext.cs
      Configurations/
      Migrations/
    Payments/
      PaystackPaymentProvider.cs
      StripePaymentProvider.cs
    AI/
      OpenAiListingAssistant.cs
      OpenAiModerationService.cs
    Storage/
      CloudinaryImageStorage.cs
    Search/
      TypesenseProductIndexer.cs

  Marketplace.Worker/
    Jobs/
```

---

## 10. Vertical Slice Architecture

Vertical Slice Architecture organizes code by feature/use case rather than only by technical layer.

Instead of grouping everything only as:

```txt
Controllers
Services
Repositories
DTOs
```

Use feature folders:

```txt
Catalog/CreateProduct
Catalog/UpdateProduct
Catalog/PublishProduct
Orders/CreateOrder
Orders/CancelOrder
Payments/HandleWebhook
AI/GenerateProductListingSuggestion
Returns/RequestReturn
```

Example slice:

```txt
Application/Catalog/CreateProduct/
  CreateProductCommand.cs
  CreateProductValidator.cs
  CreateProductHandler.cs
  CreateProductResponse.cs
```

Benefits:

- Easier to understand one use case at a time.
- Easier to test.
- Less giant service classes.
- Better alignment with marketplace workflows.

---

## 11. CQRS recommendation

Use a pragmatic CQRS style.

Separate commands from queries:

### Commands change state

Examples:

```txt
CreateProductCommand
SubmitProductForReviewCommand
ApproveSellerCommand
ReserveInventoryCommand
CreateCheckoutCommand
HandlePaymentWebhookCommand
RequestReturnCommand
ApproveRefundCommand
ReleaseSellerPayoutCommand
```

### Queries read data

Examples:

```txt
GetProductDetailsQuery
SearchProductsQuery
GetSellerOrdersQuery
GetAdminDashboardQuery
GetSellerBalanceQuery
GetPayoutHistoryQuery
```

This does not mean you need separate databases at the beginning. Use the same PostgreSQL database initially. Later, if needed, search reads can use Typesense and product recommendations can use pgvector.

---

## 12. Domain-Driven Design recommendation

Use DDD where the business rules are complex. Do not over-engineer simple CRUD screens.

Strong DDD candidates:

```txt
Orders
Payments
Ledger
Seller payouts
Returns/refunds
Inventory reservation
Seller risk
Product moderation
```

Lighter CRUD candidates:

```txt
Static content pages
Simple category management
Basic admin lookup screens
Basic notification templates
```

### 12.1 Aggregates

Suggested aggregate roots:

```txt
Seller
Product
Order
Payment
ReturnRequest
SellerPayout
Dispute
```

### 12.2 Value objects

Suggested value objects:

```txt
Money
Address
Email
PhoneNumber
Sku
ProductVariantOption
CommissionRate
TrackingNumber
BankAccountReference
```

### 12.3 Domain events

Suggested domain events:

```txt
SellerRegistered
SellerApproved
ProductSubmittedForReview
ProductPublished
InventoryReserved
OrderCreated
PaymentConfirmed
OrderPaid
OrderShipped
OrderDelivered
ReturnRequested
RefundApproved
SellerBalanceCredited
SellerPayoutReleased
AiSuggestionGenerated
ProductFlaggedForReview
```

Domain events help separate business side effects from core state changes.

---

## 13. Outbox pattern

The platform should use the Outbox pattern for important side effects.

Why this matters:

- A payment webhook may update an order, create ledger entries, send notifications, and enqueue seller emails.
- If the database update succeeds but the notification fails, you need a reliable retry mechanism.
- If the payment provider sends duplicate webhooks, you need idempotency.

### 13.1 Outbox table

Suggested table:

```txt
outbox_messages
- id
- occurred_on
- type
- payload_json
- status
- retry_count
- processed_on
- error
```

### 13.2 Example flow

```txt
Payment webhook received
        ↓
Validate webhook signature
        ↓
Start database transaction
        ↓
Mark payment as confirmed
        ↓
Create ledger entries
        ↓
Mark order as paid
        ↓
Create outbox messages: OrderPaid, NotifySeller, SyncSearchIndex
        ↓
Commit transaction
        ↓
Background worker processes outbox messages
```

---

## 14. State machine pattern

Use explicit state transitions for core workflows.

Important state machines:

```txt
Product status
Order status
Payment status
Return status
Refund status
Seller verification status
Seller payout status
Moderation status
```

Example order statuses:

```txt
PendingPayment
Paid
Processing
ReadyToShip
Shipped
Delivered
ReturnRequested
Refunded
Cancelled
Disputed
Completed
```

Do not allow random status updates from anywhere in the code. Use domain methods or application services such as:

```csharp
order.MarkAsPaid(paymentId);
order.MarkAsShipped(trackingNumber);
order.MarkAsDelivered(deliveredAt);
order.RequestReturn(reason);
order.Cancel(reason);
```

This prevents invalid transitions such as moving directly from `PendingPayment` to `Delivered`.

---

## 15. Strategy and adapter patterns

Use Strategy and Adapter patterns for external services.

### 15.1 Payment provider strategy

```csharp
public interface IPaymentProvider
{
    Task<CreatePaymentResult> CreatePaymentAsync(CreatePaymentRequest request, CancellationToken ct);
    Task<VerifiedWebhookResult> VerifyWebhookAsync(HttpRequest request, CancellationToken ct);
    Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken ct);
}
```

Implementations:

```txt
PaystackPaymentProvider
StripePaymentProvider
```

### 15.2 Shipping provider strategy

```txt
ManualShippingProvider
BobGoShippingProvider
CourierGuyShippingProvider
```

### 15.3 AI provider strategy

```txt
OpenAiListingAssistant
OpenAiModerationService
OpenAiEmbeddingService
```

### 15.4 Storage provider strategy

```txt
CloudinaryImageStorage
AzureBlobImageStorage
S3ImageStorage
```

Benefits:

- Easier provider switching.
- Cleaner tests.
- Less vendor-specific code in business logic.

---

## 16. Specification pattern

The Specification pattern can be useful for reusable product filters and seller/order queries.

Examples:

```txt
PublishedProductsSpecification
ProductsByCategorySpecification
ProductsInPriceRangeSpecification
ProductsAvailableInSizeSpecification
OrdersReadyForPayoutSpecification
HighRiskSellerSpecification
```

Use it carefully. Do not introduce a complex specification library unless it solves a real problem. For simple queries, LINQ projections are enough.

---

## 17. Policy/rules engine pattern

The platform will need many business rules.

Examples:

```txt
Beauty product + opened = admin review required.
Luxury brand + no proof = admin review required.
New seller + high-value order = payout hold.
Seller with high refund rate = risk review.
Product title contains replica = block or admin review.
Beauty product mentions cures acne = admin review.
```

Start with simple C# rules. Later, if rules become complex and frequently changed by non-developers, consider a more formal rules engine.

Suggested structure:

```txt
IMarketplacePolicyEvaluator
IProductModerationPolicy
IPayoutHoldPolicy
ISellerRiskPolicy
IReturnEligibilityPolicy
```

---

## 18. Recommended backend request flow

Example: Seller creates a product with AI assistance.

```txt
Angular seller form
        ↓
POST /api/seller/products/drafts
        ↓
Create product draft
        ↓
Seller uploads images
        ↓
POST /api/seller/products/{id}/ai-suggestion
        ↓
AI Listing Assistant generates structured suggestion
        ↓
Backend validates suggestion
        ↓
Suggestion saved in ai_product_suggestions
        ↓
Seller accepts/edits fields
        ↓
Product saved as draft
        ↓
Seller submits for review
        ↓
Moderation + admin review
        ↓
Product published
        ↓
Search indexing + embeddings generated
```

Example: Buyer checkout.

```txt
Buyer starts checkout
        ↓
Validate cart
        ↓
Reserve inventory
        ↓
Create pending order
        ↓
Create payment session
        ↓
Buyer pays
        ↓
Payment webhook received
        ↓
Verify payment
        ↓
Mark order paid
        ↓
Create ledger entries
        ↓
Notify seller
        ↓
Seller fulfils order
        ↓
Delivery confirmed
        ↓
Seller balance becomes available after policy checks
        ↓
Payout released
```

---

## 19. Phase 0: Product definition and operating rules

### 19.1 Goal

Define how the marketplace operates before building too much code.

### 19.2 Deliverables

- Product requirements document.
- User roles and permissions.
- Category structure.
- Product attribute rules.
- Commission model.
- Return/refund policy.
- Seller payout policy.
- Seller verification policy.
- Prohibited products policy.
- Beauty product policy.
- Branded/luxury goods policy.
- Data privacy checklist.
- Admin workflow map.

### 19.3 Decisions to make

- Will sellers be individuals, businesses, or both?
- Will you allow used items or only new products?
- Will beauty products be new/sealed only?
- Will luxury branded products be allowed?
- What proof is required for branded/luxury goods?
- Who handles shipping?
- Who pays return shipping?
- When does seller payout happen?
- What is your platform commission?
- Will prices be VAT-inclusive?
- What is the return window?
- Can sellers reject returns?
- What happens when buyer and seller disagree?
- Will sellers need approval before publishing products?
- Will high-risk categories require admin approval?

---

## 20. Phase 1: Core transactional marketplace MVP

### 20.1 Goal

Build the core marketplace without depending on AI.

### 20.2 Buyer features

- Register/login.
- Browse products.
- Search products.
- Filter products.
- View product details.
- View seller storefront.
- Add to wishlist.
- Add to cart.
- Checkout.
- Pay online.
- View order history.
- Track order status.
- Request return/refund.
- Leave review.

### 20.3 Seller features

- Seller registration.
- Seller profile.
- Seller storefront.
- Seller verification state.
- Product creation.
- Product variants.
- Product images.
- Inventory management.
- Order dashboard.
- Manual fulfilment.
- Tracking upload.
- Seller balance.
- Payout history.
- Return response.

### 20.4 Admin features

- Seller approval.
- Product approval.
- Category management.
- Brand management.
- Order overview.
- Payment overview.
- Refund overview.
- Dispute overview.
- Manual moderation.
- Basic reporting.

### 20.5 Technical features

- Angular frontend.
- ASP.NET Core API.
- PostgreSQL.
- EF Core.
- Auth and roles.
- Product image storage.
- Payment provider integration.
- Payment webhooks.
- Internal ledger.
- Email notifications.
- Admin dashboard.

### 20.6 MVP acceptance criteria

Phase 1 is complete when:

- A seller can register and create products with variants.
- A buyer can browse, add to cart, pay, and receive an order confirmation.
- The system records payment webhooks correctly.
- Inventory reduces after successful payment.
- Admin can review sellers, products, orders, payments, and refunds.
- Seller balances are calculated internally.

---

## 21. Phase 2: AI Fashion Product Listing Assistant

### 21.1 Goal

Help sellers create high-quality product listings faster and create cleaner catalog data for the marketplace.

### 21.2 Features

- Generate product title.
- Generate short description.
- Generate full description.
- Suggest category.
- Suggest category-specific attributes.
- Suggest style tags.
- Suggest occasion tags.
- Suggest SEO keywords.
- Generate image alt text.
- Suggest missing fields.
- Score listing quality.
- Flag risky claims.

### 21.3 Seller flow

```txt
Seller creates product draft
        ↓
Seller uploads images
        ↓
Seller enters short notes
        ↓
Seller clicks Generate with AI
        ↓
AI returns structured suggestions
        ↓
Seller accepts or edits suggestions
        ↓
Backend validates output
        ↓
Product remains draft or moves to review
```

### 21.4 Example seller input

```json
{
  "sellerNotes": "Black satin dress, sizes S to L, for parties and evening events.",
  "condition": "new",
  "knownAttributes": {
    "colour": "Black",
    "material": "Satin",
    "sizes": ["S", "M", "L"]
  },
  "imageIds": ["img_001", "img_002"]
}
```

### 21.5 Example AI output

```json
{
  "recommendedTitle": "Black Satin Evening Dress",
  "titleSuggestions": [
    "Black Satin Evening Dress",
    "Black Satin Party Dress",
    "Black Satin Midi Dress for Evening Wear"
  ],
  "shortDescription": "A sleek black satin dress for parties, evening events, and formal occasions.",
  "fullDescription": "This black satin dress offers a polished look for evening events, parties, and formal occasions. Available in sizes S to L. Please confirm exact measurements and care instructions before publishing.",
  "suggestedCategory": {
    "categoryPath": "Women > Clothing > Dresses > Evening Dresses",
    "confidence": 0.91
  },
  "attributes": {
    "colour": "Black",
    "material": "Satin",
    "occasion": ["Party", "Evening", "Formal"],
    "sizes": ["S", "M", "L"]
  },
  "tags": [
    "black dress",
    "satin dress",
    "evening dress",
    "party wear"
  ],
  "missingFields": [
    "Exact measurements",
    "Care instructions",
    "Stock per size",
    "Model height",
    "Model wearing size"
  ],
  "riskFlags": [],
  "qualityScore": 76
}
```

### 21.6 Backend validation rules

The backend should validate that:

- Seller owns the product draft.
- Image IDs belong to the seller.
- Suggested category exists.
- Suggested attributes are allowed for that category.
- Title length is acceptable.
- Description does not include prohibited claims.
- Beauty claims are not unsupported.
- Luxury claims are not unsupported.
- AI did not invent brand, material, authenticity, ingredients, or expiry date.

### 21.7 Suggested AI tables

```txt
ai_product_suggestions
ai_suggestion_field_audits
ai_usage_logs
ai_prompt_versions
```

### 21.8 AI success metrics

Track:

- AI assistant usage rate.
- Suggestion acceptance rate.
- Suggestion edit rate.
- Average listing quality score improvement.
- Average time saved per listing.
- Moderation failure rate after AI assistance.
- Cost per AI suggestion.
- AI latency.
- AI failure rate.

---

## 22. Phase 3: Trust, safety, and moderation

### 22.1 Goal

Reduce unsafe products, fraud, misleading claims, counterfeit risk, and low-quality listings.

### 22.2 Features

- Text moderation.
- Image moderation.
- Counterfeit-risk keyword detection.
- Beauty claim detection.
- Seller risk scoring.
- Buyer report product.
- Buyer report seller.
- Admin review queue.
- Auto-block high-risk listings.
- Manual review for uncertain listings.

### 22.3 Moderation statuses

```txt
Approved
Flagged
Rejected
NeedsAdminReview
Blocked
```

### 22.4 Counterfeit-risk terms

Examples:

```txt
Replica
AAA copy
Mirror quality
Designer inspired
Gucci style
Rolex style
Dupe
```

### 22.5 Beauty-risk terms

Examples:

```txt
Cures acne
Removes scars permanently
Guaranteed skin whitening
Medical-grade treatment
Clinically proven
Permanent results
```

### 22.6 Admin review queue should show

- Product.
- Seller.
- Risk reason.
- AI summary.
- Detected phrases.
- Images.
- Seller history.
- Recommended action.
- Approve/reject/escalate buttons.

---

## 23. Phase 4: Search, filters, and product discovery

### 23.1 Goal

Make it easy for buyers to find relevant products.

### 23.2 Search features

- Keyword search.
- Category browsing.
- Faceted filters.
- Sort by price.
- Sort by newest.
- Sort by popularity.
- Sort by rating.
- Filter by size.
- Filter by colour.
- Filter by brand.
- Filter by material.
- Filter by seller.
- Filter by availability.

### 23.3 Product discovery features

- Similar products.
- More from this seller.
- Recently viewed.
- Trending now.
- New arrivals.
- Best sellers.
- Recommended categories.
- Complete the look.
- Customers also viewed.

### 23.4 Search acceptance criteria

Phase 4 is complete when:

- A buyer can search naturally and find relevant products.
- Filters work accurately by category.
- Out-of-stock products are handled correctly.
- Search results update quickly after product changes.
- Admin can manage synonyms and featured categories.

---

## 24. Phase 5: Payments, refunds, and payout hardening

### 24.1 Goal

Make financial operations reliable, auditable, and safe.

### 24.2 Features

- Payment reconciliation dashboard.
- Webhook retry handling.
- Failed payment handling.
- Partial refunds.
- Full refunds.
- Seller payout holds.
- Seller payout release rules.
- Manual payout adjustments.
- Payment provider fee tracking.
- Platform commission reports.
- Finance export.

### 24.3 Internal ledger

The platform should maintain a ledger for all marketplace money movement.

Example:

```txt
Buyer pays: R1,000
Platform commission: R120
Payment provider fee: R30
Seller balance: R850
Payout status: Pending
```

### 24.4 Suggested ledger entry types

```txt
BuyerPaymentReceived
PaymentProviderFeeRecorded
PlatformCommissionRecorded
SellerBalanceCredited
SellerBalanceHeld
SellerPayoutReleased
RefundIssued
RefundReversal
ManualAdjustment
```

### 24.5 Suggested payout statuses

```txt
Pending
OnHold
Available
Processing
PaidOut
Reversed
Failed
```

### 24.6 Suggested payout rules

- Release seller balance 3 to 7 days after delivery.
- Hold payout if return requested.
- Hold payout if seller is under review.
- Hold payout for high-risk orders.
- Hold payout for first few seller orders.

---

## 25. Phase 6: Shipping, fulfilment, and returns automation

### 25.1 Goal

Move from manual fulfilment to operational efficiency.

### 25.2 MVP shipping

- Seller fulfils manually.
- Seller uploads tracking number.
- Buyer receives shipping updates.
- Admin can view shipping status.

### 25.3 Advanced shipping

- Courier rate calculation.
- Courier booking.
- Waybill generation.
- Pickup scheduling.
- Tracking webhooks.
- Return shipping labels.
- Delivery confirmation.
- Lost parcel handling.

### 25.4 Fulfilment statuses

```txt
AwaitingFulfilment
Packed
ReadyForCourier
Collected
InTransit
Delivered
DeliveryFailed
ReturnedToSender
```

### 25.5 Return automation

```txt
Buyer starts return
        ↓
Seller approves or admin reviews
        ↓
Return label generated
        ↓
Buyer ships item back
        ↓
Seller confirms receipt
        ↓
Refund processed
        ↓
Ledger adjusted
```

---

## 26. Phase 7: Buyer AI shopping/style assistant

### 26.1 Goal

Create a buyer-facing AI experience that recommends real marketplace products.

### 26.2 Example buyer prompts

```txt
I need an outfit for a wedding under R1,500.
Find gold earrings for sensitive ears.
Show me a black dress in size medium.
I need skincare for oily skin under R300.
Find a birthday gift for someone who likes rose gold jewellery.
```

### 26.3 Correct architecture

```txt
Buyer asks AI
        ↓
AI extracts intent
        ↓
Backend searches real products
        ↓
Backend returns product candidates
        ↓
AI explains and groups the real results
        ↓
Frontend shows product cards
```

The AI must never invent products, prices, sellers, delivery times, or stock. It should only explain products returned by the backend.

### 26.4 Features

- Natural-language product search.
- Outfit builder.
- Gift finder.
- Beauty product finder.
- Budget-based suggestions.
- Occasion-based suggestions.
- Saved AI searches.

---

## 27. Phase 8: Personalization, fit assistance, and visual search

### 27.1 Goal

Make the marketplace feel tailored to each buyer.

### 27.2 Features

- Recommended for you.
- Recently viewed.
- Wishlist-based recommendations.
- Size preference storage.
- Fit feedback.
- Similar by image.
- Visual search.
- Complete-the-look recommendations.
- Beauty shade/skin-type matching.

### 27.3 Fit assistant

Start simple:

- Seller provides size chart.
- Buyer selects usual size.
- Buyer optionally adds measurements.
- System recommends likely size.
- Buyer leaves fit feedback after purchase.

Example:

```txt
This item runs small. Based on seller measurements, you may prefer Large instead of Medium.
```

Do not start with complex AI fit prediction. It becomes better only once the platform has returns, reviews, and buyer fit feedback.

### 27.4 Visual search

Buyer uploads an image:

```txt
Find something similar to this handbag.
```

System flow:

```txt
AI describes uploaded image
        ↓
System extracts category, colour, shape, and style
        ↓
Search finds matching products
        ↓
Embedding similarity ranks results
        ↓
Buyer sees product cards
```

---

## 28. Phase 9: Growth and monetization

### 28.1 Goal

Increase revenue and engagement.

### 28.2 Marketplace monetization options

- Commission per sale.
- Featured product placement.
- Promoted storefronts.
- Seller subscription plans.
- Premium analytics for sellers.
- Express payout fee.
- Campaign fees.
- Brand partnership campaigns.

### 28.3 Promotion features

- Coupons.
- Seller discounts.
- Platform discounts.
- Flash sales.
- New-arrival campaigns.
- Free shipping threshold.
- Bundle discounts.
- Category sales.

### 28.4 Seller growth tools

- Sales dashboard.
- Product performance.
- Conversion rate.
- Low-stock alerts.
- Pricing suggestions.
- Popular search terms.
- Abandoned cart insights.
- Return rate by product.
- AI improvement suggestions.

### 28.5 Buyer engagement

- Wishlist reminders.
- Back-in-stock notifications.
- Price-drop alerts.
- New arrivals from followed sellers.
- Order update notifications.
- Personalized recommendations.

---

## 29. Phase 10: Security, compliance, and reliability

### 29.1 Security features

- Role-based access control.
- Admin two-factor authentication.
- Seller two-factor authentication for payout changes.
- JWT refresh token rotation.
- Rate limiting.
- Audit logs.
- Input validation.
- File upload scanning.
- Secure image URLs.
- Webhook signature verification.
- Secrets management.
- Database backups.
- Disaster recovery plan.

### 29.2 Privacy and data protection

The platform should have:

- Privacy policy.
- Cookie policy.
- Data retention rules.
- Marketing consent.
- User data export.
- User deletion/anonymisation process.
- Breach response process.
- Access controls for personal information.

### 29.3 Accessibility

Design accessibility early:

- Sufficient colour contrast.
- Keyboard navigation.
- Accessible forms.
- Error messages linked to fields.
- Alt text for images.
- Focus states.
- Screen-reader-friendly components.

### 29.4 Tax/VAT readiness

Confirm with an accountant or legal advisor. The platform should be designed to support:

- VAT-inclusive pricing if applicable.
- Seller invoices.
- Platform commission invoices.
- Buyer receipts.
- Finance exports.
- Seller tax reports.

---

## 30. Suggested database modules

### 30.1 Identity and users

```txt
users
roles
user_roles
buyer_profiles
seller_profiles
seller_verifications
seller_bank_accounts
seller_storefronts
user_addresses
```

### 30.2 Catalog

```txt
categories
category_attributes
brands
products
product_variants
product_images
product_attributes
inventory_movements
inventory_reservations
```

### 30.3 Commerce

```txt
carts
cart_items
orders
order_items
order_status_history
payments
payment_events
ledger_entries
commissions
seller_balances
seller_payouts
```

### 30.4 Shipping and returns

```txt
shipments
shipment_events
returns
return_items
refunds
refund_events
disputes
dispute_messages
```

### 30.5 Engagement

```txt
wishlists
reviews
product_questions
notifications
seller_followers
```

### 30.6 AI

```txt
ai_product_suggestions
ai_suggestion_field_audits
ai_moderation_results
ai_usage_logs
ai_prompt_versions
product_embeddings
search_logs
```

### 30.7 Admin and support

```txt
admin_actions
audit_logs
support_tickets
support_messages
reports
policy_versions
outbox_messages
```

---

## 31. Fashion and beauty category attributes

### 31.1 Clothing

Recommended attributes:

- Size.
- Colour.
- Material.
- Fit.
- Gender.
- Occasion.
- Sleeve length.
- Neckline.
- Pattern.
- Care instructions.
- Measurements.
- Model height.
- Model wearing size.

### 31.2 Shoes

Recommended attributes:

- Shoe size.
- UK/EU/US size system.
- Colour.
- Material.
- Heel height.
- Closure type.
- Condition.

### 31.3 Jewellery

Recommended attributes:

- Material.
- Metal type.
- Stone type.
- Plating.
- Length.
- Ring size.
- Earring size.
- Hypoallergenic status.
- Certificate/authenticity document.

### 31.4 Accessories

Recommended attributes:

- Type.
- Colour.
- Material.
- Dimensions.
- Brand.
- Style.
- Closure type.
- Compartments.

### 31.5 Beauty products

Recommended attributes:

- Brand.
- Product type.
- Shade.
- Skin type.
- Hair type.
- Ingredients.
- Expiry date.
- Batch number.
- Sealed/unsealed status.
- Volume/weight.
- Cruelty-free/vegan claims.
- Warnings.
- Usage instructions.

Beauty products should have stricter review rules than clothing because unsupported health, medical, or cosmetic claims can create legal and trust risks.

---

## 32. AI governance and cost controls

AI should have operational controls from the beginning.

Add:

- Prompt versioning.
- AI usage logs.
- Cost estimates.
- Rate limits per seller.
- AI output audit logs.
- Seller acceptance tracking.
- Moderation override logs.
- Admin review of risky AI outputs.
- Fallback when AI fails.

AI failure cases to handle:

- AI returns invalid JSON.
- AI suggests wrong category.
- AI invents unsupported material.
- AI suggests prohibited wording.
- AI service is unavailable.
- AI response is too slow.
- Seller abuses AI generation repeatedly.

Seller UI disclaimer:

```txt
AI suggestions are drafts. Please review and confirm all product details before publishing.
```

---

## 33. Suggested colour palette

Recommended palette: **Luxe Blush**.

```txt
Primary: #3A1D32
Primary Hover: #2A1425
Accent: #B76E79
Soft Accent: #F3D9D6
Background: #FFF9F4
Surface: #FFFFFF
Surface Warm: #F4EDE7
Border: #E8D6C7
Text: #1F1A1C
Muted Text: #6F5E66
Success: #0F766E
Warning: #B45309
Error: #B42318
```

Usage:

- Deep Plum for brand, header, primary buttons, and active navigation.
- Warm Ivory for page backgrounds.
- White for cards, modals, and forms.
- Rose Gold for fashion/beauty accents.
- Emerald for trusted transactional states such as successful payment and verified seller.
- Amber for pending review or warnings.
- Red for failed payments, rejected products, and dispute states.

---

## 34. Platform KPIs

### 34.1 Marketplace KPIs

- Gross merchandise value.
- Number of orders.
- Average order value.
- Conversion rate.
- Refund rate.
- Return rate.
- Dispute rate.
- Seller activation rate.
- Buyer repeat purchase rate.
- Product approval rate.
- Payment failure rate.

### 34.2 Seller KPIs

- Products created.
- Products published.
- Sales per seller.
- Average fulfilment time.
- Refund rate by seller.
- Late shipment rate.
- Seller rating.
- Payout frequency.

### 34.3 Buyer KPIs

- Search-to-product-view rate.
- Product-view-to-cart rate.
- Cart-to-checkout rate.
- Checkout success rate.
- Wishlist usage.
- Repeat purchases.
- Review rate.

### 34.4 AI KPIs

- AI assistant usage.
- Suggestion acceptance rate.
- Average listing quality improvement.
- Moderation flag rate.
- Cost per AI action.
- AI latency.
- AI failure rate.

---

## 35. Recommended MVP feature list

### 35.1 Include in MVP

- Buyer registration/login.
- Seller registration/login.
- Seller verification status.
- Seller storefront.
- Product creation.
- Product variants.
- Product images.
- Inventory management.
- Category/attribute system.
- Search and filters.
- Product detail page.
- Wishlist.
- Single-seller cart.
- Checkout.
- Payment provider integration.
- Payment webhooks.
- Internal ledger.
- Orders.
- Seller order dashboard.
- Manual shipping/tracking.
- Basic returns/refunds.
- Admin dashboard.
- Product approval.
- Seller approval.
- Basic reports.
- Email notifications.
- AI Product Listing Assistant.
- Basic AI moderation.

### 35.2 Exclude from MVP

- Multi-seller checkout.
- Complex seller subscriptions.
- Virtual try-on.
- Advanced size prediction.
- Fully automated counterfeit detection.
- Advanced AI pricing.
- Full personalization engine.
- Complex loyalty programme.
- Native mobile app.

---

## 36. Recommended build order

A practical build sequence:

```txt
1. Finalise operating rules and policies.
2. Design database schema.
3. Build auth, roles, buyer/seller/admin foundations.
4. Build seller onboarding and storefronts.
5. Build catalog, products, variants, images, and inventory.
6. Build cart and single-seller checkout.
7. Integrate payments and webhooks.
8. Build internal ledger and seller balances.
9. Build order fulfilment and manual shipping.
10. Build admin approval and support workflows.
11. Add AI Fashion Product Listing Assistant.
12. Add AI moderation and risk flags.
13. Add search engine and filters.
14. Add returns/refunds/disputes.
15. Add analytics and seller dashboards.
16. Add buyer AI assistant, personalization, and visual search.
```

---

## 37. Final architecture recommendation

Use this architecture:

```txt
Angular frontend
        ↓
ASP.NET Core API
        ↓
Application layer with commands/queries
        ↓
Domain layer for business rules
        ↓
Infrastructure layer for EF Core, payments, AI, storage, search, email
        ↓
PostgreSQL + pgvector
        ↓
Hangfire/Worker services for background jobs
```

Use these patterns:

```txt
Modular Monolith
Clean Architecture
Vertical Slice Architecture
DDD for complex business areas
Pragmatic CQRS
Selective Repository pattern
DbContext as Unit of Work
Domain Events
Outbox pattern
State Machine pattern for workflows
Strategy/Adapter pattern for providers
Policy/rules pattern for marketplace decisions
```

Avoid:

```txt
Starting with microservices too early
Generic repositories for every entity
Putting business logic in controllers
Calling AI directly from Angular
Letting AI publish products without seller review
Relying only on payment provider dashboards for seller balances
Allowing arbitrary status updates without state-transition rules
```

This gives the project a strong foundation that can start manageable and grow into a robust transactional ecommerce marketplace.

---

## 38. Reference links

These references are useful for validating the technical direction:

- Microsoft: Architect modern web applications with ASP.NET Core and Azure: https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/
- Microsoft: Common web application architectures: https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures
- Microsoft: DDD and CQRS patterns for complex .NET systems: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/
- Microsoft: DDD-oriented microservice design: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/ddd-oriented-microservice
- Microsoft: Infrastructure persistence layer with EF Core: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-implementation-entity-framework-core
- Microsoft: EF Core overview: https://learn.microsoft.com/en-us/ef/core/
- Microsoft: Domain events design and implementation: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation
- PostgreSQL pgvector: https://github.com/pgvector/pgvector
- OpenAI Structured Outputs: https://platform.openai.com/docs/guides/structured-outputs
- OpenAI Embeddings: https://platform.openai.com/docs/guides/embeddings
- OpenAI Moderation: https://platform.openai.com/docs/guides/moderation

