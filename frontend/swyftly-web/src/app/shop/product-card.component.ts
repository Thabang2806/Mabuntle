import { CurrencyPipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { ProductSearchItemResponse } from './public-catalog.models';

@Component({
  selector: 'app-product-card',
  imports: [CurrencyPipe, MatButtonModule, RouterLink],
  template: `
    <article class="product-card">
      <a class="product-card-media" [routerLink]="['/product', product().slug]">
        @if (product().primaryImageUrl) {
          <img [src]="product().primaryImageUrl" [alt]="product().primaryImageAltText ?? product().title ?? 'Product image'" loading="lazy">
        } @else {
          <span>{{ product().title ?? 'Product' }}</span>
        }
      </a>

      <div class="product-card-body">
        <div>
          <a class="product-card-title" [routerLink]="['/product', product().slug]">{{ product().title ?? 'Untitled product' }}</a>
          @if (product().sellerStoreSlug) {
            <a class="product-card-seller" [routerLink]="['/seller', product().sellerStoreSlug]">{{ product().sellerStoreName ?? 'Seller' }}</a>
          } @else {
            <span class="product-card-seller">{{ product().sellerStoreName ?? 'Seller' }}</span>
          }
        </div>

        <div class="product-card-price">
          <strong>{{ product().priceMin | currency:'ZAR':'symbol-narrow' }}</strong>
          @if (product().compareAtPriceMin) {
            <span>{{ product().compareAtPriceMin | currency:'ZAR':'symbol-narrow' }}</span>
          }
        </div>

        <div class="product-card-meta">
          <span class="status-pill">{{ product().inStock ? 'In stock' : 'Out of stock' }}</span>
          <span>Rating pending</span>
          <button mat-icon-button type="button" aria-label="Wishlist placeholder">
            <span aria-hidden="true">♡</span>
          </button>
        </div>
      </div>
    </article>
  `
})
export class ProductCardComponent {
  readonly product = input.required<ProductSearchItemResponse>();
}
