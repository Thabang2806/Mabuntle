# Payment And Ledger Review

Last reviewed: 2026-05-19

## Remediation Status

- Prompt 68 fixed the critical duplicate payment initiation path by returning an existing active payment per order and adding a filtered unique active-payment index.
- Prompt 68 hardened adjacent payment status handling: authorization no longer settles orders, and late paid webhooks no longer resurrect failed/cancelled payments or orders.
- Prompt 68 made refund approval safer by persisting processing state before provider calls, adding a refund concurrency token, and using a provider idempotency key.
- The next gap remediation pass hardened inventory reservations with database-side conditional reserved-stock updates and check constraints.
- The 2026-05-19 finance hardening pass added duplicate webhook race handling, refund payout adjustments, a fake-provider payout lifecycle, finance policies, dual control, and HttpOnly refresh-token cookies.
- The 2026-05-19 dispute money-movement pass made buyer-favoured dispute resolution create a requested refund for the remaining refundable payment amount while keeping held payout funds in place for finance completion.
- The 2026-05-19 seller payout privacy pass removed internal ledger, order, and payment identifiers from seller payout history while keeping them available on admin finance reconciliation views.

## Blocking Correctness Issues

- None currently open from this review after Prompt 68.

## High Edge Cases

- None currently open from this review. Buyer-favoured disputes now start the refund workflow, but provider execution remains under finance approval/manual PayFast confirmation.

## Medium Issues

- Real payment and payout provider reconciliation remains future work; the current payout provider is a fake local implementation.

## Low Issues

- None currently open from this review.

## Suggested Remediation Tasks

- Define production provider reconciliation and payout retry operations for real provider integration.

## Tests To Add

- Add provider-backed end-to-end tests after PayFast sandbox credentials/callback URLs are available.
