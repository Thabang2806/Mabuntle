import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { getApiErrorMessage } from '../auth/api-error';
import { ProductCardComponent } from '../shop/product-card.component';
import { ProductSearchItemResponse } from '../shop/public-catalog.models';
import { PublicCatalogService } from '../shop/public-catalog.service';

@Component({
  selector: 'app-shop-page',
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    ProductCardComponent,
    ReactiveFormsModule
  ],
  template: `
    <section class="page shop-surface">
      <div class="page-header">
        <span class="eyebrow">Shop</span>
        <h1>Shop</h1>
        <p>Browse published fashion, beauty, jewellery, and accessories from verified sellers.</p>
      </div>

      <div class="shop-layout">
        <form [formGroup]="filtersForm" (ngSubmit)="search(1)" class="shop-filters" novalidate>
          <mat-form-field appearance="outline">
            <mat-label>Search</mat-label>
            <input matInput formControlName="query">
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Min price</mat-label>
            <input matInput type="number" min="0" formControlName="minPrice">
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Max price</mat-label>
            <input matInput type="number" min="0" formControlName="maxPrice">
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Size</mat-label>
            <input matInput formControlName="size">
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Colour</mat-label>
            <input matInput formControlName="colour">
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Material</mat-label>
            <input matInput formControlName="material">
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Sort</mat-label>
            <mat-select formControlName="sort">
              <mat-option value="newest">Newest</mat-option>
              <mat-option value="price_asc">Price low to high</mat-option>
              <mat-option value="price_desc">Price high to low</mat-option>
              <mat-option value="relevance">Relevance</mat-option>
            </mat-select>
          </mat-form-field>

          <div class="shop-filter-actions">
            <button mat-flat-button type="submit" [disabled]="isLoading()">Apply</button>
            <button mat-stroked-button type="button" [disabled]="isLoading()" (click)="clearFilters()">Clear</button>
          </div>
        </form>

        <div class="shop-results">
          @if (isLoading()) {
            <div class="route-card">Loading products...</div>
          } @else {
            @if (errorMessage()) {
              <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
            }

            @if (products().length === 0 && !errorMessage()) {
              <div class="route-card">
                <span class="status-pill">Empty</span>
                <h2>No products found</h2>
                <p>Try a broader search or fewer filters.</p>
              </div>
            } @else {
              <div class="shop-result-bar">
                <span>{{ totalCount() }} result{{ totalCount() === 1 ? '' : 's' }}</span>
                <span>Page {{ page() }}</span>
              </div>

              <div class="product-grid">
                @for (product of products(); track product.productId) {
                  <app-product-card [product]="product"></app-product-card>
                }
              </div>

              <div class="shop-pagination">
                <button mat-stroked-button type="button" [disabled]="page() <= 1 || isLoading()" (click)="search(page() - 1)">Previous</button>
                <button mat-stroked-button type="button" [disabled]="page() * pageSize() >= totalCount() || isLoading()" (click)="search(page() + 1)">Next</button>
              </div>
            }
          }
        </div>
      </div>
    </section>
  `
})
export class ShopPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly publicCatalogService = inject(PublicCatalogService);

  protected readonly products = signal<ProductSearchItemResponse[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(24);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly filtersForm = this.formBuilder.group({
    query: [''],
    minPrice: [''],
    maxPrice: [''],
    size: [''],
    colour: [''],
    material: [''],
    sort: ['newest']
  });

  async ngOnInit(): Promise<void> {
    await this.search(1);
  }

  protected async search(page: number): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const filters = this.filtersForm.getRawValue();
      const response = await this.publicCatalogService.searchProducts({
        query: filters.query,
        minPrice: this.toNumber(filters.minPrice),
        maxPrice: this.toNumber(filters.maxPrice),
        size: filters.size,
        colour: filters.colour,
        material: filters.material,
        sort: filters.sort,
        page,
        pageSize: this.pageSize()
      });

      this.products.set(response.items);
      this.totalCount.set(response.totalCount);
      this.page.set(response.page);
      this.pageSize.set(response.pageSize);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.products.set([]);
      this.totalCount.set(0);
    } finally {
      this.isLoading.set(false);
    }
  }

  protected async clearFilters(): Promise<void> {
    this.filtersForm.reset({
      query: '',
      minPrice: '',
      maxPrice: '',
      size: '',
      colour: '',
      material: '',
      sort: 'newest'
    });
    await this.search(1);
  }

  private toNumber(value: string): number | null {
    if (!value) {
      return null;
    }

    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }
}
