# Seller Flow QA Results

Date: 2026-05-27

## Scope

Phase 10F checked the seeded seller lifecycle across the existing seller and admin surfaces. This pass was intentionally verification-led: no new routes, backend APIs, database migrations, payment behavior, carrier-provider behavior, or new seller workflows were added.

## Seed Command

Run twice from the repository root to verify idempotency:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\seed-dev-users.ps1 -Password "UseYourOwnDevPassword1!" -ApplyMigrations -SeedSampleProducts -SeedSellerFlowDemo
```

Result: passed both runs. The seed created or reused the standard admin, buyer, verified seller, and pending seller accounts, seeded the buyer demo catalog, and seeded pending seller/product/ad review records.

## QA Checklist

| Area | Routes | Status | Notes |
| --- | --- | --- | --- |
| Seller acquisition | `/sell`, `/register/seller` | Pass by build/spec/static route check | Public seller entry and seller registration route are wired. Human copy/visual review should still be repeated in browser. |
| Seller onboarding | `/seller` | Pass by tests/static route check | Pending, under-review, rejected, suspended, and verified dashboard states are covered by component tests. Evidence upload remains optional review context. |
| Seller settings | `/seller/settings/store` | Pass by build/spec/static route check | Store profile, delivery methods, policies, payout change request, and notification preferences are covered by existing services/tests. |
| Seller products | `/seller/products`, `/seller/products/new`, `/seller/products/:id/edit` | Pass by build/spec/static route check | Product editor, listing revisions, variant/pricing revisions, moderation context, and image flows compile and remain tested. |
| Seller inventory | `/seller/inventory` | Pass by build/spec/static route check | CSV import/export, SKU/barcode search, movement history, and Phase 10E stock ledger panels compile and remain tested. |
| Seller ads | `/seller/ads`, `/seller/ads/new`, `/seller/ads/:id` | Pass by build/spec/static route check | Campaign create/detail/review-state flows compile and remain tested. |
| Seller operations | `/seller/orders`, `/seller/orders/:orderId`, `/seller/returns`, `/seller/returns/:returnRequestId` | Pass by build/spec/static route check | Fulfilment, policy context, carrier context, returns, and stock-ledger context compile and remain tested. |
| Seller notifications | `/seller/notifications` | Pass by build/spec/static route check | Notification list/read/read-all route is wired and realtime service tests passed. |
| Admin approval queues | `/admin/sellers`, `/admin/products`, `/admin/ads` | Pass by build/spec/static route check | Seeded pending seller/product/ad rows should be visible after logging in as `admin@swyftly.local`. |

## Defects Fixed

- The verified seller dashboard "What to check next" panel previously combined support tickets, unread notifications, and ad review counts in one support row that linked only to `/seller/support`. The row is now split so support remains support-specific and seller updates route to `/seller/notifications` or `/seller/ads` depending on the live summary.

## Verification Evidence

```powershell
dotnet build backend\Swyftly.sln --no-restore
dotnet test backend\Swyftly.sln --no-build
dotnet dotnet-ef migrations has-pending-model-changes --project backend\src\Swyftly.Infrastructure --startup-project backend\src\Swyftly.Api --context SwyftlyDbContext --no-build
cd frontend\swyftly-web
cmd /c npm run build
cmd /c npm run test:ci
rg -n "\x{00C2}|\x{00C3}|\x{00E2}|\x{00F0}|\x{FFFD}" src
```

Result: all passed. EF reported no pending model changes. Angular build reported an initial total of `648.94 kB`. The mojibake scan returned no matches.

## Manual Browser Follow-Up

This terminal pass did not perform a human visual walkthrough in an interactive browser. Use `docs/seller-flow-test-runbook.md` with the seeded accounts to complete desktop/mobile visual checks for the listed routes, especially approval actions that intentionally mutate seeded records.

## Deferred Follow-Ups

- Admin queues remain pending-review focused rather than all-state operational lists.
- Historical stock-ledger backfill for pre-10E orders remains future work.
- Dedicated restock decisions after return/refund completion remain future work.
- Hardware barcode scanner integration remains future work.
- Real carrier-provider integration and sensitive payout-bank storage remain future work.
