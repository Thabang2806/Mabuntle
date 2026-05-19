import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { AdminProductSummaryResponse } from '../admin/admin-product.models';
import { AdminProductService } from '../admin/admin-product.service';
import { getApiErrorMessage } from '../auth/api-error';

@Component({
  selector: 'app-admin-products-page',
  imports: [DatePipe, MatButtonModule, RouterLink],
  template: `
    <section class="page admin-review">
      <a class="admin-back-link" routerLink="/admin">Back to admin</a>

      <div class="page-header">
        <span class="eyebrow">Admin review</span>
        <h1>Product review queue</h1>
        <p>Review submitted products before they are published to the marketplace.</p>
      </div>

      @if (isLoading()) {
        <div class="route-card">Loading product reviews...</div>
      } @else {
        @if (errorMessage()) {
          <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
        }

        @if (pendingProducts().length === 0 && !errorMessage()) {
          <div class="route-card">
            <span class="status-pill">Clear</span>
            <h2>No products pending review</h2>
            <p>Submitted products and AI-flagged listings will appear here.</p>
          </div>
        } @else {
          <div class="admin-table" role="table" aria-label="Pending product reviews">
            <div class="admin-table-row heading" role="row">
              <span role="columnheader">Product</span>
              <span role="columnheader">Seller</span>
              <span role="columnheader">Updated</span>
              <span role="columnheader">Status</span>
              <span role="columnheader">Action</span>
            </div>

            @for (product of pendingProducts(); track product.productId) {
              <div class="admin-table-row" role="row">
                <span role="cell">
                  <strong>{{ product.title ?? 'Untitled product' }}</strong>
                  <small>{{ product.categoryPath ?? 'No category' }}</small>
                </span>
                <span role="cell">
                  <strong>{{ product.sellerDisplayName ?? 'Unnamed seller' }}</strong>
                  <small>{{ product.sellerVerificationStatus ?? 'Unknown seller status' }}</small>
                </span>
                <span role="cell">{{ product.updatedAtUtc | date:'medium' }}</span>
                <span role="cell">
                  <span class="status-pill">{{ product.status }}</span>
                  @if (product.highRiskFlagCount > 0) {
                    <small>{{ product.highRiskFlagCount }} high-risk flag{{ product.highRiskFlagCount === 1 ? '' : 's' }}</small>
                  }
                </span>
                <span role="cell">
                  <a mat-stroked-button [routerLink]="['/admin/products', product.productId]">Review</a>
                </span>
              </div>
            }
          </div>
        }
      }
    </section>
  `
})
export class AdminProductsPageComponent implements OnInit {
  private readonly adminProductService = inject(AdminProductService);

  protected readonly pendingProducts = signal<AdminProductSummaryResponse[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    await this.loadPendingProducts();
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
