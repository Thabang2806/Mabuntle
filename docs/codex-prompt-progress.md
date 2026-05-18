# Codex Prompt Progress

Source prompt pack: `Documentation/swyftly_codex_development_prompts.md`

Last updated: 2026-05-18

## How To Use This Tracker

- Keep the source prompt pack unchanged.
- Update this file after every completed implementation task.
- Use `Done` only when the prompt acceptance criteria are met.
- Use `Partial` when foundation work exists but the prompt still has unmet acceptance criteria.
- Add short evidence: changed area, verification command, or known blocker.
- Keep ad hoc work in the separate section at the bottom.

## Current Snapshot

- Done: 7
- Partial: 0
- Not started: 62
- Current recommended next step: start Prompt 8, common backend building blocks.

## Prompt Checklist

| # | Prompt | Status | Evidence / next action |
|---|---|---|---|
| 1 | Create the initial monorepo | Done | Backend solution, Angular SSR app, root docs, Docker compose, `.env.example`, README, and project folders created. |
| 2 | Create AGENTS.md for Codex guidance | Done | `AGENTS.md` added with stack, architecture, security, payment, AI, and command guidance. |
| 3 | Add development environment and health checks | Done | API `/health`, CORS, logging, Angular shell routes, and frontend build verified. Docker runtime not verified because Docker is unavailable locally. |
| 4 | Write architecture.md | Done | `docs/architecture.md` added. |
| 5 | Write feature roadmap documentation | Done | `docs/feature-roadmap.md` added. |
| 6 | Write coding standards documentation | Done | `docs/coding-standards.md` added. |
| 7 | Configure PostgreSQL and EF Core | Done | EF tooling, design-time factory, initial schema migration, `/health/ready`, and opt-in PostgreSQL tests exist. Migration applied to local PostgreSQL and DB readiness/tests passed. |
| 8 | Add common backend building blocks | Not started | Add reusable result/error, domain events, current user abstraction, audit placeholder, and tests. |
| 9 | Add CI pipeline | Not started | Add build/test pipeline for backend and frontend. |
| 10 | Implement identity and roles | Not started | Implement buyer, seller, admin identity and authorization foundation. |
| 11 | Implement Angular auth screens and guards | Not started | Add frontend auth routes, forms, guards, and API integration. |
| 12 | Implement seller profile and verification domain | Not started | Add seller profile aggregate and verification statuses. |
| 13 | Seller onboarding API | Not started | Add seller onboarding commands, endpoints, validation, and tests. |
| 14 | Angular seller onboarding wizard | Not started | Add seller onboarding UI flow. |
| 15 | Admin seller approval | Not started | Add admin review and approval workflow. |
| 16 | Implement categories and category attributes | Not started | Add category model and attribute definitions. |
| 17 | Implement product aggregate and statuses | Not started | Add product aggregate and state transitions. |
| 18 | Implement product variants | Not started | Add variant model for size, colour, SKU, price, and inventory ties. |
| 19 | Implement product images | Not started | Add product image model and storage abstraction. |
| 20 | Seller product draft API | Not started | Add draft product endpoints and application slices. |
| 21 | Angular seller product form | Not started | Add product draft UI. |
| 22 | Define AI product suggestion schema and database tables | Not started | Add AI suggestion schema, persistence, and validation boundaries. |
| 23 | Implement AI Listing Assistant service abstraction | Not started | Add backend AI provider interface and placeholder adapter. |
| 24 | Implement AI product suggestion endpoint | Not started | Add endpoint and application command. |
| 25 | Apply AI suggestions to product draft | Not started | Add seller-reviewed suggestion application flow. |
| 26 | Angular AI Product Listing Assistant panel | Not started | Add seller UI panel for listing suggestions. |
| 27 | AI moderation rules for product listings | Not started | Add moderation policies and review flags. |
| 28 | Admin product review workflow | Not started | Add admin product approval workflow. |
| 29 | Admin audit logs | Not started | Add audit log model and recording for sensitive actions. |
| 30 | Implement basic product search API | Not started | Add search query endpoint and filters. |
| 31 | Angular shop and category pages | Not started | Build public browsing pages beyond current placeholders. |
| 32 | Add Typesense or Meilisearch integration placeholder | Not started | Add search provider abstraction and placeholder implementation. |
| 33 | Add product embeddings and pgvector foundation | Not started | Add embedding storage foundation and pgvector migration. |
| 34 | Implement single-seller cart | Not started | Add cart domain/application/API flow. |
| 35 | Implement inventory reservations | Not started | Add reservation rules and expiry path. |
| 36 | Create order aggregate from cart | Not started | Add order creation workflow. |
| 37 | Angular cart and checkout UI | Not started | Add buyer cart and checkout screens. |
| 38 | Payment provider abstraction | Not started | Add payment strategy interfaces and provider placeholders. |
| 39 | Implement payments and payment events | Not started | Add payment entities and event handling. |
| 40 | Payment webhook idempotency | Not started | Add idempotent webhook processing and signature validation. |
| 41 | Internal marketplace ledger | Not started | Add ledger model and transaction entries. |
| 42 | Seller balances and payout workflow | Not started | Add balance calculation and payout states. |
| 43 | Manual shipping and fulfilment | Not started | Add manual tracking and fulfilment statuses. |
| 44 | Return request workflow | Not started | Add return requests and statuses. |
| 45 | Refund workflow and ledger reversals | Not started | Add refund flow and ledger reversal entries. |
| 46 | Dispute workflow | Not started | Add dispute model and admin/support handling. |
| 47 | Admin dashboard foundation | Not started | Add admin shell and summary endpoints. |
| 48 | Support ticket system | Not started | Add support tickets and agent workflow. |
| 49 | Ad campaign domain model | Not started | Add campaign model and statuses. |
| 50 | Seller ad campaign API | Not started | Add seller campaign endpoints. |
| 51 | Admin ad campaign review | Not started | Add admin review for campaigns. |
| 52 | Track ad impressions, clicks, and conversions | Not started | Add tracking endpoints/events. |
| 53 | Seller ad campaign dashboard UI | Not started | Add seller advertising dashboard. |
| 54 | Seller analytics dashboard | Not started | Add seller metrics and UI. |
| 55 | Admin finance and marketplace reports | Not started | Add admin reporting endpoints/UI. |
| 56 | AI usage analytics | Not started | Add AI usage and cost reporting. |
| 57 | Add API authorization audit | Not started | Review and test authorization coverage. |
| 58 | Add rate limiting and abuse protection placeholders | Not started | Add rate-limit policies and abuse hooks. |
| 59 | Add webhook security and idempotency review | Not started | Review webhook security once webhooks exist. |
| 60 | Add observability foundation | Not started | Add tracing, correlation IDs, and structured observability. |
| 61 | Buyer AI shopping assistant intent extraction | Not started | Add buyer AI intent extraction. |
| 62 | Buyer AI shopping assistant product recommendations | Not started | Add recommendation flow using catalog data. |
| 63 | Visual search MVP | Not started | Add image-based search foundation. |
| 64 | Codex PR review prompt | Not started | Use when reviewing a PR. |
| 65 | Security review prompt | Not started | Use for focused security review. |
| 66 | Payment and ledger review prompt | Not started | Use after payment and ledger work exists. |
| 67 | Refactor prompt template | Not started | Use for scoped refactoring tasks. |
| 68 | Bug fix prompt template | Not started | Use for defects. |
| 69 | Documentation update prompt | Not started | Use whenever behavior or architecture changes need docs updates. |

## Ad Hoc Work Completed

| Date | Work | Evidence |
|---|---|---|
| 2026-05-18 | Added Swagger/OpenAPI to API project | `Swashbuckle.AspNetCore` added, `/swagger` enabled in Development, build/test passed, runtime Swagger JSON checked. |
| 2026-05-18 | Consolidated git ignores | Single root `.gitignore`, nested Angular `.gitignore` removed, `.vscode/`, `.env`, frontend build outputs, and Angular cache ignored. |
