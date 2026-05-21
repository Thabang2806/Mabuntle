import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { BuyerNotificationResponse, BuyerProductReviewResponse, BuyerWishlistItemResponse } from '../buyer/buyer-engagement.models';
import { BuyerEngagementService } from '../buyer/buyer-engagement.service';
import { BuyerDisputeResponse } from '../buyer/buyer-dispute.models';
import { BuyerDisputeService } from '../buyer/buyer-dispute.service';
import { BuyerOrderResult } from '../buyer/buyer-order.models';
import { BuyerOrderService } from '../buyer/buyer-order.service';
import { BuyerReturnRequestResult } from '../buyer/buyer-return.models';
import { BuyerReturnService } from '../buyer/buyer-return.service';
import { BuyerSupportTicketResponse } from '../buyer/buyer-support.models';
import { BuyerSupportService } from '../buyer/buyer-support.service';
import { BuyerWorkspaceNavComponent } from '../buyer/buyer-workspace-nav.component';
import { getApiErrorMessage } from '../auth/api-error';
import { DashboardCardComponent } from '../shared/ui/dashboard-card.component';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-account-page',
  imports: [
    BuyerWorkspaceNavComponent,
    CurrencyPipe,
    DashboardCardComponent,
    DatePipe,
    EmptyStateComponent,
    MatButtonModule,
    PageHeaderComponent,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page buyer-ops-page">
      <app-buyer-workspace-nav />

      <app-page-header
        eyebrow="Buyer account"
        heading="Account dashboard"
        description="Track purchases, returns, disputes, and marketplace support from one workspace."
      >
        <div pageHeaderActions>
          <a mat-stroked-button routerLink="/shop">Continue shopping</a>
          <a mat-stroked-button routerLink="/cart">Cart</a>
        </div>
      </app-page-header>

      @if (isLoading()) {
        <div class="route-card">Loading account activity...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        <div class="buyer-dashboard-grid">
          <app-dashboard-card
            eyebrow="Purchases"
            heading="Orders"
            [description]="orders().length + ' order' + (orders().length === 1 ? '' : 's') + ' in your account.'"
          >
            <a mat-stroked-button routerLink="/account/orders">View orders</a>
          </app-dashboard-card>

          <app-dashboard-card
            eyebrow="After-sales"
            heading="Returns"
            [description]="activeReturns().length + ' active return' + (activeReturns().length === 1 ? '' : 's') + ' need attention.'"
          >
            <a mat-stroked-button routerLink="/account/returns">View returns</a>
          </app-dashboard-card>

          <app-dashboard-card
            eyebrow="Resolution"
            heading="Disputes"
            [description]="openDisputes().length + ' open dispute' + (openDisputes().length === 1 ? '' : 's') + ' on record.'"
          >
            <a mat-stroked-button routerLink="/account/disputes">View disputes</a>
          </app-dashboard-card>

          <app-dashboard-card
            eyebrow="Help"
            heading="Support"
            [description]="openTickets().length + ' open support ticket' + (openTickets().length === 1 ? '' : 's') + '.'"
          >
            <a mat-stroked-button routerLink="/account/support">Open support</a>
          </app-dashboard-card>

          <app-dashboard-card
            eyebrow="Saved"
            heading="Wishlist"
            [description]="wishlist().length + ' saved product' + (wishlist().length === 1 ? '' : 's') + ' in your account.'"
          >
            <a mat-stroked-button routerLink="/account/wishlist">View wishlist</a>
          </app-dashboard-card>

          <app-dashboard-card
            eyebrow="Feedback"
            heading="Reviews"
            [description]="reviews().length + ' product review' + (reviews().length === 1 ? '' : 's') + ' written.'"
          >
            <a mat-stroked-button routerLink="/account/reviews">Manage reviews</a>
          </app-dashboard-card>

          <app-dashboard-card
            eyebrow="Updates"
            heading="Notifications"
            [description]="unreadNotifications().length + ' unread notification' + (unreadNotifications().length === 1 ? '' : 's') + '.'"
          >
            <a mat-stroked-button routerLink="/account/notifications">View notifications</a>
          </app-dashboard-card>
        </div>

        @if (hasNoActivity() && !errorMessage()) {
          <app-empty-state
            eyebrow="Account"
            heading="No buyer activity yet"
            message="Orders, returns, disputes, and support tickets will appear here after you start shopping."
          >
            <a mat-flat-button routerLink="/shop">Browse marketplace</a>
          </app-empty-state>
        } @else {
          <div class="buyer-detail-grid">
            <section class="buyer-panel">
              <h2>Recent orders</h2>
              @if (recentOrders().length === 0) {
                <p>No order activity yet.</p>
              } @else {
                <div class="buyer-activity-list">
                  @for (order of recentOrders(); track order.orderId) {
                    <a [routerLink]="['/account/orders', order.orderId]">
                      <span>
                        <strong>{{ primaryItemLabel(order) }}</strong>
                        <small>{{ itemCount(order) }} item{{ itemCount(order) === 1 ? '' : 's' }} - {{ order.totalAmount | currency:'ZAR':'symbol-narrow' }}</small>
                      </span>
                      <app-status-badge [label]="order.status" [tone]="statusTone(order.status)" />
                    </a>
                  }
                </div>
              }
            </section>

            <section class="buyer-panel">
              <h2>Recent support</h2>
              @if (recentTickets().length === 0) {
                <p>No support tickets yet.</p>
              } @else {
                <div class="buyer-activity-list">
                  @for (ticket of recentTickets(); track ticket.supportTicketId) {
                    <a [routerLink]="['/account/support', ticket.supportTicketId]">
                      <span>
                        <strong>{{ ticket.subject }}</strong>
                        <small>{{ ticket.openedAtUtc | date:'mediumDate' }}</small>
                      </span>
                      <app-status-badge [label]="ticket.status" [tone]="ticketStatusTone(ticket.status)" />
                    </a>
                  }
                </div>
              }
            </section>
          </div>
        }
      }
    </section>
  `
})
export class AccountPageComponent implements OnInit {
  private readonly engagementService = inject(BuyerEngagementService);
  private readonly disputeService = inject(BuyerDisputeService);
  private readonly orderService = inject(BuyerOrderService);
  private readonly returnService = inject(BuyerReturnService);
  private readonly supportService = inject(BuyerSupportService);

  protected readonly orders = signal<BuyerOrderResult[]>([]);
  protected readonly returns = signal<BuyerReturnRequestResult[]>([]);
  protected readonly disputes = signal<BuyerDisputeResponse[]>([]);
  protected readonly tickets = signal<BuyerSupportTicketResponse[]>([]);
  protected readonly wishlist = signal<BuyerWishlistItemResponse[]>([]);
  protected readonly reviews = signal<BuyerProductReviewResponse[]>([]);
  protected readonly notifications = signal<BuyerNotificationResponse[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly activeReturns = computed(() =>
    this.returns().filter(item => !['Refunded', 'Closed', 'Rejected'].includes(item.status)));
  protected readonly openDisputes = computed(() =>
    this.disputes().filter(item => !['Resolved', 'Closed'].includes(item.status)));
  protected readonly openTickets = computed(() =>
    this.tickets().filter(item => !['Resolved', 'Closed'].includes(item.status)));
  protected readonly unreadNotifications = computed(() =>
    this.notifications().filter(item => !item.readAtUtc));
  protected readonly recentOrders = computed(() => this.orders().slice(0, 3));
  protected readonly recentTickets = computed(() => this.tickets().slice(0, 3));

  async ngOnInit(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const [orders, returns, disputes, tickets, wishlist, reviews, notifications] = await Promise.all([
        this.orderService.listOrders(),
        this.returnService.listReturns(),
        this.disputeService.listDisputes(),
        this.supportService.listTickets(),
        this.engagementService.listWishlist(),
        this.engagementService.listBuyerReviews(),
        this.engagementService.listNotifications()
      ]);
      this.orders.set(orders);
      this.returns.set(returns);
      this.disputes.set(disputes);
      this.tickets.set(tickets);
      this.wishlist.set(wishlist);
      this.reviews.set(reviews);
      this.notifications.set(notifications);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  protected hasNoActivity(): boolean {
    return this.orders().length === 0 &&
      this.returns().length === 0 &&
      this.disputes().length === 0 &&
      this.tickets().length === 0 &&
      this.wishlist().length === 0 &&
      this.reviews().length === 0 &&
      this.notifications().length === 0;
  }

  protected itemCount(order: BuyerOrderResult): number {
    return order.items.reduce((total, item) => total + item.quantity, 0);
  }

  protected primaryItemLabel(order: BuyerOrderResult): string {
    const firstItem = order.items[0];
    return firstItem?.productTitle ?? firstItem?.sku ?? 'Order items';
  }

  protected statusTone(status: string): StatusBadgeTone {
    if (['Paid', 'Processing', 'ReadyToShip', 'AwaitingFulfilment'].includes(status)) {
      return 'accent';
    }

    if (['Shipped', 'Delivered', 'Completed'].includes(status)) {
      return 'success';
    }

    if (['Cancelled', 'Refunded', 'Disputed', 'Failed'].includes(status)) {
      return 'danger';
    }

    return 'neutral';
  }

  protected ticketStatusTone(status: string): StatusBadgeTone {
    if (['Open', 'WaitingForBuyer', 'Escalated'].includes(status)) {
      return 'warning';
    }

    if (['Resolved', 'Closed'].includes(status)) {
      return 'success';
    }

    return 'neutral';
  }
}
