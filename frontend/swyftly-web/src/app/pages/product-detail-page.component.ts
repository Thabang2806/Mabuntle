import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { getApiErrorMessage } from '../auth/api-error';
import { AuthService } from '../auth/auth.service';
import { CartService } from '../cart/cart.service';
import { PublicProductDetailResponse } from '../shop/public-catalog.models';
import { PublicCatalogService } from '../shop/public-catalog.service';

@Component({
  selector: 'app-product-detail-page',
  imports: [CurrencyPipe, FormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, RouterLink],
  template: `
    <section class="page product-detail-page">
      <a class="admin-back-link" routerLink="/shop">Back to shop</a>

      @if (isLoading()) {
        <div class="route-card">Loading product...</div>
      } @else if (productDetail()) {
        <div class="product-detail-layout">
          <div class="product-gallery">
            @if (primaryImageUrl()) {
              <img [src]="primaryImageUrl()" [alt]="productDetail()?.product?.primaryImageAltText ?? productDetail()?.product?.title ?? 'Product image'">
            } @else {
              <div class="product-gallery-placeholder">{{ productDetail()?.product?.title ?? 'Product' }}</div>
            }
          </div>

          <div class="product-detail-info">
            <span class="eyebrow">{{ productDetail()?.product?.categoryPath ?? 'Swyftly product' }}</span>
            <h1>{{ productDetail()?.product?.title ?? 'Untitled product' }}</h1>

            @if (productDetail()?.product?.sellerStoreSlug) {
              <a class="product-card-seller" [routerLink]="['/seller', productDetail()?.product?.sellerStoreSlug]">
                {{ productDetail()?.product?.sellerStoreName ?? 'Seller' }}
              </a>
            }

            <div class="product-detail-price">
              <strong>{{ productDetail()?.product?.priceMin | currency:'ZAR':'symbol-narrow' }}</strong>
              @if (productDetail()?.product?.compareAtPriceMin) {
                <span>{{ productDetail()?.product?.compareAtPriceMin | currency:'ZAR':'symbol-narrow' }}</span>
              }
            </div>

            <p>{{ productDetail()?.fullDescription ?? productDetail()?.product?.shortDescription }}</p>

            @if (addToCartMessage()) {
              <p class="auth-alert success" role="status">{{ addToCartMessage() }}</p>
            }

            @if (addToCartError()) {
              <p class="auth-alert error" role="alert">{{ addToCartError() }}</p>
            }

            <div class="product-detail-actions product-purchase-actions">
              <mat-form-field appearance="outline">
                <mat-label>Variant</mat-label>
                <mat-select [(ngModel)]="selectedVariantId">
                  @for (variant of productDetail()?.variants; track variant.variantId) {
                    <mat-option [value]="variant.variantId" [disabled]="!variant.inStock">
                      {{ variant.size }} / {{ variant.colour }} · {{ variant.price | currency:'ZAR':'symbol-narrow' }}
                    </mat-option>
                  }
                </mat-select>
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Qty</mat-label>
                <input matInput type="number" min="1" [(ngModel)]="quantity">
              </mat-form-field>

              <button mat-flat-button type="button" [disabled]="isAddingToCart() || !selectedVariantId" (click)="addToCart()">
                {{ isAddingToCart() ? 'Adding...' : 'Add to cart' }}
              </button>
              <button mat-stroked-button type="button" disabled>Wishlist</button>
            </div>

            <section>
              <h2>Variants</h2>
              <div class="product-option-list">
                @for (variant of productDetail()?.variants; track variant.variantId) {
                  <span>{{ variant.size }} / {{ variant.colour }} · {{ variant.price | currency:'ZAR':'symbol-narrow' }} · {{ variant.inStock ? 'In stock' : 'Out of stock' }}</span>
                }
              </div>
            </section>

            <section>
              <h2>Details</h2>
              <dl class="admin-facts">
                @for (attribute of attributeEntries(); track attribute.key) {
                  <div>
                    <dt>{{ attribute.key }}</dt>
                    <dd>{{ attribute.value }}</dd>
                  </div>
                }
              </dl>
            </section>
          </div>
        </div>
      } @else {
        <p class="auth-alert error" role="alert">{{ errorMessage() ?? 'Product was not found.' }}</p>
      }
    </section>
  `
})
export class ProductDetailPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly authService = inject(AuthService);
  private readonly cartService = inject(CartService);
  private readonly publicCatalogService = inject(PublicCatalogService);
  private readonly router = inject(Router);

  protected readonly productDetail = signal<PublicProductDetailResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly isAddingToCart = signal(false);
  protected readonly addToCartMessage = signal<string | null>(null);
  protected readonly addToCartError = signal<string | null>(null);
  protected selectedVariantId = '';
  protected quantity = 1;
  protected readonly primaryImageUrl = computed(() =>
    this.productDetail()?.images.find(image => image.isPrimary)?.url ??
    this.productDetail()?.images[0]?.url ??
    this.productDetail()?.product.primaryImageUrl ??
    null);
  protected readonly attributeEntries = computed(() => {
    const attributes = this.productDetail()?.attributes ?? {};
    return Object.entries(attributes).map(([key, value]) => ({
      key,
      value: this.formatAttributeValue(value)
    }));
  });

  async ngOnInit(): Promise<void> {
    const slug = this.route.snapshot.paramMap.get('slug');
    if (!slug) {
      this.errorMessage.set('Product slug is missing.');
      this.isLoading.set(false);
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const detail = await this.publicCatalogService.getProduct(slug);
      this.productDetail.set(detail);
      this.selectedVariantId = detail.variants.find(variant => variant.inStock)?.variantId ?? '';
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.productDetail.set(null);
    } finally {
      this.isLoading.set(false);
    }
  }

  protected async addToCart(): Promise<void> {
    this.addToCartMessage.set(null);
    this.addToCartError.set(null);

    await this.authService.initialize();
    if (!this.authService.hasAnyRole(['Buyer'])) {
      await this.router.navigate(['/login'], {
        queryParams: { returnUrl: this.router.url }
      });
      return;
    }

    if (!this.selectedVariantId || this.quantity <= 0) {
      this.addToCartError.set('Choose an available variant and quantity.');
      return;
    }

    this.isAddingToCart.set(true);
    try {
      await this.cartService.addItem({
        productVariantId: this.selectedVariantId,
        quantity: this.quantity
      });
      this.addToCartMessage.set('Added to cart.');
    } catch (error) {
      this.addToCartError.set(getApiErrorMessage(error));
    } finally {
      this.isAddingToCart.set(false);
    }
  }

  private formatAttributeValue(valueJson: string): string {
    try {
      const parsed = JSON.parse(valueJson) as unknown;
      if (Array.isArray(parsed)) {
        return parsed.join(', ');
      }

      return String(parsed ?? '');
    } catch {
      return valueJson;
    }
  }
}
