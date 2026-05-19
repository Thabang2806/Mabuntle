import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { getApiErrorMessage } from '../auth/api-error';
import { ProductCardComponent } from '../shop/product-card.component';
import { ProductSearchItemResponse, PublicCategoryResponse } from '../shop/public-catalog.models';
import { PublicCatalogService } from '../shop/public-catalog.service';

@Component({
  selector: 'app-category-page',
  imports: [MatButtonModule, ProductCardComponent, RouterLink],
  template: `
    <section class="page shop-surface">
      <a class="admin-back-link" routerLink="/shop">Back to shop</a>

      <div class="page-header">
        <span class="eyebrow">Category</span>
        <h1>{{ category()?.name ?? 'Category' }}</h1>
        <p>{{ categoryPath() }}</p>
      </div>

      @if (isLoading()) {
        <div class="route-card">Loading category products...</div>
      } @else {
        @if (errorMessage()) {
          <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
        }

        @if (products().length === 0 && !errorMessage()) {
          <div class="route-card">
            <span class="status-pill">Empty</span>
            <h2>No products in this category</h2>
            <p>New published products will appear here after review.</p>
          </div>
        } @else {
          <div class="product-grid">
            @for (product of products(); track product.productId) {
              <app-product-card [product]="product"></app-product-card>
            }
          </div>
        }
      }
    </section>
  `
})
export class CategoryPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly publicCatalogService = inject(PublicCatalogService);

  protected readonly categories = signal<PublicCategoryResponse[]>([]);
  protected readonly products = signal<ProductSearchItemResponse[]>([]);
  protected readonly category = signal<PublicCategoryResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly categoryPath = computed(() => {
    const selected = this.category();
    if (!selected) {
      return 'Published products by category.';
    }

    const byId = new Map(this.categories().map(category => [category.categoryId, category]));
    const names: string[] = [];
    let current: PublicCategoryResponse | undefined = selected;
    while (current) {
      names.unshift(current.name);
      current = current.parentCategoryId ? byId.get(current.parentCategoryId) : undefined;
    }

    return names.join(' > ');
  });

  async ngOnInit(): Promise<void> {
    const slug = this.route.snapshot.paramMap.get('slug');
    if (!slug) {
      this.errorMessage.set('Category slug is missing.');
      this.isLoading.set(false);
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const categories = await this.publicCatalogService.getCategories();
      this.categories.set(categories);
      this.category.set(categories.find(category => category.slug === slug) ?? null);
      const response = await this.publicCatalogService.searchProducts({
        categorySlug: slug,
        pageSize: 24
      });
      this.products.set(response.items);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.products.set([]);
    } finally {
      this.isLoading.set(false);
    }
  }
}
