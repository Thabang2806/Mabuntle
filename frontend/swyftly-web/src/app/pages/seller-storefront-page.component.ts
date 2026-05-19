import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { getApiErrorMessage } from '../auth/api-error';
import { ProductCardComponent } from '../shop/product-card.component';
import { PublicSellerStorefrontResponse } from '../shop/public-catalog.models';
import { PublicCatalogService } from '../shop/public-catalog.service';

@Component({
  selector: 'app-seller-storefront-page',
  imports: [MatButtonModule, ProductCardComponent, RouterLink],
  template: `
    <section class="page shop-surface">
      <a class="admin-back-link" routerLink="/shop">Back to shop</a>

      @if (isLoading()) {
        <div class="route-card">Loading seller storefront...</div>
      } @else if (storefront()) {
        <div class="seller-storefront-hero">
          @if (storefront()?.bannerUrl) {
            <img [src]="storefront()?.bannerUrl" [alt]="storefront()?.storeName ?? 'Seller banner'">
          }
          <div>
            <span class="eyebrow">Seller storefront</span>
            <h1>{{ storefront()?.storeName }}</h1>
            <p>{{ storefront()?.description ?? 'Published products from this Swyftly seller.' }}</p>
          </div>
        </div>

        @if ((storefront()?.products?.length ?? 0) === 0) {
          <div class="route-card">
            <span class="status-pill">Empty</span>
            <h2>No published products</h2>
            <p>This seller does not have public products yet.</p>
          </div>
        } @else {
          <div class="product-grid">
            @for (product of storefront()?.products; track product.productId) {
              <app-product-card [product]="product"></app-product-card>
            }
          </div>
        }
      } @else {
        <p class="auth-alert error" role="alert">{{ errorMessage() ?? 'Seller storefront was not found.' }}</p>
      }
    </section>
  `
})
export class SellerStorefrontPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly publicCatalogService = inject(PublicCatalogService);

  protected readonly storefront = signal<PublicSellerStorefrontResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    const storeSlug = this.route.snapshot.paramMap.get('storeSlug');
    if (!storeSlug) {
      this.errorMessage.set('Seller slug is missing.');
      this.isLoading.set(false);
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.storefront.set(await this.publicCatalogService.getSellerStorefront(storeSlug));
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.storefront.set(null);
    } finally {
      this.isLoading.set(false);
    }
  }
}
