import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { getApiErrorMessage } from '../auth/api-error';
import { BuyerWishlistItemResponse } from '../buyer/buyer-engagement.models';
import { BuyerEngagementService } from '../buyer/buyer-engagement.service';
import { BuyerWorkspaceNavComponent } from '../buyer/buyer-workspace-nav.component';
import { ProductCardComponent } from '../shop/product-card.component';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-buyer-wishlist-page',
  imports: [
    BuyerWorkspaceNavComponent,
    DatePipe,
    EmptyStateComponent,
    MatButtonModule,
    PageHeaderComponent,
    ProductCardComponent,
    RouterLink,
    UiAlertComponent
  ],
  template: `
    <section class="page buyer-ops-page">
      <app-buyer-workspace-nav />

      <app-page-header
        eyebrow="Buyer account"
        heading="Wishlist"
        description="Keep track of published marketplace products you want to revisit."
      >
        <div pageHeaderActions>
          <a mat-stroked-button routerLink="/shop">Find more products</a>
        </div>
      </app-page-header>

      @if (isLoading()) {
        <div class="route-card">Loading wishlist...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (successMessage()) {
          <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
        }

        @if (wishlist().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Wishlist"
            heading="No saved products yet"
            message="Save products from listing cards or product detail pages to compare them later."
          >
            <a mat-flat-button routerLink="/shop">Browse marketplace</a>
          </app-empty-state>
        } @else {
          <div class="wishlist-grid">
            @for (item of wishlist(); track item.wishlistItemId) {
              <article class="wishlist-item">
                <app-product-card [product]="item.product" wishlistAction="hidden" />
                <div class="wishlist-item-footer">
                  <small>Saved {{ item.createdAtUtc | date:'mediumDate' }}</small>
                  <button
                    mat-stroked-button
                    type="button"
                    [disabled]="removingProductId() === item.product.productId"
                    (click)="remove(item)"
                  >
                    {{ removingProductId() === item.product.productId ? 'Removing...' : 'Remove' }}
                  </button>
                </div>
              </article>
            }
          </div>
        }
      }
    </section>
  `
})
export class BuyerWishlistPageComponent implements OnInit {
  private readonly engagementService = inject(BuyerEngagementService);

  protected readonly wishlist = signal<BuyerWishlistItemResponse[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly removingProductId = signal<string | null>(null);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  async ngOnInit(): Promise<void> {
    await this.loadWishlist();
  }

  protected async remove(item: BuyerWishlistItemResponse): Promise<void> {
    if (this.removingProductId()) {
      return;
    }

    this.removingProductId.set(item.product.productId);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      await this.engagementService.removeWishlistItem(item.product.productId);
      this.wishlist.set(this.wishlist().filter(existing => existing.product.productId !== item.product.productId));
      this.successMessage.set('Removed from wishlist.');
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.removingProductId.set(null);
    }
  }

  private async loadWishlist(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.wishlist.set(await this.engagementService.listWishlist());
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }
}
