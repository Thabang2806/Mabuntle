import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { AdminWorkspaceNavComponent } from '../admin/admin-workspace-nav.component';
import { AdminProductSummaryResponse } from '../admin/admin-product.models';
import { AdminProductService } from '../admin/admin-product.service';
import { getApiErrorMessage } from '../auth/api-error';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-admin-products-page',
  imports: [
    AdminWorkspaceNavComponent,
    DatePipe,
    EmptyStateComponent,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page admin-review">
      <app-admin-workspace-nav />

      <app-page-header
        eyebrow="Admin review"
        heading="Product review queue"
        description="Triage submitted products and AI-flagged listings before marketplace publication."
      >
        <div pageHeaderActions>
          <a mat-stroked-button routerLink="/admin/sellers">Seller queue</a>
          <a mat-stroked-button routerLink="/admin/audit-logs">Audit logs</a>
        </div>
      </app-page-header>

      <form [formGroup]="filtersForm" (ngSubmit)="applyFilters()" class="route-card admin-moderation-filters" novalidate>
        <mat-form-field appearance="outline">
          <mat-label>Search products</mat-label>
          <input matInput formControlName="search" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Status</mat-label>
          <input matInput formControlName="status" placeholder="PendingReview" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Seller</mat-label>
          <input matInput formControlName="seller" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Risk</mat-label>
          <input matInput formControlName="risk" placeholder="high, none" />
        </mat-form-field>

        <div class="admin-audit-actions">
          <button mat-flat-button type="submit">Apply filters</button>
          <button mat-stroked-button type="button" (click)="clearFilters()">Clear</button>
        </div>
      </form>

      @if (isLoading()) {
        <div class="route-card">Loading product reviews...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (filteredProducts().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Clear"
            heading="No products pending review"
            message="Submitted products and AI-flagged listings will appear here."
          />
        } @else {
          <div class="admin-table admin-moderation-table" role="table" aria-label="Pending product reviews">
            <div class="admin-table-row heading admin-moderation-table-row" role="row">
              <span role="columnheader">Product</span>
              <span role="columnheader">Seller</span>
              <span role="columnheader">Updated</span>
              <span role="columnheader">Status</span>
              <span role="columnheader">Action</span>
            </div>

            @for (product of filteredProducts(); track product.productId) {
              <div class="admin-table-row admin-moderation-table-row" role="row">
                <span role="cell">
                  <strong>{{ product.title ?? 'Untitled product' }}</strong>
                  <small>{{ product.categoryPath ?? 'No category' }}</small>
                </span>
                <span role="cell">
                  <strong>{{ product.sellerDisplayName ?? 'Unnamed seller' }}</strong>
                  <small>{{ product.sellerVerificationStatus ?? 'Unknown seller status' }}</small>
                </span>
                <span role="cell">
                  <strong>{{ product.updatedAtUtc | date:'mediumDate' }}</strong>
                  <small>{{ product.updatedAtUtc | date:'shortTime' }}</small>
                </span>
                <span role="cell">
                  <app-status-badge [label]="product.status" [tone]="productStatusTone(product.status)" />
                  @if (product.highRiskFlagCount > 0) {
                    <small>{{ product.highRiskFlagCount }} high-risk flag{{ product.highRiskFlagCount === 1 ? '' : 's' }}</small>
                  } @else {
                    <small>No high-risk flags</small>
                  }
                </span>
                <span role="cell">
                  <a mat-stroked-button [routerLink]="['/admin/products', product.productId]">Review</a>
                </span>
              </div>
            }
          </div>

          <p class="audit-count">{{ filteredProducts().length }} of {{ pendingProducts().length }} product{{ pendingProducts().length === 1 ? '' : 's' }}</p>
        }
      }
    </section>
  `
})
export class AdminProductsPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly adminProductService = inject(AdminProductService);

  protected readonly pendingProducts = signal<AdminProductSummaryResponse[]>([]);
  protected readonly filters = signal({ search: '', status: '', seller: '', risk: '' });
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly filtersForm = this.formBuilder.group({
    search: [''],
    status: [''],
    seller: [''],
    risk: ['']
  });

  protected readonly filteredProducts = computed(() => {
    const { search, status, seller, risk } = this.filters();
    const normalizedSearch = search.trim().toLowerCase();
    const normalizedStatus = status.trim().toLowerCase();
    const normalizedSeller = seller.trim().toLowerCase();
    const normalizedRisk = risk.trim().toLowerCase();

    return this.pendingProducts().filter(product => {
      const haystack = [
        product.title,
        product.categoryPath,
        product.sellerDisplayName,
        product.sellerVerificationStatus,
        product.status
      ]
        .filter(Boolean)
        .join(' ')
        .toLowerCase();
      const sellerText = [product.sellerDisplayName, product.sellerVerificationStatus, product.sellerId]
        .filter(Boolean)
        .join(' ')
        .toLowerCase();
      const riskText = product.highRiskFlagCount > 0 ? 'high risk flagged' : 'none no risk clear';

      return (normalizedSearch.length === 0 || haystack.includes(normalizedSearch)) &&
        (normalizedStatus.length === 0 || product.status.toLowerCase().includes(normalizedStatus)) &&
        (normalizedSeller.length === 0 || sellerText.includes(normalizedSeller)) &&
        (normalizedRisk.length === 0 || riskText.includes(normalizedRisk));
    });
  });

  async ngOnInit(): Promise<void> {
    await this.loadPendingProducts();
  }

  protected applyFilters(): void {
    this.filters.set(this.filtersForm.getRawValue());
  }

  protected clearFilters(): void {
    this.filtersForm.reset({ search: '', status: '', seller: '', risk: '' });
    this.applyFilters();
  }

  protected productStatusTone(status: string): StatusBadgeTone {
    if (['Published', 'Approved'].includes(status)) {
      return 'success';
    }

    if (['Rejected', 'NeedsAdminReview'].includes(status)) {
      return 'danger';
    }

    return 'warning';
  }

  private async loadPendingProducts(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.pendingProducts.set(await this.adminProductService.getPendingReviewProducts());
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }
}
