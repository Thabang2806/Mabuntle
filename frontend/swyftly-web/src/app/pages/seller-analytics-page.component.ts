import { CurrencyPipe, DatePipe, DecimalPipe, PercentPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import {
  SellerAdPerformanceDetailResponse,
  SellerAnalyticsCsvReport,
  SellerAnalyticsPerformanceRequest,
  SellerAnalyticsPerformanceResponse,
  SellerAnalyticsSummaryResponse,
  SellerInventoryPerformanceResponse,
  SellerProductPerformanceResponse,
  SellerSalesTrendBucketResponse
} from '../seller/seller-analytics.models';
import { SellerAnalyticsService } from '../seller/seller-analytics.service';
import { getApiErrorMessage } from '../auth/api-error';
import { SellerWorkspaceNavComponent } from '../seller/seller-workspace-nav.component';

@Component({
  selector: 'app-seller-analytics-page',
  imports: [
    CurrencyPipe,
    DatePipe,
    DecimalPipe,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    PercentPipe,
    ReactiveFormsModule,
    RouterLink,
    SellerWorkspaceNavComponent
  ],
  template: `
    <section class="page seller-ops-page seller-products seller-analytics-page">
      <app-seller-workspace-nav />

      <div class="page-header seller-products-header">
        <div>
          <span class="eyebrow">Seller analytics</span>
          <h1>Performance workspace</h1>
          <p>Review seller-owned sales, product, inventory, ad, and customer-care signals. Conversion tracking is not captured yet.</p>
        </div>
        <a mat-stroked-button routerLink="/seller">Seller workspace</a>
      </div>

      <form [formGroup]="filtersForm" (ngSubmit)="loadAnalytics()" class="route-card admin-audit-filters" novalidate>
        <mat-form-field appearance="outline" class="swyftly-field">
          <mat-label>From</mat-label>
          <input matInput type="datetime-local" formControlName="from">
        </mat-form-field>

        <mat-form-field appearance="outline" class="swyftly-field">
          <mat-label>To</mat-label>
          <input matInput type="datetime-local" formControlName="to">
        </mat-form-field>

        <mat-form-field appearance="outline" class="swyftly-field">
          <mat-label>Bucket</mat-label>
          <mat-select formControlName="bucket">
            <mat-option value="Day">Daily</mat-option>
            <mat-option value="Week">Weekly</mat-option>
          </mat-select>
        </mat-form-field>

        <div class="admin-audit-actions">
          <button mat-flat-button type="submit" [disabled]="isLoading()">Apply filters</button>
          @if (performance() && !errorMessage()) {
            @for (report of csvReports; track report) {
              <a mat-stroked-button [href]="getCsvExportUrl(report)" target="_blank" rel="noreferrer">{{ report }} CSV</a>
            }
          }
        </div>
      </form>

      @if (isLoading()) {
        <div class="route-card">Loading analytics...</div>
      } @else if (summary() && performance()) {
        @if (errorMessage()) {
          <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
        }

        <div class="route-card compact-card">
          <span class="status-pill">Range {{ performance()!.fromUtc | date:'mediumDate' }} to {{ performance()!.toUtc | date:'mediumDate' }}</span>
          <p>Bucketed by {{ performance()!.bucket.toLowerCase() }}. Storefront sessions and conversion-rate tracking are not captured yet.</p>
        </div>

        <div class="dashboard-metrics" aria-label="Seller analytics metrics">
          <div class="dashboard-metric-card"><span>Total sales</span><strong>{{ summary()!.totalSales | currency:'ZAR':'symbol-narrow' }}</strong></div>
          <div class="dashboard-metric-card"><span>Orders</span><strong>{{ summary()!.orderCount }}</strong></div>
          <div class="dashboard-metric-card"><span>Average order value</span><strong>{{ summary()!.averageOrderValue | currency:'ZAR':'symbol-narrow' }}</strong></div>
          <div class="dashboard-metric-card"><span>Products sold</span><strong>{{ summary()!.productsSold }}</strong></div>
          <div class="dashboard-metric-card"><span>Refund rate</span><strong>{{ summary()!.refundRate | percent:'1.0-2' }}</strong></div>
          <div class="dashboard-metric-card"><span>Return rate</span><strong>{{ summary()!.returnRate | percent:'1.0-2' }}</strong></div>
        </div>

        <div class="dashboard-metrics" aria-label="Selected range metrics">
          <div class="dashboard-metric-card"><span>Range gross sales</span><strong>{{ rangeGrossSales() | currency:'ZAR':'symbol-narrow' }}</strong></div>
          <div class="dashboard-metric-card"><span>Range net sales</span><strong>{{ rangeNetSales() | currency:'ZAR':'symbol-narrow' }}</strong></div>
          <div class="dashboard-metric-card"><span>Range refunds</span><strong>{{ rangeRefunds() | currency:'ZAR':'symbol-narrow' }}</strong></div>
          <div class="dashboard-metric-card"><span>Range units sold</span><strong>{{ rangeUnitsSold() }}</strong></div>
        </div>

        <div class="admin-detail-layout">
          <div class="admin-detail-main">
            <article class="route-card admin-detail-card">
              <h2>Sales trend</h2>
              @if (performance()!.salesTrend.length === 0) {
                <p>No sales activity exists for this range.</p>
              } @else {
                <div class="admin-table audit-table" role="table" aria-label="Sales trend">
                  <div class="admin-table-row heading" role="row">
                    <span role="columnheader">Period</span>
                    <span role="columnheader">Orders</span>
                    <span role="columnheader">Gross</span>
                    <span role="columnheader">Refunds</span>
                    <span role="columnheader">Net</span>
                    <span role="columnheader">Units</span>
                  </div>
                  @for (bucket of visibleSalesTrend(); track bucket.periodStartUtc) {
                    <div class="admin-table-row" role="row">
                      <span role="cell">{{ bucket.periodStartUtc | date:'mediumDate' }}</span>
                      <span role="cell">{{ bucket.orderCount }}</span>
                      <span role="cell">{{ bucket.grossSales | currency:'ZAR':'symbol-narrow' }}</span>
                      <span role="cell">{{ bucket.refundedAmount | currency:'ZAR':'symbol-narrow' }}</span>
                      <span role="cell">{{ bucket.netSales | currency:'ZAR':'symbol-narrow' }}</span>
                      <span role="cell">{{ bucket.unitsSold }}</span>
                    </div>
                  }
                </div>
              }
            </article>

            <article class="route-card admin-detail-card">
              <h2>Product performance</h2>
              @if (performance()!.productPerformance.length === 0) {
                <p>No product data is available yet.</p>
              } @else {
                <div class="admin-table audit-table" role="table" aria-label="Product performance">
                  <div class="admin-table-row heading" role="row">
                    <span role="columnheader">Product</span>
                    <span role="columnheader">Sold</span>
                    <span role="columnheader">Revenue</span>
                    <span role="columnheader">Returns</span>
                    <span role="columnheader">Available</span>
                    <span role="columnheader">Action</span>
                  </div>
                  @for (product of visibleProductPerformance(); track product.productId) {
                    <div class="admin-table-row" role="row">
                      <span role="cell">
                        <strong>{{ product.productTitle ?? 'Untitled product' }}</strong>
                        <small>{{ product.status }}</small>
                      </span>
                      <span role="cell">{{ product.unitsSold }}</span>
                      <span role="cell">{{ product.grossSales | currency:'ZAR':'symbol-narrow' }}</span>
                      <span role="cell">{{ product.returnCount }} / {{ product.returnRate | percent:'1.0-2' }}</span>
                      <span role="cell">{{ product.availableQuantity }}</span>
                      <span role="cell"><a mat-stroked-button [routerLink]="['/seller/products', product.productId, 'edit']">Open</a></span>
                    </div>
                  }
                </div>
              }
            </article>

            <article class="route-card admin-detail-card">
              <h2>Inventory performance</h2>
              @if (performance()!.inventoryPerformance.length === 0) {
                <p>No variants are available for this seller yet.</p>
              } @else {
                <div class="admin-table audit-table" role="table" aria-label="Inventory performance">
                  <div class="admin-table-row heading" role="row">
                    <span role="columnheader">Variant</span>
                    <span role="columnheader">Barcode</span>
                    <span role="columnheader">Stock</span>
                    <span role="columnheader">Reserved</span>
                    <span role="columnheader">Available</span>
                    <span role="columnheader">State</span>
                  </div>
                  @for (item of visibleInventoryPerformance(); track item.productVariantId) {
                    <div class="admin-table-row" role="row">
                      <span role="cell">
                        <strong>{{ item.productTitle ?? 'Untitled product' }}</strong>
                        <small>{{ item.sku }} / {{ item.size }} / {{ item.colour }}</small>
                      </span>
                      <span role="cell">{{ item.barcode ?? 'Not set' }}</span>
                      <span role="cell">{{ item.stockQuantity }}</span>
                      <span role="cell">{{ item.reservedQuantity }}</span>
                      <span role="cell">{{ item.availableQuantity }}</span>
                      <span role="cell">
                        <span class="status-pill">{{ item.isOutOfStock ? 'Out of stock' : item.isLowStock ? 'Low stock' : item.status }}</span>
                      </span>
                    </div>
                  }
                </div>
              }
            </article>
          </div>

          <aside class="admin-actions">
            <div class="route-card admin-action-card">
              <h2>Customer care</h2>
              <dl class="admin-facts">
                <div><dt>Returns</dt><dd>{{ performance()!.customerCareSummary.returnCount }}</dd></div>
                <div><dt>Open returns</dt><dd>{{ performance()!.customerCareSummary.openReturnCount }}</dd></div>
                <div><dt>Refunds</dt><dd>{{ performance()!.customerCareSummary.refundCount }}</dd></div>
                <div><dt>Refunded</dt><dd>{{ performance()!.customerCareSummary.refundedAmount | currency:'ZAR':'symbol-narrow' }}</dd></div>
                <div><dt>Support tickets</dt><dd>{{ performance()!.customerCareSummary.supportTicketCount }}</dd></div>
                <div><dt>Open support</dt><dd>{{ performance()!.customerCareSummary.openSupportTicketCount }}</dd></div>
                <div><dt>Disputes</dt><dd>{{ performance()!.customerCareSummary.disputeCount }}</dd></div>
                <div><dt>Active disputes</dt><dd>{{ performance()!.customerCareSummary.activeDisputeCount }}</dd></div>
              </dl>
            </div>

            <div class="route-card admin-action-card">
              <h2>Ad performance</h2>
              @if (performance()!.adPerformance.length === 0) {
                <p>No ad activity has been recorded for this range.</p>
              } @else {
                <div class="admin-product-risks">
                  @for (campaign of visibleAdPerformance(); track campaign.adCampaignId) {
                    <div>
                      <span class="status-pill">{{ campaign.status }}</span>
                      <strong>{{ campaign.name }}</strong>
                      <span>{{ campaign.clicks }} clicks / {{ campaign.revenueGenerated | currency:'ZAR':'symbol-narrow' }} revenue</span>
                      <small>Spend {{ campaign.spend | currency:'ZAR':'symbol-narrow' }} / ROAS {{ campaign.returnOnAdSpend | number:'1.0-2' }}</small>
                    </div>
                  }
                </div>
              }
            </div>

            <div class="route-card admin-action-card">
              <h2>AI usage</h2>
              <dl class="admin-facts">
                <div><dt>Requests</dt><dd>{{ summary()!.aiUsage.requests }}</dd></div>
                <div><dt>Successful</dt><dd>{{ summary()!.aiUsage.successfulRequests }}</dd></div>
                <div><dt>Failed</dt><dd>{{ summary()!.aiUsage.failedRequests }}</dd></div>
                <div><dt>Estimated cost</dt><dd>{{ summary()!.aiUsage.estimatedCost | currency:'USD':'symbol-narrow' }}</dd></div>
                <div><dt>Average latency</dt><dd>{{ summary()!.aiUsage.averageLatencyMs }} ms</dd></div>
                <div><dt>Acceptance rate</dt><dd>{{ summary()!.aiUsage.suggestionAcceptanceRate | percent:'1.0-2' }}</dd></div>
              </dl>
              <p>{{ summary()!.aiUsage.qualityScoreImprovementNote }}</p>
            </div>
          </aside>
        </div>
      } @else {
        <p class="auth-alert error" role="alert">{{ errorMessage() ?? 'Analytics could not be loaded.' }}</p>
      }
    </section>
  `
})
export class SellerAnalyticsPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly analyticsService = inject(SellerAnalyticsService);

  protected readonly summary = signal<SellerAnalyticsSummaryResponse | null>(null);
  protected readonly performance = signal<SellerAnalyticsPerformanceResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly csvReports: SellerAnalyticsCsvReport[] = ['Sales', 'Products', 'Inventory', 'Ads', 'Returns'];

  protected readonly filtersForm = this.formBuilder.group({
    from: [this.toDateTimeLocalInput(new Date(Date.now() - 30 * 24 * 60 * 60 * 1000))],
    to: [this.toDateTimeLocalInput(new Date())],
    bucket: ['Day' as 'Day' | 'Week']
  });

  async ngOnInit(): Promise<void> {
    await this.loadAnalytics();
  }

  protected async loadAnalytics(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    const request = this.getPerformanceRequest();
    if (request.fromUtc && request.toUtc && request.fromUtc > request.toUtc) {
      this.errorMessage.set('From must be earlier than or equal to To.');
      this.summary.set(null);
      this.performance.set(null);
      this.isLoading.set(false);
      return;
    }

    try {
      const [summary, performance] = await Promise.all([
        this.analyticsService.getSummary(),
        this.analyticsService.getPerformance(request)
      ]);
      this.summary.set(summary);
      this.performance.set(performance);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.summary.set(null);
      this.performance.set(null);
    } finally {
      this.isLoading.set(false);
    }
  }

  protected getCsvExportUrl(report: SellerAnalyticsCsvReport): string {
    return this.analyticsService.getCsvExportUrl(report, this.getPerformanceRequest());
  }

  protected visibleSalesTrend(): SellerSalesTrendBucketResponse[] {
    return this.performance()?.salesTrend.slice(-12) ?? [];
  }

  protected visibleProductPerformance(): SellerProductPerformanceResponse[] {
    return this.performance()?.productPerformance.slice(0, 10) ?? [];
  }

  protected visibleInventoryPerformance(): SellerInventoryPerformanceResponse[] {
    return this.performance()?.inventoryPerformance.slice(0, 10) ?? [];
  }

  protected visibleAdPerformance(): SellerAdPerformanceDetailResponse[] {
    return this.performance()?.adPerformance.slice(0, 5) ?? [];
  }

  protected rangeGrossSales(): number {
    return this.performance()?.salesTrend.reduce((total, bucket) => total + bucket.grossSales, 0) ?? 0;
  }

  protected rangeNetSales(): number {
    return this.performance()?.salesTrend.reduce((total, bucket) => total + bucket.netSales, 0) ?? 0;
  }

  protected rangeRefunds(): number {
    return this.performance()?.salesTrend.reduce((total, bucket) => total + bucket.refundedAmount, 0) ?? 0;
  }

  protected rangeUnitsSold(): number {
    return this.performance()?.salesTrend.reduce((total, bucket) => total + bucket.unitsSold, 0) ?? 0;
  }

  private getPerformanceRequest(): SellerAnalyticsPerformanceRequest {
    const filters = this.filtersForm.getRawValue();
    return {
      fromUtc: this.toIsoStringOrUndefined(filters.from),
      toUtc: this.toIsoStringOrUndefined(filters.to),
      bucket: filters.bucket
    };
  }

  private toIsoStringOrUndefined(value: string): string | undefined {
    return value ? new Date(value).toISOString() : undefined;
  }

  private toDateTimeLocalInput(value: Date): string {
    const localTime = new Date(value.getTime() - value.getTimezoneOffset() * 60_000);
    return localTime.toISOString().slice(0, 16);
  }
}
