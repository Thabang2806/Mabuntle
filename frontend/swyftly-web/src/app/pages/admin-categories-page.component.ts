import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { AdminCategoryResponse } from '../admin/admin-category.models';
import { AdminCategoryService } from '../admin/admin-category.service';
import { getApiErrorMessage } from '../auth/api-error';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-admin-categories-page',
  imports: [
    EmptyStateComponent,
    MatButtonModule,
    PageHeaderComponent,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page admin-support-page">
      <a class="admin-back-link" routerLink="/admin">Back to dashboard</a>

      <app-page-header
        eyebrow="Catalog operations"
        heading="Categories and attributes"
        description="Read the active catalog taxonomy and category attribute definitions used by seller product forms."
      >
        <div pageHeaderActions>
          <a mat-stroked-button routerLink="/admin/products">Product review</a>
        </div>
      </app-page-header>

      <app-ui-alert tone="info">
        Category and attribute write APIs are not available yet. This page is intentionally read-only until create, edit, reorder, and deactivation endpoints exist.
      </app-ui-alert>

      @if (isLoading()) {
        <div class="route-card">Loading categories...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (categories().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Catalog"
            heading="No categories found"
            message="Seeded category metadata will appear here once it exists in the database."
          />
        } @else {
          <div class="admin-category-grid">
            @for (category of rootCategories(); track category.categoryId) {
              <article class="admin-category-card">
                <div class="admin-category-header">
                  <div>
                    <h2>{{ category.name }}</h2>
                    <p>{{ category.slug }}</p>
                  </div>
                  <app-status-badge [label]="category.isActive ? 'Active' : 'Inactive'" [tone]="category.isActive ? 'success' : 'neutral'" />
                </div>

                <dl class="seller-facts">
                  <div><dt>Display order</dt><dd>{{ category.displayOrder }}</dd></div>
                  <div><dt>Attributes</dt><dd>{{ category.attributes.length }}</dd></div>
                  <div><dt>Subcategories</dt><dd>{{ childCategories(category.categoryId).length }}</dd></div>
                </dl>

                @if (category.attributes.length > 0) {
                  <div class="admin-category-attributes">
                    @for (attribute of category.attributes; track attribute.attributeId) {
                      <span>{{ attribute.name }}{{ attribute.isRequired ? ' *' : '' }}</span>
                    }
                  </div>
                }

                @if (childCategories(category.categoryId).length > 0) {
                  <div class="admin-category-children">
                    @for (child of childCategories(category.categoryId); track child.categoryId) {
                      <div>
                        <strong>{{ child.name }}</strong>
                        <span>{{ child.attributes.length }} attribute{{ child.attributes.length === 1 ? '' : 's' }}</span>
                      </div>
                    }
                  </div>
                }
              </article>
            }
          </div>
        }
      }
    </section>
  `
})
export class AdminCategoriesPageComponent implements OnInit {
  private readonly categoryService = inject(AdminCategoryService);

  protected readonly categories = signal<AdminCategoryResponse[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly rootCategories = computed(() =>
    this.categories().filter(category => category.parentCategoryId === null));

  async ngOnInit(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.categories.set(await this.categoryService.listCategories());
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.categories.set([]);
    } finally {
      this.isLoading.set(false);
    }
  }

  protected childCategories(parentCategoryId: string): AdminCategoryResponse[] {
    return this.categories().filter(category => category.parentCategoryId === parentCategoryId);
  }
}
