import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { getApiErrorMessage } from '../auth/api-error';
import { SellerProductSummaryResponse } from '../seller/seller-product.models';
import { SellerProductService } from '../seller/seller-product.service';

@Component({
  selector: 'app-seller-products-page',
  imports: [DatePipe, MatButtonModule, RouterLink],
  template: `
    <section class="page seller-products">
      <div class="page-header seller-products-header">
        <div>
          <span class="eyebrow">Seller catalog</span>
          <h1>Products</h1>
          <p>Manage draft listings, variants, images, and review submission.</p>
        </div>
        <a mat-flat-button routerLink="/seller/products/new">New product</a>
      </div>

      @if (isLoading()) {
        <div class="route-card">Loading products...</div>
      } @else {
        @if (errorMessage()) {
          <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
        }

        @if (products().length === 0 && !errorMessage()) {
          <div class="route-card">
            <span class="status-pill">Drafts</span>
            <h2>No products yet</h2>
            <p>Create your first product draft before adding images and variants.</p>
          </div>
        } @else {
          <div class="admin-table" role="table" aria-label="Seller products">
            <div class="admin-table-row heading" role="row">
              <span role="columnheader">Product</span>
              <span role="columnheader">Slug</span>
              <span role="columnheader">Updated</span>
              <span role="columnheader">Status</span>
              <span role="columnheader">Action</span>
            </div>

            @for (product of products(); track product.productId) {
              <div class="admin-table-row" role="row">
                <span role="cell">
                  <strong>{{ product.title ?? 'Untitled product' }}</strong>
                  <small>{{ product.productId }}</small>
                </span>
                <span role="cell">{{ product.slug ?? 'No slug' }}</span>
                <span role="cell">{{ product.updatedAtUtc | date:'medium' }}</span>
                <span role="cell"><span class="status-pill">{{ product.status }}</span></span>
                <span role="cell">
                  <a mat-stroked-button [routerLink]="['/seller/products', product.productId, 'edit']">Edit</a>
                </span>
              </div>
            }
          </div>
        }
      }
    </section>
  `
})
export class SellerProductsPageComponent implements OnInit {
  private readonly productService = inject(SellerProductService);

  protected readonly products = signal<SellerProductSummaryResponse[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    await this.loadProducts();
  }

  private async loadProducts(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.products.set(await this.productService.listProducts());
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }
}
