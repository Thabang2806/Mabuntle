# Swyftly Database Schema

The database is PostgreSQL with EF Core migrations.

## Current State

The scaffold includes a `SwyftlyDbContext`, default schema name `swyftly`, and an initial migration that creates the schema only. No business tables are defined yet.

EF Core migrations live in `backend/src/Swyftly.Infrastructure/Persistence/Migrations` and are generated through the Infrastructure project with the API project as startup.

## Planned Modules

- Identity and users.
- Buyers and sellers.
- Catalog, products, variants, images, and inventory.
- Cart, checkout, orders, payments, ledger, and payouts.
- Shipping, returns, refunds, and disputes.
- Reviews, wishlist, notifications, support, and admin audit logs.
- AI suggestions, moderation, embeddings, and search indexes.

## Rules

- Use relational modeling for financial, order, inventory, and payout data.
- Use JSONB only where flexible category attributes require it.
- Keep payment and ledger records append-oriented.
- Add migrations through the Infrastructure project.
