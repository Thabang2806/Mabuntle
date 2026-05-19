# Swyftly Architecture

Swyftly uses a modular monolith backend and an Angular SSR frontend.

## Backend

The backend follows Clean Architecture:

- `Swyftly.Domain`: entities, value objects, domain events, and business rules.
- `Swyftly.Application`: use cases, commands, queries, DTOs, validators, and interfaces.
- `Swyftly.Infrastructure`: EF Core persistence, provider adapters, storage, search, AI, email, and other integrations.
- `Swyftly.Api`: HTTP endpoints, auth wiring, middleware, and API response formatting.
- `Swyftly.Worker`: background processing host.

Feature work should use vertical slices inside the application layer where practical.

## Frontend

The Angular app uses SSR/hybrid rendering for future public ecommerce pages and normal client-side behavior for private dashboard areas.

Current primary frontend routes include:

- `/`
- `/shop`
- `/seller`
- `/admin`
- `/account`
- `/assistant`
- `/visual-search`
- `/cart`
- `/checkout`

## Infrastructure

PostgreSQL is the primary database. pgvector support is included for private product embeddings, using the local Docker `pgvector` image when Docker is available.

Current external-provider integrations remain local placeholders: fake AI providers, fake payment provider, local image storage, and local search indexing. Production provider selection, secret management, payout execution, and provider-grade webhook hardening remain future work.
