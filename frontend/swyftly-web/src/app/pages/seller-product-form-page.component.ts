import { Component, OnInit, computed, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  FormRecord,
  FormsModule,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { getApiErrorMessage } from '../auth/api-error';
import {
  ApplySellerAiSuggestionRequest,
  AttachSellerProductImageRequest,
  GenerateSellerAiSuggestionRequest,
  SellerAiSuggestionResponse,
  SellerCatalogCategoryAttributeResponse,
  SellerCatalogCategoryResponse,
  SellerProductDetailResponse,
  UpsertSellerProductRequest,
  UpsertSellerProductVariantRequest
} from '../seller/seller-product.models';
import { SellerProductService } from '../seller/seller-product.service';

type ProductStep = 0 | 1 | 2 | 3 | 4 | 5;

@Component({
  selector: 'app-seller-product-form-page',
  imports: [
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    FormsModule,
    ReactiveFormsModule,
    RouterLink
  ],
  template: `
    <section class="page product-editor">
      <a class="admin-back-link" routerLink="/seller/products">Back to products</a>

      <div class="page-header">
        <span class="eyebrow">Product editor</span>
        <h1>{{ product()?.title ?? 'New product' }}</h1>
        <p>{{ product()?.status ?? 'Draft' }}</p>
      </div>

      @if (isLoading()) {
        <div class="route-card">Loading product editor...</div>
      } @else {
        @if (errorMessage()) {
          <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
        }

        @if (successMessage()) {
          <p class="auth-alert success" role="status">{{ successMessage() }}</p>
        }

        <section class="ai-assistant-panel" aria-labelledby="ai-product-assistant-title">
          <div class="ai-assistant-header">
            <div>
              <span class="ai-badge">AI</span>
              <h2 id="ai-product-assistant-title">AI Product Assistant</h2>
            </div>
            <span class="ai-quality">{{ aiSuggestion()?.qualityScore ?? 0 }} / 100</span>
          </div>

          <form [formGroup]="aiForm" (ngSubmit)="generateAiSuggestion()" class="ai-form" novalidate>
            <mat-form-field appearance="outline">
              <mat-label>Seller notes</mat-label>
              <textarea matInput rows="3" formControlName="sellerNotes"></textarea>
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Product type hint</mat-label>
              <input matInput formControlName="productTypeHint" />
            </mat-form-field>
            <button mat-flat-button type="submit" [disabled]="isAiGenerating() || isSaving()">
              {{ isAiGenerating() ? 'Generating...' : 'Generate with AI' }}
            </button>
          </form>

          <p class="ai-disclaimer">AI suggestions are drafts. Please review and confirm all product details before publishing.</p>

          @if (aiErrorMessage()) {
            <p class="auth-alert error" role="alert">{{ aiErrorMessage() }}</p>
          }

          @if (aiSuggestion(); as suggestion) {
            <div class="ai-suggestion-grid">
              <article class="ai-suggestion-card">
                <span class="status-pill">Title</span>
                <h2>{{ suggestion.recommendedTitle ?? 'No title suggested' }}</h2>
                @if (suggestion.titleSuggestions.length > 1) {
                  <p>{{ suggestion.titleSuggestions.join(' / ') }}</p>
                }
              </article>
              <article class="ai-suggestion-card">
                <span class="status-pill">Descriptions</span>
                <p>{{ suggestion.shortDescription ?? 'No short description suggested' }}</p>
                <p>{{ suggestion.fullDescription ?? 'No full description suggested' }}</p>
              </article>
              <article class="ai-suggestion-card">
                <span class="status-pill">Category</span>
                <h2>{{ suggestion.suggestedCategoryPath ?? categoryName(suggestion.suggestedCategoryId) ?? 'No category suggested' }}</h2>
              </article>
              <article class="ai-suggestion-card">
                <span class="status-pill">Attributes</span>
                @for (attribute of aiAttributeEntries(); track attribute.key) {
                  <p><strong>{{ attribute.key }}</strong>: {{ attribute.value }}</p>
                } @empty {
                  <p>No attributes suggested.</p>
                }
              </article>
              <article class="ai-suggestion-card">
                <span class="status-pill">Tags</span>
                <p>{{ suggestion.tags.length ? suggestion.tags.join(', ') : 'No tags suggested.' }}</p>
              </article>
              <article class="ai-suggestion-card">
                <span class="status-pill">Missing fields</span>
                @for (field of suggestion.missingFields; track field) {
                  <p>{{ field }}</p>
                } @empty {
                  <p>No missing fields returned.</p>
                }
              </article>
            </div>

            @if (suggestion.riskFlags.length > 0) {
              <div class="ai-risk-box" role="alert">
                <strong>Risk flags</strong>
                @for (flag of suggestion.riskFlags; track flag) {
                  <p>{{ flag }}</p>
                }
              </div>
            }

            <form [formGroup]="aiApplyForm" (ngSubmit)="applyAiSuggestion()" class="ai-apply-form" novalidate>
              <div class="ai-field-grid">
                <mat-checkbox formControlName="title">Title</mat-checkbox>
                <mat-checkbox formControlName="shortDescription">Short description</mat-checkbox>
                <mat-checkbox formControlName="fullDescription">Full description</mat-checkbox>
                <mat-checkbox formControlName="category">Category</mat-checkbox>
                <mat-checkbox formControlName="attributes">Attributes</mat-checkbox>
                <mat-checkbox formControlName="tags">Tags</mat-checkbox>
                <mat-checkbox formControlName="imageAltText">Image alt text</mat-checkbox>
              </div>

              <div [formGroup]="aiEditForm" class="ai-edit-grid">
                <mat-form-field appearance="outline">
                  <mat-label>Reviewed title</mat-label>
                  <input matInput formControlName="title" />
                </mat-form-field>
                <mat-form-field appearance="outline">
                  <mat-label>Reviewed short description</mat-label>
                  <textarea matInput rows="2" formControlName="shortDescription"></textarea>
                </mat-form-field>
                <mat-form-field appearance="outline">
                  <mat-label>Reviewed full description</mat-label>
                  <textarea matInput rows="4" formControlName="fullDescription"></textarea>
                </mat-form-field>
                <mat-form-field appearance="outline">
                  <mat-label>Reviewed category</mat-label>
                  <mat-select formControlName="suggestedCategoryId">
                    @for (category of categories(); track category.categoryId) {
                      <mat-option [value]="category.categoryId">{{ categoryLabel(category) }}</mat-option>
                    }
                  </mat-select>
                </mat-form-field>
                <mat-form-field appearance="outline">
                  <mat-label>Reviewed tags</mat-label>
                  <input matInput formControlName="tags" />
                </mat-form-field>
              </div>

              @if (aiAttributeEntries().length > 0) {
                <div [formGroup]="aiAttributeEditForm" class="dynamic-attributes">
                  @for (attribute of aiAttributeEntries(); track attribute.key) {
                    <mat-form-field appearance="outline">
                      <mat-label>{{ attribute.key }}</mat-label>
                      <input matInput [formControlName]="attribute.key" />
                    </mat-form-field>
                  }
                </div>
              }

              @if ((product()?.images?.length ?? 0) > 0) {
                <div [formGroup]="aiImageAltTextForm" class="dynamic-attributes">
                  @for (image of product()?.images ?? []; track image.imageId) {
                    <mat-form-field appearance="outline">
                      <mat-label>Alt text for {{ image.storageKey }}</mat-label>
                      <input matInput [formControlName]="image.imageId" />
                    </mat-form-field>
                  }
                </div>
              }

              @if (suggestion.riskFlags.length > 0) {
                <mat-checkbox formControlName="confirmRiskFlags">Confirm risk flags</mat-checkbox>
              }

              <button mat-flat-button type="submit" [disabled]="isAiApplying()">
                {{ isAiApplying() ? 'Applying...' : 'Apply selected suggestions' }}
              </button>
            </form>
          }
        </section>

        <div class="wizard-layout">
          <nav class="wizard-steps" aria-label="Product form sections">
            @for (step of steps; track step.index) {
              <button
                type="button"
                [class.active]="currentStep() === step.index"
                [class.complete]="isStepComplete(step.index)"
                (click)="currentStep.set(step.index)"
              >
                <span>{{ step.index + 1 }}</span>
                {{ step.label }}
              </button>
            }
          </nav>

          <div class="wizard-panel">
            @switch (currentStep()) {
              @case (0) {
                <form [formGroup]="basicForm" (ngSubmit)="saveDraft()" class="wizard-form" novalidate>
                  <h2>Basic details</h2>
                  <mat-form-field appearance="outline">
                    <mat-label>Title</mat-label>
                    <input matInput formControlName="title" />
                    @if (basicForm.controls.title.hasError('required')) {
                      <mat-error>Title is required.</mat-error>
                    }
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Slug</mat-label>
                    <input matInput formControlName="slug" />
                    @if (basicForm.controls.slug.hasError('required')) {
                      <mat-error>Slug is required.</mat-error>
                    } @else if (basicForm.controls.slug.hasError('pattern')) {
                      <mat-error>Use lowercase letters, numbers, and hyphens.</mat-error>
                    }
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Short description</mat-label>
                    <textarea matInput rows="3" formControlName="shortDescription"></textarea>
                    @if (basicForm.controls.shortDescription.hasError('required')) {
                      <mat-error>Short description is required.</mat-error>
                    }
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Full description</mat-label>
                    <textarea matInput rows="5" formControlName="fullDescription"></textarea>
                    @if (basicForm.controls.fullDescription.hasError('required')) {
                      <mat-error>Full description is required.</mat-error>
                    }
                  </mat-form-field>

                  <button mat-flat-button type="submit" [disabled]="isSaving()">Save draft</button>
                </form>
              }
              @case (1) {
                <form [formGroup]="basicForm" (ngSubmit)="saveDraft()" class="wizard-form" novalidate>
                  <h2>Category</h2>
                  <mat-form-field appearance="outline">
                    <mat-label>Category</mat-label>
                    <mat-select formControlName="categoryId" (selectionChange)="onCategoryChanged($event.value)">
                      @for (category of categories(); track category.categoryId) {
                        <mat-option [value]="category.categoryId">{{ categoryLabel(category) }}</mat-option>
                      }
                    </mat-select>
                    @if (basicForm.controls.categoryId.hasError('required')) {
                      <mat-error>Category is required.</mat-error>
                    }
                  </mat-form-field>

                  <div [formGroup]="attributeForm" class="dynamic-attributes">
                    @for (attribute of selectedCategory()?.attributes ?? []; track attribute.attributeId) {
                      <mat-form-field appearance="outline">
                        <mat-label>{{ attribute.name }}{{ attribute.isRequired ? ' *' : '' }}</mat-label>
                        @switch (attribute.dataType) {
                          @case ('Select') {
                            <mat-select [formControlName]="attribute.key">
                              @for (value of attribute.allowedValues; track value) {
                                <mat-option [value]="value">{{ value }}</mat-option>
                              }
                            </mat-select>
                          }
                          @case ('MultiSelect') {
                            <mat-select multiple [formControlName]="attribute.key">
                              @for (value of attribute.allowedValues; track value) {
                                <mat-option [value]="value">{{ value }}</mat-option>
                              }
                            </mat-select>
                          }
                          @case ('Boolean') {
                            <mat-select [formControlName]="attribute.key">
                              <mat-option [value]="true">Yes</mat-option>
                              <mat-option [value]="false">No</mat-option>
                            </mat-select>
                          }
                          @case ('Number') {
                            <input matInput type="number" step="1" [formControlName]="attribute.key" />
                          }
                          @case ('Decimal') {
                            <input matInput type="number" step="0.01" [formControlName]="attribute.key" />
                          }
                          @case ('Date') {
                            <input matInput type="date" [formControlName]="attribute.key" />
                          }
                          @default {
                            <input matInput [formControlName]="attribute.key" />
                          }
                        }
                        @if (attributeForm.controls[attribute.key].hasError('required')) {
                          <mat-error>{{ attribute.name }} is required.</mat-error>
                        }
                      </mat-form-field>
                    }
                  </div>

                  <button mat-flat-button type="submit" [disabled]="isSaving()">Save category</button>
                </form>
              }
              @case (2) {
                <form [formGroup]="imageForm" (ngSubmit)="addImage()" class="wizard-form" novalidate>
                  <h2>Images</h2>
                  <mat-form-field appearance="outline">
                    <mat-label>Storage key</mat-label>
                    <input matInput formControlName="storageKey" />
                    @if (imageForm.controls.storageKey.hasError('required')) {
                      <mat-error>Storage key is required.</mat-error>
                    }
                  </mat-form-field>
                  <mat-form-field appearance="outline">
                    <mat-label>Image URL</mat-label>
                    <input matInput formControlName="url" />
                  </mat-form-field>
                  <mat-form-field appearance="outline">
                    <mat-label>Alt text</mat-label>
                    <input matInput formControlName="altText" />
                  </mat-form-field>
                  <div class="form-grid">
                    <mat-form-field appearance="outline">
                      <mat-label>Sort order</mat-label>
                      <input matInput type="number" min="0" formControlName="sortOrder" />
                    </mat-form-field>
                    <mat-form-field appearance="outline">
                      <mat-label>Primary image</mat-label>
                      <mat-select formControlName="isPrimary">
                        <mat-option [value]="true">Yes</mat-option>
                        <mat-option [value]="false">No</mat-option>
                      </mat-select>
                    </mat-form-field>
                  </div>
                  <button mat-flat-button type="submit" [disabled]="isSaving()">Attach image</button>
                </form>

                <div class="product-list-block">
                  @for (image of product()?.images ?? []; track image.imageId) {
                    <article class="route-card compact-card">
                      <span class="status-pill">{{ image.isPrimary ? 'Primary' : 'Image' }}</span>
                      <h2>{{ image.altText ?? image.storageKey }}</h2>
                      <p>{{ image.url }}</p>
                      <button mat-stroked-button type="button" (click)="removeImage(image.imageId)">Remove</button>
                    </article>
                  }
                </div>
              }
              @case (3) {
                <form [formGroup]="variantForm" (ngSubmit)="addVariant()" class="wizard-form" novalidate>
                  <h2>Variants and stock</h2>
                  <div class="form-grid">
                    <mat-form-field appearance="outline">
                      <mat-label>SKU</mat-label>
                      <input matInput formControlName="sku" />
                    </mat-form-field>
                    <mat-form-field appearance="outline">
                      <mat-label>Status</mat-label>
                      <mat-select formControlName="status">
                        <mat-option value="Active">Active</mat-option>
                        <mat-option value="Inactive">Inactive</mat-option>
                        <mat-option value="OutOfStock">Out of stock</mat-option>
                      </mat-select>
                    </mat-form-field>
                  </div>
                  <div class="form-grid">
                    <mat-form-field appearance="outline">
                      <mat-label>Size</mat-label>
                      <input matInput formControlName="size" />
                    </mat-form-field>
                    <mat-form-field appearance="outline">
                      <mat-label>Colour</mat-label>
                      <input matInput formControlName="colour" />
                    </mat-form-field>
                  </div>
                  <div class="form-grid">
                    <mat-form-field appearance="outline">
                      <mat-label>Price</mat-label>
                      <input matInput type="number" step="0.01" formControlName="price" />
                    </mat-form-field>
                    <mat-form-field appearance="outline">
                      <mat-label>Compare-at price</mat-label>
                      <input matInput type="number" step="0.01" formControlName="compareAtPrice" />
                    </mat-form-field>
                  </div>
                  <div class="form-grid">
                    <mat-form-field appearance="outline">
                      <mat-label>Stock quantity</mat-label>
                      <input matInput type="number" min="0" formControlName="stockQuantity" />
                    </mat-form-field>
                    <mat-form-field appearance="outline">
                      <mat-label>Reserved quantity</mat-label>
                      <input matInput type="number" min="0" formControlName="reservedQuantity" />
                    </mat-form-field>
                  </div>
                  <mat-form-field appearance="outline">
                    <mat-label>Barcode</mat-label>
                    <input matInput formControlName="barcode" />
                  </mat-form-field>
                  <button mat-flat-button type="submit" [disabled]="isSaving()">Add variant</button>
                </form>

                <div class="product-list-block">
                  @for (variant of product()?.variants ?? []; track variant.variantId) {
                    <article class="route-card compact-card">
                      <span class="status-pill">{{ variant.status }}</span>
                      <h2>{{ variant.sku }}</h2>
                      <p>{{ variant.size }} / {{ variant.colour }} / {{ variant.price }}</p>
                      <button mat-stroked-button type="button" (click)="removeVariant(variant.variantId)">Remove</button>
                    </article>
                  }
                </div>
              }
              @case (4) {
                <form [formGroup]="shippingForm" class="wizard-form" novalidate>
                  <h2>Shipping and returns</h2>
                  <mat-form-field appearance="outline">
                    <mat-label>Shipping notes</mat-label>
                    <textarea matInput rows="4" formControlName="shippingNotes"></textarea>
                  </mat-form-field>
                  <mat-form-field appearance="outline">
                    <mat-label>Return notes</mat-label>
                    <textarea matInput rows="4" formControlName="returnNotes"></textarea>
                  </mat-form-field>
                </form>
              }
              @case (5) {
                <div class="review-panel">
                  <h2>Review and submit</h2>
                  <div class="review-grid">
                    @for (item of reviewItems(); track item.label) {
                      <article class="route-card compact-card">
                        <span class="status-pill">{{ item.complete ? 'Complete' : 'Missing' }}</span>
                        <h2>{{ item.label }}</h2>
                        <p>{{ item.summary }}</p>
                      </article>
                    }
                  </div>
                  <button mat-flat-button type="button" [disabled]="!canSubmitReview() || isSaving()" (click)="submitForReview()">
                    Submit for review
                  </button>
                </div>
              }
            }
          </div>
        </div>
      }
    </section>
  `
})
export class SellerProductFormPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly productService = inject(SellerProductService);

  protected readonly categories = signal<SellerCatalogCategoryResponse[]>([]);
  protected readonly product = signal<SellerProductDetailResponse | null>(null);
  protected readonly selectedCategoryId = signal<string | null>(null);
  protected readonly currentStep = signal<ProductStep>(0);
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly isAiGenerating = signal(false);
  protected readonly isAiApplying = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly aiErrorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected readonly aiSuggestion = signal<SellerAiSuggestionResponse | null>(null);

  protected readonly steps: readonly { index: ProductStep; label: string }[] = [
    { index: 0, label: 'Details' },
    { index: 1, label: 'Category' },
    { index: 2, label: 'Images' },
    { index: 3, label: 'Variants' },
    { index: 4, label: 'Shipping' },
    { index: 5, label: 'Review' }
  ];

  protected readonly selectedCategory = computed(() =>
    this.categories().find(category => category.categoryId === this.selectedCategoryId()) ?? null);

  protected readonly basicForm = new FormGroup({
    categoryId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    title: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    slug: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(/^[a-z0-9-]+$/)] }),
    shortDescription: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    fullDescription: new FormControl('', { nonNullable: true, validators: [Validators.required] })
  });

  protected readonly attributeForm = new FormRecord<FormControl<unknown>>({});

  protected readonly imageForm = new FormGroup({
    storageKey: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    url: new FormControl('', { nonNullable: true }),
    altText: new FormControl('', { nonNullable: true }),
    sortOrder: new FormControl(0, { nonNullable: true, validators: [Validators.min(0)] }),
    isPrimary: new FormControl(false, { nonNullable: true })
  });

  protected readonly variantForm = new FormGroup({
    sku: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    size: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    colour: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    price: new FormControl(0, { nonNullable: true, validators: [Validators.required, Validators.min(0.01)] }),
    compareAtPrice: new FormControl<number | null>(null),
    stockQuantity: new FormControl(0, { nonNullable: true, validators: [Validators.required, Validators.min(0)] }),
    reservedQuantity: new FormControl(0, { nonNullable: true, validators: [Validators.required, Validators.min(0)] }),
    status: new FormControl<'Active' | 'Inactive' | 'OutOfStock'>('Active', { nonNullable: true }),
    barcode: new FormControl('', { nonNullable: true })
  });

  protected readonly shippingForm = new FormGroup({
    shippingNotes: new FormControl('', { nonNullable: true }),
    returnNotes: new FormControl('', { nonNullable: true })
  });

  protected readonly aiForm = new FormGroup({
    sellerNotes: new FormControl('', { nonNullable: true }),
    productTypeHint: new FormControl('', { nonNullable: true })
  });

  protected readonly aiApplyForm = new FormGroup({
    title: new FormControl(false, { nonNullable: true }),
    shortDescription: new FormControl(false, { nonNullable: true }),
    fullDescription: new FormControl(false, { nonNullable: true }),
    category: new FormControl(false, { nonNullable: true }),
    attributes: new FormControl(false, { nonNullable: true }),
    tags: new FormControl(false, { nonNullable: true }),
    imageAltText: new FormControl(false, { nonNullable: true }),
    confirmRiskFlags: new FormControl(false, { nonNullable: true })
  });

  protected readonly aiEditForm = new FormGroup({
    title: new FormControl('', { nonNullable: true }),
    shortDescription: new FormControl('', { nonNullable: true }),
    fullDescription: new FormControl('', { nonNullable: true }),
    suggestedCategoryId: new FormControl('', { nonNullable: true }),
    tags: new FormControl('', { nonNullable: true })
  });

  protected readonly aiAttributeEditForm = new FormRecord<FormControl<string>>({});
  protected readonly aiImageAltTextForm = new FormRecord<FormControl<string>>({});

  async ngOnInit(): Promise<void> {
    await this.loadEditor();
  }

  protected onCategoryChanged(categoryId: string): void {
    this.selectedCategoryId.set(categoryId);
    this.rebuildAttributeControls({});
  }

  protected categoryLabel(category: SellerCatalogCategoryResponse): string {
    const parent = this.categories().find(item => item.categoryId === category.parentCategoryId);
    return parent ? `${parent.name} / ${category.name}` : category.name;
  }

  protected async saveDraft(): Promise<SellerProductDetailResponse | null> {
    if (!this.ensureValid(this.basicForm) || !this.ensureValid(this.attributeForm)) {
      return null;
    }

    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const request = this.createProductRequest();
      const existing = this.product();
      const saved = existing
        ? await this.productService.updateProduct(existing.productId, request)
        : await this.productService.createProduct(request);

      this.setProduct(saved);
      this.successMessage.set('Product draft saved.');

      if (!existing) {
        await this.router.navigate(['/seller/products', saved.productId, 'edit'], { replaceUrl: true });
      }

      return saved;
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      return null;
    } finally {
      this.isSaving.set(false);
    }
  }

  protected async addImage(): Promise<void> {
    if (!this.ensureValid(this.imageForm)) {
      return;
    }

    const saved = await this.ensureProductSaved();
    if (!saved) {
      return;
    }

    const value = this.imageForm.getRawValue();
    await this.runProductAction(
      () => this.productService.addImage(saved.productId, {
        storageKey: value.storageKey,
        url: emptyToNull(value.url),
        altText: emptyToNull(value.altText),
        sortOrder: Number(value.sortOrder),
        isPrimary: value.isPrimary
      } satisfies AttachSellerProductImageRequest),
      'Image attached.');
    this.imageForm.reset({ storageKey: '', url: '', altText: '', sortOrder: 0, isPrimary: false });
  }

  protected async removeImage(imageId: string): Promise<void> {
    const product = this.product();
    if (!product) {
      return;
    }

    await this.runProductAction(
      () => this.productService.deleteImage(product.productId, imageId),
      'Image removed.');
  }

  protected async addVariant(): Promise<void> {
    if (!this.ensureValid(this.variantForm)) {
      return;
    }

    const saved = await this.ensureProductSaved();
    if (!saved) {
      return;
    }

    const value = this.variantForm.getRawValue();
    await this.runProductAction(
      () => this.productService.addVariant(saved.productId, {
        sku: value.sku,
        size: value.size,
        colour: value.colour,
        price: Number(value.price),
        compareAtPrice: value.compareAtPrice ? Number(value.compareAtPrice) : null,
        stockQuantity: Number(value.stockQuantity),
        reservedQuantity: Number(value.reservedQuantity),
        status: value.status,
        barcode: emptyToNull(value.barcode)
      } satisfies UpsertSellerProductVariantRequest),
      'Variant added.');
    this.variantForm.reset({
      sku: '',
      size: '',
      colour: '',
      price: 0,
      compareAtPrice: null,
      stockQuantity: 0,
      reservedQuantity: 0,
      status: 'Active',
      barcode: ''
    });
  }

  protected async removeVariant(variantId: string): Promise<void> {
    const product = this.product();
    if (!product) {
      return;
    }

    await this.runProductAction(
      () => this.productService.deleteVariant(product.productId, variantId),
      'Variant removed.');
  }

  protected async submitForReview(): Promise<void> {
    const saved = await this.ensureProductSaved();
    if (!saved || !this.canSubmitReview()) {
      return;
    }

    await this.runProductAction(
      () => this.productService.submitForReview(saved.productId),
      'Product submitted for review.');
  }

  protected async generateAiSuggestion(): Promise<void> {
    const saved = await this.ensureProductSaved();
    if (!saved) {
      return;
    }

    this.isAiGenerating.set(true);
    this.aiErrorMessage.set(null);
    this.successMessage.set(null);

    try {
      const value = this.aiForm.getRawValue();
      const request: GenerateSellerAiSuggestionRequest = {
        sellerNotes: emptyToNull(value.sellerNotes),
        productTypeHint: emptyToNull(value.productTypeHint),
        selectedCategoryId: this.basicForm.controls.categoryId.value || null,
        knownAttributes: this.createAttributesRequest(),
        imageIds: saved.images.map(image => image.imageId)
      };
      const suggestion = await this.productService.generateAiSuggestion(saved.productId, request);
      this.aiSuggestion.set(suggestion);
      this.populateAiEditForms(suggestion);
      this.successMessage.set('AI suggestion generated.');
    } catch (error) {
      this.aiErrorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isAiGenerating.set(false);
    }
  }

  protected async applyAiSuggestion(): Promise<void> {
    const product = this.product();
    const suggestion = this.aiSuggestion();
    if (!product || !suggestion) {
      return;
    }

    const fieldsToApply = this.selectedAiFields();
    if (fieldsToApply.length === 0) {
      this.aiErrorMessage.set('Select at least one AI field to apply.');
      return;
    }

    this.isAiApplying.set(true);
    this.aiErrorMessage.set(null);
    this.successMessage.set(null);

    try {
      const updated = await this.productService.applyAiSuggestion(
        product.productId,
        suggestion.suggestionId,
        {
          fieldsToApply,
          editedValues: this.createAiEditedValues(fieldsToApply),
          confirmRiskFlags: this.aiApplyForm.controls.confirmRiskFlags.value
        } satisfies ApplySellerAiSuggestionRequest);

      this.setProduct(updated);
      this.successMessage.set('AI suggestion applied.');
    } catch (error) {
      this.aiErrorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isAiApplying.set(false);
    }
  }

  protected aiAttributeEntries(): readonly { key: string; value: string }[] {
    const suggestion = this.aiSuggestion();
    if (!suggestion) {
      return [];
    }

    return Object.entries(suggestion.attributes)
      .map(([key, value]) => ({ key, value: formatAiValue(value) }));
  }

  protected categoryName(categoryId: string | null): string | null {
    return this.categories().find(category => category.categoryId === categoryId)?.name ?? null;
  }

  protected canSubmitReview(): boolean {
    const product = this.product();
    return this.basicForm.valid
      && this.attributeForm.valid
      && (product?.images.length ?? 0) > 0
      && (product?.variants.some(variant => variant.status === 'Active' && variant.availableQuantity > 0) ?? false);
  }

  protected isStepComplete(step: ProductStep): boolean {
    const product = this.product();
    return [
      this.basicForm.valid,
      this.basicForm.controls.categoryId.valid && this.attributeForm.valid,
      (product?.images.length ?? 0) > 0,
      (product?.variants.length ?? 0) > 0,
      true,
      this.canSubmitReview() || product?.status === 'PendingReview'
    ][step];
  }

  protected reviewItems(): readonly { label: string; complete: boolean; summary: string }[] {
    const product = this.product();
    return [
      { label: 'Details', complete: this.basicForm.valid, summary: this.basicForm.controls.title.value || 'Basic details are required.' },
      { label: 'Category', complete: this.basicForm.controls.categoryId.valid && this.attributeForm.valid, summary: this.selectedCategory()?.name ?? 'Category is required.' },
      { label: 'Images', complete: (product?.images.length ?? 0) > 0, summary: `${product?.images.length ?? 0} attached` },
      { label: 'Variants', complete: product?.variants.some(variant => variant.status === 'Active' && variant.availableQuantity > 0) ?? false, summary: `${product?.variants.length ?? 0} variants` }
    ];
  }

  private async loadEditor(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.categories.set(await this.productService.getCategories());
      const productId = this.route.snapshot.paramMap.get('id');
      if (productId) {
        this.setProduct(await this.productService.getProduct(productId));
      } else {
        this.rebuildAttributeControls({});
      }
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  private setProduct(product: SellerProductDetailResponse): void {
    this.product.set(product);
    this.selectedCategoryId.set(product.categoryId);
    this.basicForm.patchValue({
      categoryId: product.categoryId ?? '',
      title: product.title ?? '',
      slug: product.slug ?? '',
      shortDescription: product.shortDescription ?? '',
      fullDescription: product.fullDescription ?? ''
    });
    this.rebuildAttributeControls(product.attributes);

    const suggestion = this.aiSuggestion();
    if (suggestion) {
      this.rebuildAiImageAltTextControls(suggestion.imageAltText);
    }
  }

  private rebuildAttributeControls(rawAttributes: Record<string, string>): void {
    for (const key of Object.keys(this.attributeForm.controls)) {
      this.attributeForm.removeControl(key);
    }

    for (const attribute of this.selectedCategory()?.attributes ?? []) {
      this.attributeForm.addControl(
        attribute.key,
        new FormControl(parseRawAttributeValue(rawAttributes[attribute.key], attribute), {
          validators: attribute.isRequired ? [Validators.required] : []
        }));
    }
  }

  private createProductRequest(): UpsertSellerProductRequest {
    return {
      categoryId: this.basicForm.controls.categoryId.value,
      brandId: null,
      title: this.basicForm.controls.title.value,
      slug: this.basicForm.controls.slug.value,
      shortDescription: this.basicForm.controls.shortDescription.value,
      fullDescription: this.basicForm.controls.fullDescription.value,
      attributes: this.createAttributesRequest()
    };
  }

  private createAttributesRequest(): Record<string, unknown> {
    const attributes: Record<string, unknown> = {};

    for (const attribute of this.selectedCategory()?.attributes ?? []) {
      const value = this.attributeForm.controls[attribute.key]?.value;
      if (value === null || value === undefined || value === '') {
        continue;
      }

      attributes[attribute.key] = attribute.dataType === 'Number' || attribute.dataType === 'Decimal'
        ? Number(value)
        : value;
    }

    return attributes;
  }

  private populateAiEditForms(suggestion: SellerAiSuggestionResponse): void {
    this.aiApplyForm.reset({
      title: false,
      shortDescription: false,
      fullDescription: false,
      category: false,
      attributes: false,
      tags: false,
      imageAltText: false,
      confirmRiskFlags: false
    });

    this.aiEditForm.patchValue({
      title: suggestion.recommendedTitle ?? '',
      shortDescription: suggestion.shortDescription ?? '',
      fullDescription: suggestion.fullDescription ?? '',
      suggestedCategoryId: suggestion.suggestedCategoryId ?? this.basicForm.controls.categoryId.value,
      tags: suggestion.tags.join(', ')
    });

    this.rebuildAiAttributeControls(suggestion.attributes);
    this.rebuildAiImageAltTextControls(suggestion.imageAltText);
  }

  private rebuildAiAttributeControls(attributes: Record<string, unknown>): void {
    for (const key of Object.keys(this.aiAttributeEditForm.controls)) {
      this.aiAttributeEditForm.removeControl(key);
    }

    for (const [key, value] of Object.entries(attributes)) {
      this.aiAttributeEditForm.addControl(key, new FormControl(formatAiValue(value), { nonNullable: true }));
    }
  }

  private rebuildAiImageAltTextControls(imageAltText: Record<string, string | null>): void {
    for (const key of Object.keys(this.aiImageAltTextForm.controls)) {
      this.aiImageAltTextForm.removeControl(key);
    }

    for (const image of this.product()?.images ?? []) {
      this.aiImageAltTextForm.addControl(
        image.imageId,
        new FormControl(imageAltText[image.imageId] ?? image.altText ?? '', { nonNullable: true }));
    }
  }

  private selectedAiFields(): string[] {
    const value = this.aiApplyForm.getRawValue();
    return [
      value.title ? 'title' : null,
      value.shortDescription ? 'shortDescription' : null,
      value.fullDescription ? 'fullDescription' : null,
      value.category ? 'category' : null,
      value.attributes ? 'attributes' : null,
      value.tags ? 'tags' : null,
      value.imageAltText ? 'imageAltText' : null
    ].filter((field): field is string => field !== null);
  }

  private createAiEditedValues(fieldsToApply: readonly string[]): Record<string, unknown> {
    const values = this.aiEditForm.getRawValue();
    const editedValues: Record<string, unknown> = {};

    if (fieldsToApply.includes('title')) {
      editedValues['title'] = emptyToNull(values.title);
    }

    if (fieldsToApply.includes('shortDescription')) {
      editedValues['shortDescription'] = emptyToNull(values.shortDescription);
    }

    if (fieldsToApply.includes('fullDescription')) {
      editedValues['fullDescription'] = emptyToNull(values.fullDescription);
    }

    if (fieldsToApply.includes('category')) {
      editedValues['suggestedCategoryId'] = values.suggestedCategoryId || null;
    }

    if (fieldsToApply.includes('attributes')) {
      editedValues['attributes'] = this.createAiAttributesRequest();
    }

    if (fieldsToApply.includes('tags')) {
      editedValues['tags'] = values.tags
        .split(',')
        .map(tag => tag.trim())
        .filter(tag => tag.length > 0);
    }

    if (fieldsToApply.includes('imageAltText')) {
      editedValues['imageAltText'] = this.createAiImageAltTextRequest();
    }

    return editedValues;
  }

  private createAiAttributesRequest(): Record<string, unknown> {
    const attributes: Record<string, unknown> = {};
    const currentCategory = this.categories().find(category =>
      category.categoryId === (this.aiEditForm.controls.suggestedCategoryId.value || this.basicForm.controls.categoryId.value));

    for (const [key, control] of Object.entries(this.aiAttributeEditForm.controls)) {
      const definition = currentCategory?.attributes.find(attribute => attribute.key === key);
      attributes[key] = parseEditedAttributeValue(control.value, definition);
    }

    return attributes;
  }

  private createAiImageAltTextRequest(): Record<string, string | null> {
    const altText: Record<string, string | null> = {};
    for (const [imageId, control] of Object.entries(this.aiImageAltTextForm.controls)) {
      altText[imageId] = emptyToNull(control.value);
    }

    return altText;
  }

  private async ensureProductSaved(): Promise<SellerProductDetailResponse | null> {
    return this.product() ?? await this.saveDraft();
  }

  private async runProductAction(
    action: () => Promise<SellerProductDetailResponse>,
    successMessage: string): Promise<void> {
    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      this.setProduct(await action());
      this.successMessage.set(successMessage);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  private ensureValid(control: AbstractControl): boolean {
    if (control.invalid || this.isSaving()) {
      control.markAllAsTouched();
      return false;
    }

    return true;
  }
}

function emptyToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}

function parseRawAttributeValue(
  rawValue: string | undefined,
  attribute: SellerCatalogCategoryAttributeResponse): unknown {
  if (rawValue === undefined) {
    return attribute.dataType === 'MultiSelect' ? [] : '';
  }

  try {
    return JSON.parse(rawValue) as unknown;
  } catch {
    return rawValue;
  }
}

function parseEditedAttributeValue(
  rawValue: string,
  attribute: SellerCatalogCategoryAttributeResponse | undefined): unknown {
  const trimmed = rawValue.trim();
  if (trimmed.length === 0) {
    return null;
  }

  if (attribute?.dataType === 'Number' || attribute?.dataType === 'Decimal') {
    return Number(trimmed);
  }

  if (attribute?.dataType === 'Boolean') {
    return trimmed.toLowerCase() === 'true';
  }

  if (attribute?.dataType === 'MultiSelect') {
    return trimmed.split(',').map(value => value.trim()).filter(value => value.length > 0);
  }

  return trimmed;
}

function formatAiValue(value: unknown): string {
  if (Array.isArray(value)) {
    return value.join(', ');
  }

  if (value === null || value === undefined) {
    return '';
  }

  return typeof value === 'object'
    ? JSON.stringify(value)
    : String(value);
}
