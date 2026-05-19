# Payment And Ledger Review

Last reviewed: 2026-05-19

## Blocking Correctness Issues

- Duplicate payment initiation can double-credit the ledger. `EfPaymentService.InitiatePaymentAsync` creates a new `Payment` for every pending-payment order initiation, while ledger idempotency is per payment id. A buyer can start payment twice for one order, and two successful provider references can create duplicate ledger and payout records.
- Refund approval is not atomic or idempotent around the provider call. `EfRefundWorkflowService` calls the provider before persisting a processing/refunded state, so retry, crash, or concurrent admin approval can issue duplicate provider refunds.

## High Edge Cases

- Inventory reservations can oversell under concurrency because variant availability is read and then updated without a row-version/concurrency token or database-side conditional update.
- Webhook idempotency is race-prone. Concurrent duplicate webhooks can both pass the pre-insert lookup; one later fails on the unique index instead of returning an idempotent success.
- Out-of-order payment events can resurrect failed/cancelled orders. A failed webhook can cancel the order, then a later paid webhook can mark payment/order paid and create ledger entries.
- `Authorized` is treated as paid/captured. For real providers, authorization is not settlement and should not credit seller balances or confirm reservations.
- Refunds debit seller balances but do not adjust existing payout records/items, so payouts can overstate payable amounts after refunds.

## Medium Issues

- Payout lifecycle remains incomplete for available/processing/paid-out/failed states, so finance reports for processed or failed payouts are mostly placeholders.
- Buyer-favoured disputes do not trigger refund creation, payout reversal, balance adjustment, or order status cleanup.

## Low Issues

- Seller payout responses expose internal ledger, order, and payment ids. Authorization is present, but external seller responses should avoid internal financial identifiers where practical.

## Suggested Remediation Tasks

- Make payment initiation idempotent by order: return the existing pending payment when one exists, or enforce a unique active payment per order/provider state.
- Add a processing state and concurrency guard before calling refund providers.
- Add optimistic concurrency or database conditional updates around `ProductVariant.ReservedQuantity`.
- Catch duplicate webhook unique-key violations and return the existing event result.
- Tighten payment status transitions so failed/cancelled payments and orders cannot be later marked paid without explicit reconciliation.
- Treat only captured/paid provider statuses as settled.
- Update payout records when refunds reverse seller payable amounts.
- Design the full payout availability and external payout processing lifecycle before production finance use.

## Tests To Add

- Duplicate payment initiation for one order does not create multiple active payments or duplicate ledger entries.
- Concurrent refund approval cannot call the provider twice.
- Duplicate concurrent webhook processing returns idempotent success.
- Failed-then-paid webhook sequence does not resurrect a cancelled order.
- Authorized webhook does not create ledger entries.
- Refunds reduce or reverse the associated pending payout.
