import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { getApiErrorMessage } from '../auth/api-error';
import { AdminDashboardSummaryResponse } from '../admin/admin-dashboard.models';
import { AdminDashboardService } from '../admin/admin-dashboard.service';

@Component({
  selector: 'app-admin-page',
  imports: [MatButtonModule, RouterLink],
  template: `
    <section class="page admin-dashboard">
      <div class="page-header">
        <span class="eyebrow">Admin command centre</span>
        <h1>Dashboard</h1>
        <p>Track operational queues without exposing buyer or seller details on the landing page.</p>
      </div>

      @if (isLoading()) {
        <div class="route-card">Loading dashboard...</div>
      } @else {
        @if (errorMessage()) {
          <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
        }

        @if (summary() && !errorMessage()) {
          <div class="dashboard-metrics" aria-label="Admin dashboard metrics">
            @for (metric of metrics(); track metric.label) {
              <a class="dashboard-metric-card" [routerLink]="metric.route">
                <span>{{ metric.label }}</span>
                <strong>{{ metric.value }}</strong>
              </a>
            }
          </div>

          <div class="route-card compact-card">
            <span class="status-pill">Finance placeholders</span>
            <div class="dashboard-finance">
              <div>
                <span>Total gross sales</span>
                <strong>{{ summary()!.totalGrossSalesPlaceholder }}</strong>
              </div>
              <div>
                <span>Platform commission</span>
                <strong>{{ summary()!.platformCommissionPlaceholder }}</strong>
              </div>
            </div>
            <a mat-stroked-button routerLink="/admin/reports">Open reports</a>
          </div>
        }
      }

      <nav class="admin-nav-grid" aria-label="Admin sections">
        @for (item of navigationItems; track item.route) {
          <a class="route-card compact-card" [routerLink]="item.route">
            <span class="status-pill">{{ item.status }}</span>
            <h2>{{ item.label }}</h2>
            <p>{{ item.description }}</p>
          </a>
        }
      </nav>
    </section>
  `
})
export class AdminPageComponent implements OnInit {
  private readonly adminDashboardService = inject(AdminDashboardService);

  protected readonly summary = signal<AdminDashboardSummaryResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly navigationItems = [
    { label: 'Sellers', route: '/admin/sellers', status: 'Review', description: 'Seller verification queue.' },
    { label: 'Products', route: '/admin/products', status: 'Review', description: 'Product moderation queue.' },
    { label: 'Orders', route: '/admin/orders', status: 'Shell', description: 'Order operations landing area.' },
    { label: 'Payments', route: '/admin/payments', status: 'Shell', description: 'Payment operations landing area.' },
    { label: 'Reports', route: '/admin/reports', status: 'Finance', description: 'Marketplace and finance reporting.' },
    { label: 'AI usage', route: '/admin/ai-usage', status: 'Analytics', description: 'AI usage, cost, quality, and moderation reporting.' },
    { label: 'Refunds', route: '/admin/refunds', status: 'Shell', description: 'Refund operations landing area.' },
    { label: 'Disputes', route: '/admin/disputes', status: 'Shell', description: 'Dispute operations landing area.' },
    { label: 'Payouts', route: '/admin/payouts', status: 'Shell', description: 'Payout operations landing area.' },
    { label: 'Ads', route: '/admin/ads', status: 'Review', description: 'Ad campaign review queue.' },
    { label: 'Audit logs', route: '/admin/audit-logs', status: 'Audit', description: 'Administrative action history.' }
  ];

  async ngOnInit(): Promise<void> {
    await this.loadSummary();
  }

  protected metrics() {
    const summary = this.summary();
    if (!summary) {
      return [];
    }

    return [
      { label: 'Pending seller approvals', value: summary.pendingSellerApprovals, route: '/admin/sellers' },
      { label: 'Pending product reviews', value: summary.pendingProductReviews, route: '/admin/products' },
      { label: 'New orders today', value: summary.newOrdersToday, route: '/admin/orders' },
      { label: 'Open disputes', value: summary.openDisputes, route: '/admin/disputes' },
      { label: 'Pending refunds', value: summary.pendingRefunds, route: '/admin/refunds' },
      { label: 'Pending payouts', value: summary.pendingPayouts, route: '/admin/payouts' }
    ];
  }

  private async loadSummary(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.summary.set(await this.adminDashboardService.getSummary());
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }
}
