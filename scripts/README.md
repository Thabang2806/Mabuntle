# Swyftly Scripts

## Development user seed

Use `seed-dev-users.ps1` to create local test users in the configured PostgreSQL database. The script uses ASP.NET Identity and EF Core, so password hashes, roles, and seller/buyer profile records are created through the same mappings as the API.

Run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\seed-dev-users.ps1 -Password "UseYourOwnDevPassword1!"
```

If the database has not been migrated yet:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\seed-dev-users.ps1 -Password "UseYourOwnDevPassword1!" -ApplyMigrations
```

To reset passwords for already-created seed users:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\seed-dev-users.ps1 -Password "UseYourOwnDevPassword1!" -ResetPasswords
```

To seed the buyer-flow demo catalog as well:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\seed-dev-users.ps1 -Password "UseYourOwnDevPassword1!" -ApplyMigrations -SeedSampleProducts
```

To seed the seller approval flow demo records as well:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\seed-dev-users.ps1 -Password "UseYourOwnDevPassword1!" -ApplyMigrations -SeedSampleProducts -SeedSellerFlowDemo
```

The script uses `ConnectionStrings__DefaultConnection` when set, otherwise it falls back to `backend/src/Swyftly.Api/appsettings.Development.json`.

Seeded accounts:

| Email | Roles and setup |
| --- | --- |
| `admin@swyftly.local` | `SuperAdmin`, `Admin` |
| `finance.operator@swyftly.local` | `FinanceOperator` |
| `finance.approver@swyftly.local` | `FinanceApprover` |
| `support@swyftly.local` | `SupportAgent` |
| `buyer@swyftly.local` | `Buyer` with buyer profile and, when `-SeedSampleProducts` is used, one default saved delivery address |
| `seller@swyftly.local` | `Seller` with verified seller profile, published storefront, payout placeholder approval, seller balance, standard delivery method, and, when `-SeedSampleProducts` is used, eight published sample products |
| `seller.pending@swyftly.local` | `Seller` with completed onboarding and an `UnderReview` seller verification when `-SeedSellerFlowDemo` is used |

`-SeedSellerFlowDemo` also creates one product in `PendingReview` and one ad campaign in `PendingReview` for the verified seller so `/admin/products` and `/admin/ads` can be tested immediately. See `docs/seller-flow-test-runbook.md` for the manual checklist and `docs/seller-flow-qa-results.md` for the latest QA evidence.

Do not use these accounts in production.
