# MVP Monetization and Seller Advertising Strategy

**Project:** Transactional Fashion, Jewellery, Accessories and Beauty Marketplace  
**Stack context:** Angular frontend, ASP.NET Core/.NET backend, PostgreSQL, OpenAI AI features, Typesense/Meilisearch search  
**Document date:** 17 May 2026

---

## 1. Executive Summary

The recommended MVP monetization model is:

```txt
Free seller registration
Free storefront creation
Free product listings
No monthly seller subscription at MVP launch
Platform earns a transaction fee when a successful purchase is made
Seller advertising campaigns are introduced later, once buyer traffic exists
```

This is a strong approach for a new marketplace because the first challenge is not maximizing revenue immediately. The first challenge is building marketplace liquidity:

```txt
Enough sellers
Enough products
Enough buyers
Enough completed transactions
Enough trust
```

Charging sellers too early can slow onboarding. A transaction-based fee aligns the platform's success with the seller's success. Advertising can become a strong revenue stream later, but it should not be the main revenue assumption at launch because sellers will only pay for campaigns once they can see real traffic, impressions, clicks and sales.

The recommended sequence is:

```txt
Phase 1: Free seller onboarding + transaction fee
Phase 2: Seller tools + AI listing assistant + seller analytics
Phase 3: Free or invite-only promotional placements to test ad demand
Phase 4: Paid featured products and promoted listings
Phase 5: Advanced ad campaigns, seller subscriptions and premium growth tools
```

---

## 2. Business Model Recommendation

### 2.1 MVP revenue model

At MVP launch, use a simple and seller-friendly model:

| Item | MVP recommendation |
|---|---|
| Seller registration | Free |
| Seller storefront | Free |
| Product listings | Free |
| AI Product Listing Assistant | Free with reasonable usage limits |
| Monthly seller subscription | Not at MVP launch |
| Platform revenue | Transaction fee on successful purchases |
| Paid seller campaigns | Not at launch, or invite-only beta |
| Featured products | Manual/free testing first |

### 2.2 Seller-facing message

Use simple messaging:

```txt
Start selling for free.
Create your store for free.
List your products for free.
Only pay when you make a sale.
```

This is attractive for small boutiques, independent fashion sellers, jewellery makers, beauty resellers and creators because it reduces their risk of joining a new marketplace.

### 2.3 Recommended fee positioning

There are two main ways to present fees to sellers.

#### Option A: Single combined marketplace fee

Example:

```txt
Marketplace fee: 12% per successful sale
```

Internally, the platform splits this into:

```txt
Platform commission
Payment processing cost
Operational margin
```

This is easier for sellers to understand.

#### Option B: Itemized platform fee and payment fee

Example:

```txt
Platform fee: 10%
Payment processing fee: charged separately
```

This is more transparent, but it can feel more complex to sellers.

### 2.4 Recommended option

For MVP, use a **single combined marketplace fee** in the seller experience, but maintain a detailed internal ledger that separates:

```txt
Gross order amount
Payment provider fee
Platform commission
Shipping amount
Refund adjustments
Seller payable amount
Payout status
```

This gives sellers simplicity while giving the platform financial control.

---

## 3. Example Transaction Fee Model

This is an illustrative model only. Final pricing should be tested against payment provider fees, VAT/tax, refunds, disputes, support costs and seller expectations.

### 3.1 Example order

```txt
Product price: R1,000
Buyer shipping fee: R70
Total buyer payment: R1,070
Marketplace fee: 12% of product price
Payment provider fee: depends on provider and country
```

### 3.2 Example internal breakdown

```txt
Buyer pays: R1,070
Product amount: R1,000
Shipping amount: R70
Marketplace fee: R120
Estimated payment fee: provider-dependent
Seller pending balance: product amount - marketplace fee - applicable payment/shipping adjustments
```

### 3.3 Why the internal ledger matters

Do not rely only on the payment provider dashboard. The marketplace must maintain its own ledger so that every transaction can be traced.

The ledger should answer:

```txt
How much did the buyer pay?
How much did the payment provider charge?
How much commission did the platform earn?
How much is owed to the seller?
Is the seller balance pending, available, on hold or paid out?
Was there a refund, partial refund, chargeback or dispute?
```

---

## 4. Payment Provider Notes

### 4.1 Paystack

Paystack is worth evaluating if the marketplace is South Africa/Africa-focused.

Relevant Paystack concepts:

```txt
Payment collection
Subaccounts
Split payments
Multi-split payments
Settlement and payout flows
```

Paystack documentation states that split payments can share settlement for a transaction with another account. Paystack also supports subaccounts, which can be used to split payments between a main account and a subaccount. Multi-split payments support splitting settlement across the payout account and one or more subaccounts.

If using Paystack, verify current country support, fee schedule, settlement timing and split-payment suitability before committing the marketplace payout architecture.

### 4.2 Stripe Connect

Stripe Connect is strong for marketplace payments in supported countries.

Relevant Stripe Connect concepts:

```txt
Connected accounts
Application fees
Separate charges and transfers
Destination charges
Payouts
Disputes
Refunds
Chargebacks
```

Stripe's marketplace documentation explains that platforms can earn revenue by keeping part of the transaction amount or charging application fees. The exact payment flow depends on the countries involved, seller onboarding needs and legal responsibility for fees, refunds and disputes.

### 4.3 Recommended payment architecture

Regardless of the payment provider, use this structure:

```txt
Payment provider handles payment collection.
Marketplace backend records every payment event.
Marketplace ledger calculates seller balances.
Seller payouts are released based on platform rules.
Refunds and disputes reverse or hold ledger entries.
```

---

## 5. Why Ads Should Not Be the First Revenue Dependency

Seller ad campaigns are a good future revenue stream, but they should not be the MVP's primary revenue assumption.

### 5.1 Ads need traffic

Sellers will only trust paid campaigns if the platform can show:

```txt
Impressions
Clicks
Product views
Orders
Revenue generated
Return on ad spend
```

If sellers pay for campaigns before the marketplace has real buyer activity, they may get poor results and lose confidence.

### 5.2 Launch ads carefully

A safer approach:

```txt
Launch with transaction fees only.
Manually feature selected sellers/products for free.
Measure buyer engagement.
Introduce seller ad credits.
Test paid featured placements.
Then launch self-service campaigns.
```

---

## 6. Seller Advertising Roadmap

### Phase A: Manual promotions at launch

At launch, the admin team manually promotes selected sellers or products.

Examples:

```txt
Homepage featured products
New seller spotlight
Beauty picks of the week
Jewellery spotlight
New arrivals
Weekend sale picks
```

Pricing:

```txt
Free during MVP
Invite-only
Used to test demand and conversion
```

Purpose:

```txt
Learn which placements drive clicks.
Learn which product categories convert.
Build data before charging sellers.
```

---

### Phase B: Free seller ad credits

After early traction, give selected sellers promotional credits.

Examples:

```txt
R100 launch ad credit
R250 founding seller credit
Free 7-day featured product boost
```

This helps you test campaign flows without forcing sellers to spend their own money before the ad product is proven.

---

### Phase C: Paid featured placements

This is the simplest paid ad product.

Seller chooses a product or storefront and pays a fixed amount for a fixed time period.

Examples:

```txt
Feature this product for 7 days: R99
Feature this store for the weekend: R199
Appear in Jewellery Spotlight for 7 days: R149
Appear in Beauty Picks for 7 days: R149
```

Placement options:

```txt
Homepage featured row
Category featured row
New arrivals spotlight
Sale section
Seller spotlight
Beauty spotlight
Jewellery spotlight
```

Advantages:

```txt
Simple to understand
Simple to build
No click-billing complexity
Good for early monetization
```

Limitations:

```txt
Less performance-based
Needs fair allocation of limited slots
Requires clear campaign reporting
```

---

### Phase D: Promoted listings

This is a more advanced ad product.

Seller chooses:

```txt
Products to promote
Daily budget
Total budget
Start date
End date
Target category
Target keywords or search terms
```

Pricing options:

```txt
Cost-per-click (CPC)
Cost-per-impression (CPM)
Cost-per-acquisition/sale (CPA)
Fixed budget boost
```

Recommended first paid performance model:

```txt
Cost-per-click with daily budget limits
```

Why:

```txt
Sellers understand paying for clicks.
The marketplace can control spend using daily budgets.
Campaigns can stop automatically once the budget is reached.
```

However, CPC requires more fraud controls than fixed placement ads.

---

### Phase E: Off-platform campaigns

This should come much later.

The platform could help sellers run ads on:

```txt
Google
Meta
Instagram
TikTok
Pinterest
```

Possible pricing:

```txt
Ad spend + management fee
Campaign package fee
Percentage of attributed sales
```

This is powerful but requires attribution, campaign approval, budget controls, seller reporting and more operational work.

---

## 7. Ad Campaign Product Types

### 7.1 Featured product

```txt
Seller pays a fixed fee to show one product in a featured section.
```

Good for:

```txt
Early MVP ad testing
New arrivals
Seasonal fashion
Beauty launches
Jewellery collections
```

---

### 7.2 Sponsored search result

```txt
Product appears in search results with a Sponsored label.
```

Good for:

```txt
Search-driven buyers
Competitive categories
Popular fashion keywords
```

Rule:

```txt
Sponsored products must still be relevant to the search.
```

A seller should not be able to promote lipstick for a search like "black dress".

---

### 7.3 Sponsored category placement

```txt
Product appears in a sponsored slot on a category page.
```

Examples:

```txt
Women > Dresses
Jewellery > Earrings
Beauty > Skincare
Accessories > Bags
```

---

### 7.4 Featured storefront

```txt
Seller's store appears in a featured seller section.
```

Good for:

```txt
Boutiques
Beauty brands
Jewellery makers
High-performing sellers
```

---

### 7.5 Campaign bundles

Later, create packages.

Example:

```txt
Weekend Fashion Boost
- 3 featured products
- Homepage rotation
- Category spotlight
- Seller storefront highlight
```

---

## 8. Ad Eligibility Rules

Not every seller or product should be allowed to run ads.

### 8.1 Seller eligibility

Seller must be:

```txt
Verified
Active
Not suspended
Not under serious dispute review
Compliant with seller terms
Within acceptable refund/dispute rate limits
```

### 8.2 Product eligibility

Product must be:

```txt
Published
In stock
Not under moderation review
Not rejected or archived
Not flagged as counterfeit-risk
Not flagged for risky beauty claims
Have acceptable image quality
Have a minimum product quality score
```

### 8.3 Suggested minimum product quality score

```txt
Minimum quality score for ads: 75/100
```

The product should include:

```txt
Good images
Clear title
Accurate description
Category attributes
Stock/variants
Price
Shipping details
Return rules
```

---

## 9. Ad Ranking Formula

Ads should not be ranked only by who pays the most.

Recommended formula:

```txt
Ad Rank = Bid x Search Relevance x Product Quality Score x Seller Trust Score
```

Where:

```txt
Bid = amount seller is willing to pay
Search Relevance = match between product and buyer search/category
Product Quality Score = completeness and quality of listing
Seller Trust Score = seller rating, fulfilment history, dispute rate, verification
```

This protects buyer experience and prevents poor products from dominating the marketplace.

---

## 10. Buyer Trust Rules for Ads

All ads must be clear and honest.

Rules:

```txt
Sponsored products must be labelled as Sponsored.
Sponsored products must be relevant to the page/search.
Out-of-stock products cannot be promoted.
Flagged products cannot be promoted.
Products with risky claims cannot be promoted until reviewed.
Ads should not hide organic results completely.
```

This is important because paid placements can damage trust if buyers feel search results are manipulated.

---

## 11. Seller Campaign Dashboard

When ads are introduced, sellers need transparent reporting.

### 11.1 Campaign list

Show:

```txt
Campaign name
Campaign type
Status
Start date
End date
Budget
Spend
Impressions
Clicks
Orders
Revenue
Return on ad spend
```

### 11.2 Campaign detail page

Show:

```txt
Promoted products
Placement types
Daily spend
Click-through rate
Conversion rate
Orders generated
Revenue generated
Cost per click
Cost per order
Budget remaining
```

### 11.3 Campaign statuses

```txt
Draft
Scheduled
Active
Paused
Completed
Cancelled
BudgetExhausted
Rejected
UnderReview
```

---

## 12. Admin Campaign Controls

Admin should be able to:

```txt
Approve/reject campaigns
Pause campaigns
Refund ad credits
Create manual promotions
Create ad placement slots
Set pricing rules
Set minimum quality score
View campaign performance
View seller ad spend
Detect suspicious clicks
Blacklist products from ads
Disable ads for risky sellers
```

Admin should also see:

```txt
Top spending sellers
Ad revenue
Campaign conversion rate
Sponsored product complaints
Click fraud signals
Ad placement performance
```

---

## 13. Technical Implementation: .NET Modules

Add monetization and advertising as separate backend modules inside the modular monolith.

```txt
Modules.Monetization
Modules.Ledger
Modules.Advertising
Modules.Campaigns
Modules.Payments
Modules.Sellers
Modules.Catalog
Modules.Admin
```

### 13.1 Recommended services

```txt
TransactionFeeService
CommissionCalculator
LedgerService
SellerBalanceService
PayoutEligibilityService
AdCampaignService
AdEligibilityService
AdBudgetService
AdBillingService
AdPlacementService
AdTrackingService
AdReportingService
ClickFraudDetectionService
```

### 13.2 Background jobs

Use Hangfire or .NET Worker Services for:

```txt
Closing expired campaigns
Resetting daily budgets
Aggregating ad metrics
Charging campaign spend
Releasing ad credits
Reconciliation reports
Detecting suspicious clicks
Updating campaign statuses
Sending seller campaign reports
```

---

## 14. Database Design

### 14.1 Transaction revenue tables

```txt
payments
payment_events
ledger_entries
commissions
seller_balances
seller_payouts
refunds
refund_events
orders
order_items
```

### 14.2 Ledger entry examples

```txt
BuyerPaymentReceived
PaymentProviderFeeRecorded
PlatformCommissionRecorded
SellerBalanceCredited
SellerBalanceHeld
SellerPayoutReleased
RefundIssued
RefundReversed
ManualAdjustment
```

### 14.3 Advertising tables

```txt
ad_campaigns
ad_campaign_products
ad_placements
ad_budgets
ad_impressions
ad_clicks
ad_conversions
ad_charges
ad_invoices
seller_ad_credits
ad_credit_transactions
ad_policy_reviews
```

### 14.4 Suggested `ad_campaigns` fields

```txt
id
seller_id
name
campaign_type
status
pricing_model
start_date
end_date
daily_budget
total_budget
spent_amount
currency
target_category_id
target_keywords_json
created_at
updated_at
approved_at
approved_by_admin_id
rejection_reason
```

### 14.5 Suggested `ad_campaign_products` fields

```txt
id
campaign_id
product_id
status
bid_amount
quality_score_at_start
created_at
```

### 14.6 Suggested `ad_impressions` fields

```txt
id
campaign_id
product_id
buyer_id_nullable
session_id
placement_key
search_query_nullable
category_id_nullable
shown_at
```

### 14.7 Suggested `ad_clicks` fields

```txt
id
campaign_id
product_id
buyer_id_nullable
session_id
ip_hash
user_agent_hash
placement_key
clicked_at
charge_amount
is_chargeable
non_chargeable_reason
```

### 14.8 Suggested `ad_conversions` fields

```txt
id
campaign_id
product_id
order_id
order_item_id
buyer_id
conversion_value
attribution_model
attribution_window_days
converted_at
```

---

## 15. Ad Wallet vs Direct Campaign Billing

There are two main billing models for ads.

### 15.1 Direct campaign billing

Seller pays for each campaign at setup.

Good for:

```txt
Fixed featured placements
Simple boost packages
```

Example:

```txt
Seller pays R99 upfront to feature a product for 7 days.
```

### 15.2 Seller ad wallet / ad credits

Seller loads money into an ad wallet or receives platform-issued ad credits.

Good for:

```txt
CPC campaigns
Daily budget campaigns
Free launch credits
Promotional incentives
```

Example:

```txt
Seller tops up R500 ad wallet.
Campaign spends up to R50 per day.
Clicks deduct from available ad balance.
```

### 15.3 Recommendation

Use both, in this order:

```txt
MVP ad testing: free ad credits
First paid ads: fixed featured placements paid upfront
Later performance ads: ad wallet + CPC budgets
```

---

## 16. API Endpoint Ideas

### 16.1 Seller campaign endpoints

```txt
GET    /api/seller/ad-campaigns
POST   /api/seller/ad-campaigns
GET    /api/seller/ad-campaigns/{id}
PUT    /api/seller/ad-campaigns/{id}
POST   /api/seller/ad-campaigns/{id}/submit
POST   /api/seller/ad-campaigns/{id}/pause
POST   /api/seller/ad-campaigns/{id}/resume
GET    /api/seller/ad-campaigns/{id}/metrics
GET    /api/seller/ad-credits
POST   /api/seller/ad-credits/top-up
```

### 16.2 Admin campaign endpoints

```txt
GET    /api/admin/ad-campaigns/pending-review
POST   /api/admin/ad-campaigns/{id}/approve
POST   /api/admin/ad-campaigns/{id}/reject
POST   /api/admin/ad-campaigns/{id}/pause
GET    /api/admin/ad-reports/revenue
GET    /api/admin/ad-reports/performance
POST   /api/admin/ad-placements
PUT    /api/admin/ad-placements/{id}
```

### 16.3 Tracking endpoints

```txt
POST   /api/ads/impression
POST   /api/ads/click
POST   /api/ads/conversion
```

Tracking endpoints should include fraud protection, rate limiting and deduplication.

---

## 17. Campaign Metrics and KPIs

### 17.1 Marketplace monetization KPIs

```txt
Gross merchandise value
Marketplace fee revenue
Payment processing costs
Net marketplace revenue
Refund rate
Dispute rate
Seller payout amount
Seller payout delays
Average order value
```

### 17.2 Advertising KPIs

```txt
Ad revenue
Active campaigns
Seller ad adoption rate
Average campaign budget
Impressions
Clicks
Click-through rate
Conversion rate
Orders attributed to ads
Revenue attributed to ads
Return on ad spend
Cost per click
Cost per order
```

### 17.3 Seller trust KPIs

```txt
Ad satisfaction
Repeat ad usage
Campaign refund requests
Seller complaints
Sponsored product report rate
Ad performance by product quality score
```

---

## 18. Launch and Growth Phases

### Phase 1: MVP launch

Build:

```txt
Free seller onboarding
Free storefronts
Free listings
Transaction fee model
Payment integration
Internal ledger
Seller balances
Basic payout flow
Admin finance view
Manual product/store promotions
```

Do not build:

```txt
Full self-service ads
CPC billing
Ad wallet
Off-platform campaigns
Seller subscriptions
```

Goal:

```txt
Build liquidity and trust.
```

---

### Phase 2: Seller growth tools

Build:

```txt
AI Product Listing Assistant
Listing quality score
Seller analytics
Product performance dashboard
Basic sales insights
Manual promotion reports
```

Goal:

```txt
Help sellers improve listings and increase sales.
```

---

### Phase 3: Ad beta

Build:

```txt
Ad campaign tables
Ad placements
Admin-created campaigns
Free seller ad credits
Featured product beta
Basic impression/click tracking
Basic seller campaign reporting
```

Goal:

```txt
Test whether promoted placements drive real buyer engagement.
```

---

### Phase 4: Paid featured placements

Build:

```txt
Seller-created fixed-fee campaigns
Campaign approval workflow
Payment or ad credit usage
Featured product slots
Seller metrics dashboard
Campaign status management
```

Goal:

```txt
Start monetizing visibility without complex CPC billing.
```

---

### Phase 5: Promoted listings and ad wallet

Build:

```txt
Seller ad wallet
Daily budgets
CPC pricing
Sponsored search results
Sponsored category placements
Click fraud protection
Attribution logic
ROAS reporting
```

Goal:

```txt
Launch performance-based seller advertising.
```

---

### Phase 6: Advanced monetization

Build:

```txt
Seller subscription tiers
Premium analytics
Campaign bundles
Off-platform managed campaigns
Featured storefronts
Category sponsorships
AI campaign recommendations
```

Goal:

```txt
Diversify marketplace revenue beyond transaction fees.
```

---

## 19. Risks and Mitigations

| Risk | Why it matters | Mitigation |
|---|---|---|
| Sellers do not join | No supply means no marketplace | Keep onboarding and listings free |
| Sellers do not get sales | Sellers churn | Focus on traffic, product quality and seller tools |
| Ads launch too early | Sellers lose trust if campaigns underperform | Start with free/manual promotions first |
| Fees feel too high | Sellers may avoid the platform | Use a clear combined fee and show value |
| Refunds reduce margins | Fashion/beauty can have returns | Track refunds and payout holds in ledger |
| Click fraud | CPC ads can be abused | Deduplicate clicks and monitor suspicious patterns |
| Poor products dominate ads | Buyer trust declines | Use ad eligibility and quality score requirements |
| Beauty/luxury risk | Counterfeit and claim issues | Block flagged products from ads until reviewed |
| Payment reconciliation errors | Financial risk | Maintain detailed ledger and webhook logs |

---

## 20. MVP User Stories

### 20.1 Seller stories

```txt
As a seller, I want to create a store for free so that I can start selling without upfront cost.
As a seller, I want to list products for free so that I can test the platform before committing money.
As a seller, I want to see my marketplace fee clearly before publishing products.
As a seller, I want to see how much I earned from each order after fees.
As a seller, I want to see my pending and available balances.
As a seller, I want to receive payout status updates.
As a seller, I want to use AI to improve my listings so that buyers can find my products.
```

### 20.2 Admin stories

```txt
As an admin, I want to configure the marketplace fee percentage.
As an admin, I want to view platform commission by order.
As an admin, I want to reconcile payments with orders.
As an admin, I want to review seller balances before payout.
As an admin, I want to manually feature products during MVP.
As an admin, I want to see which featured products drive clicks and orders.
```

### 20.3 Future seller ad stories

```txt
As a seller, I want to promote a product so that it gets more visibility.
As a seller, I want to set a campaign budget so that I control my spend.
As a seller, I want to see impressions, clicks and orders from my campaign.
As a seller, I want to pause a campaign if it is not performing.
As a seller, I want to use ad credits before spending my own money.
```

---

## 21. Open Decisions

These decisions should be finalized before implementation:

```txt
What exact transaction fee will the platform charge?
Will the fee be a single combined fee or itemized fee?
Will the transaction fee apply to product price only or product + shipping?
Will sellers pay payment processing fees separately?
When does seller payout become available?
How long is the payout hold after delivery?
How are refunds and partial refunds handled?
Will early sellers get reduced fees?
Will founding sellers get free ad credits?
When will paid ads launch?
Will ads be fixed-fee first, CPC first or both?
What product quality score is required for ads?
Which categories require stricter ad approval?
```

---

## 22. Recommended Final Positioning

Use this as the business-model statement:

> The marketplace will be free for sellers to join, create a storefront and list products. The platform will earn a transaction-based marketplace fee only when a seller makes a successful sale. Once the platform has enough buyer traffic and product activity, sellers will be able to purchase optional advertising campaigns such as promoted listings, featured products and sponsored storefront placements.

This model is strong because it:

```txt
Reduces seller onboarding friction
Aligns platform revenue with seller success
Creates a path to recurring ad revenue
Supports marketplace liquidity first
Keeps paid campaigns optional
Allows the platform to monetize traffic later
```

---

## 23. References Checked

The following sources were used as implementation references and should be rechecked before production launch because pricing, fees and provider capabilities can change:

1. Paystack South Africa pricing: https://paystack.com/za/pricing
2. Paystack Split Payments documentation: https://paystack.com/docs/payments/split-payments/
3. Paystack Subaccounts API documentation: https://paystack.com/docs/api/subaccount/
4. Paystack Multi-split Payments documentation: https://paystack.com/docs/payments/multi-split-payments/
5. Paystack Transaction Split API: https://paystack.com/docs/api/split/
6. Stripe Connect marketplace documentation: https://docs.stripe.com/connect/marketplace
7. Stripe Connect application fees documentation: https://docs.stripe.com/connect/marketplace/tasks/app-fees
8. Etsy Ads campaign setup reference: https://help.etsy.com/hc/en-us/articles/360033701174-How-to-Set-Up-and-Manage-an-Etsy-Ads-Campaign
9. Etsy Ads performance/cost factors reference: https://help.etsy.com/hc/en-us/articles/360034223613-How-to-Review-the-Performance-of-Your-Etsy-Ads

---

## 24. Next Recommended Deliverables

After this document, the next useful documents would be:

```txt
1. Marketplace fee and ledger specification
2. Seller payout rules specification
3. Advertising campaign feature specification
4. Seller dashboard wireframe specification
5. Admin finance and campaign dashboard specification
6. Database schema for orders, ledger, payouts and ads
7. API contract for monetization and campaign endpoints
```

