import { CurrencyPipe } from '@angular/common';
import { Component, Injector, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { getApiErrorMessage } from '../auth/api-error';
import { AuthService } from '../auth/auth.service';
import { BuyerEngagementService } from '../buyer/buyer-engagement.service';
import { StatusBadgeComponent } from '../shared/ui/status-badge.component';
import { ProductSearchItemResponse } from './public-catalog.models';

@Component({
  selector: 'app-product-card',
  imports: [CurrencyPipe, MatButtonModule, RouterLink, StatusBadgeComponent],
  template: `
    <article class="product-card">
      <a class="product-card-media" [routerLink]="['/product', product().slug]">
        @if (product().primaryImageUrl) {
          <img [src]="product().primaryImageUrl" [alt]="product().primaryImageAltText ?? product().title ?? 'Product image'" loading="lazy">
        } @else {
          <div class="product-card-fallback">
            <span>{{ product().categoryPath ?? 'Swyftly edit' }}</span>
            <strong>{{ product().title ?? 'Product' }}</strong>
          </div>
        }
      </a>

      <div class="product-card-body">
        <div class="product-card-heading">
          <app-status-badge [label]="product().categoryPath ?? 'Marketplace'" tone="accent" />
          <a class="product-card-title" [routerLink]="['/product', product().slug]">{{ product().title ?? 'Untitled product' }}</a>
          @if (product().sellerStoreSlug) {
            <a class="product-card-seller" [routerLink]="['/seller', product().sellerStoreSlug]">{{ product().sellerStoreName ?? 'Seller' }}</a>
          } @else {
            <span class="product-card-seller">{{ product().sellerStoreName ?? 'Seller' }}</span>
          }
        </div>

        @if (product().shortDescription) {
          <p class="product-card-description">{{ product().shortDescription }}</p>
        }

        <div class="product-card-price">
          <strong>{{ product().priceMin | currency:'ZAR':'symbol-narrow' }}</strong>
          @if (product().compareAtPriceMin) {
            <span>{{ product().compareAtPriceMin | currency:'ZAR':'symbol-narrow' }}</span>
          }
        </div>

        <div class="product-card-meta">
          <app-status-badge [label]="product().inStock ? 'In stock' : 'Out of stock'" [tone]="product().inStock ? 'success' : 'warning'" />
          <span>{{ product().publishedAtUtc ? 'Recently listed' : 'Marketplace item' }}</span>
        </div>

        <div class="product-card-actions">
          <a mat-stroked-button [routerLink]="['/product', product().slug]">View details</a>
          @if (wishlistAction() !== 'hidden') {
            <button
              mat-button
              type="button"
              [disabled]="isSavingWishlist() || isWishlisted()"
              [attr.aria-label]="isWishlisted() ? 'Product saved to wishlist' : 'Save product to wishlist'"
              (click)="saveToWishlist()"
            >
              {{ isSavingWishlist() ? 'Saving...' : isWishlisted() ? 'Saved' : 'Save' }}
            </button>
          }
        </div>

        @if (wishlistError()) {
          <small class="product-card-feedback">{{ wishlistError() }}</small>
        }
      </div>
    </article>
  `
})
export class ProductCardComponent {
  private readonly injector = inject(Injector);

  readonly product = input.required<ProductSearchItemResponse>();
  readonly wishlistAction = input<'save' | 'hidden'>('save');

  protected readonly isSavingWishlist = signal(false);
  protected readonly isWishlisted = signal(false);
  protected readonly wishlistError = signal<string | null>(null);

  protected async saveToWishlist(): Promise<void> {
    this.wishlistError.set(null);

    const authService = this.injector.get(AuthService);
    const router = this.injector.get(Router);

    await authService.initialize();
    if (!authService.hasAnyRole(['Buyer'])) {
      await router.navigate(['/login'], {
        queryParams: { returnUrl: router.url }
      });
      return;
    }

    if (this.isSavingWishlist() || this.isWishlisted()) {
      return;
    }

    this.isSavingWishlist.set(true);
    try {
      await this.injector.get(BuyerEngagementService).addWishlistItem(this.product().productId);
      this.isWishlisted.set(true);
    } catch (error) {
      this.wishlistError.set(getApiErrorMessage(error));
    } finally {
      this.isSavingWishlist.set(false);
    }
  }
}
