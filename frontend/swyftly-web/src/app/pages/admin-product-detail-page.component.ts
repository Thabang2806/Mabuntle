import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { AdminProductDetailResponse } from '../admin/admin-product.models';
import { AdminProductService } from '../admin/admin-product.service';
import { getApiErrorMessage } from '../auth/api-error';

@Component({
  selector: 'app-admin-product-detail-page',
  imports: [
    CurrencyPipe,
    DatePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    ReactiveFormsModule,
    RouterLink
  ],
  template: `
    <section class="page admin-review">
      <a class="admin-back-link" routerLink="/admin/products">Back to product queue</a>

      @if (isLoading()) {
        <div class="route-card">Loading product review...</div>
      } @else if (product()) {
        <div class="page-header">
          <span class="eyebrow">Product review</span>
          <h1>{{ product()?.title ?? 'Untitled product' }}</h1>
          <p>{{ product()?.categoryPath ?? 'No category' }}</p>
        </div>

        @if (errorMessage()) {
          <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
        }

        @if (successMessage()) {
          <p class="auth-alert success" role="status">{{ successMessage() }}</p>
        }

        <div class="admin-detail-layout">
          <div class="admin-detail-main">
            <article class="route-card admin-detail-card">
              <span class="status-pill">{{ product()?.status }}</span>
              <h2>Listing</h2>
              <dl class="admin-facts">
                <div><dt>Seller</dt><dd>{{ product()?.seller?.displayName ?? 'Unnamed seller' }}</dd></div>
                <div><dt>Seller status</dt><dd>{{ product()?.seller?.verificationStatus ?? 'Unknown' }}</dd></div>
                <div><dt>Slug</dt><dd>{{ product()?.slug ?? 'Not provided' }}</dd></div>
                <div><dt>Short description</dt><dd>{{ product()?.shortDescription ?? 'Not provided' }}</dd></div>
                <div><dt>Full description</dt><dd>{{ product()?.fullDescription ?? 'Not provided' }}</dd></div>
                <div><dt>Tags</dt><dd>{{ product()?.tags?.length ? product()?.tags?.join(', ') : 'None' }}</dd></div>
                @if (product()?.rejectionReason) {
                  <div><dt>Latest reason</dt><dd>{{ product()?.rejectionReason }}</dd></div>
                }
              </dl>
            </article>

            <article class="route-card admin-detail-card">
              <h2>Images</h2>
              @if ((product()?.images?.length ?? 0) === 0) {
                <p>No images are attached to this product.</p>
              } @else {
                <div class="admin-product-images">
                  @for (image of product()?.images; track image.imageId) {
                    <figure>
                      <img [src]="image.url" [alt]="image.altText ?? 'Product image'" loading="lazy">
                      <figcaption>
                        <span class="status-pill">{{ image.isPrimary ? 'Primary' : 'Image' }}</span>
                        <span>{{ image.altText ?? 'No alt text' }}</span>
                      </figcaption>
                    </figure>
                  }
                </div>
              }
            </article>

            <article class="route-card admin-detail-card">
              <h2>Attributes</h2>
              @if (attributeEntries().length === 0) {
                <p>No attributes were provided.</p>
              } @else {
                <dl class="admin-facts">
                  @for (attribute of attributeEntries(); track attribute.key) {
                    <div>
                      <dt>{{ attribute.key }}</dt>
                      <dd>{{ attribute.value }}</dd>
                    </div>
                  }
                </dl>
              }
            </article>

            <article class="route-card admin-detail-card">
              <h2>Variants</h2>
              @if ((product()?.variants?.length ?? 0) === 0) {
                <p>No variants were provided.</p>
              } @else {
                <div class="admin-product-variants">
                  @for (variant of product()?.variants; track variant.variantId) {
                    <div>
                      <span class="status-pill">{{ variant.status }}</span>
                      <strong>{{ variant.sku }}</strong>
                      <span>{{ variant.size }} / {{ variant.colour }}</span>
                      <span>{{ variant.price | currency:'ZAR':'symbol-narrow' }} · {{ variant.availableQuantity }} available</span>
                    </div>
                  }
                </div>
              }
            </article>

            <article class="route-card admin-detail-card">
              <h2>AI risk flags</h2>
              @if ((product()?.moderationResults?.length ?? 0) === 0) {
                <p>No moderation flags were recorded for this product.</p>
              } @else {
                <div class="admin-product-risks">
                  @for (result of product()?.moderationResults; track result.moderationResultId) {
                    <div>
                      <span class="status-pill">{{ result.riskLevel }}</span>
                      <strong>{{ result.reason }}</strong>
                      <span>{{ result.provider }} · {{ result.createdAtUtc | date:'medium' }}</span>
                      @if (result.flags.length > 0) {
                        <small>Flags: {{ result.flags.join(', ') }}</small>
                      }
                      @if (result.detectedTerms.length > 0) {
                        <small>Terms: {{ result.detectedTerms.join(', ') }}</small>
                      }
                      @if (result.missingFields.length > 0) {
                        <small>Missing: {{ result.missingFields.join(', ') }}</small>
                      }
                    </div>
                  }
                </div>
              }
            </article>
          </div>

          <aside class="admin-actions">
            <div class="route-card admin-action-card">
              <h2>Review actions</h2>

              <form [formGroup]="approveForm" (ngSubmit)="approve()" class="admin-reason-form" novalidate>
                @if (hasHighRiskModeration()) {
                  <mat-form-field appearance="outline">
                    <mat-label>Override reason</mat-label>
                    <textarea matInput rows="3" formControlName="overrideReason"></textarea>
                    <mat-hint>Required for unresolved high-risk AI flags.</mat-hint>
                  </mat-form-field>
                }
                <button mat-flat-button type="submit" [disabled]="isSaving()">Approve product</button>
              </form>

              <form [formGroup]="changesForm" (ngSubmit)="requestChanges()" class="admin-reason-form" novalidate>
                <mat-form-field appearance="outline">
                  <mat-label>Change request reason</mat-label>
                  <textarea matInput rows="3" formControlName="reason"></textarea>
                  @if (changesForm.controls.reason.hasError('required')) {
                    <mat-error>Reason is required.</mat-error>
                  }
                </mat-form-field>
                <button mat-stroked-button type="submit" [disabled]="isSaving()">Request changes</button>
              </form>

              <form [formGroup]="rejectForm" (ngSubmit)="reject()" class="admin-reason-form" novalidate>
                <mat-form-field appearance="outline">
                  <mat-label>Rejection reason</mat-label>
                  <textarea matInput rows="3" formControlName="reason"></textarea>
                  @if (rejectForm.controls.reason.hasError('required')) {
                    <mat-error>Reason is required.</mat-error>
                  }
                </mat-form-field>
                <button mat-stroked-button type="submit" [disabled]="isSaving()">Reject product</button>
              </form>
            </div>

            <div class="route-card admin-action-card">
              <h2>Audit trail</h2>
              @if ((product()?.auditTrail?.length ?? 0) === 0) {
                <p>No admin actions have been recorded for this product.</p>
              } @else {
                <ol class="audit-list">
                  @for (entry of product()?.auditTrail; track entry.id) {
                    <li>
                      <strong>{{ entry.actionType }}</strong>
                      <span>{{ entry.createdAtUtc | date:'medium' }}</span>
                      <span>{{ entry.actorRole ?? 'Admin' }}</span>
                      @if (entry.reason) {
                        <p>{{ entry.reason }}</p>
                      }
                    </li>
                  }
                </ol>
              }
            </div>
          </aside>
        </div>
      } @else {
        <p class="auth-alert error" role="alert">{{ errorMessage() ?? 'Product was not found.' }}</p>
      }
    </section>
  `
})
export class AdminProductDetailPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly adminProductService = inject(AdminProductService);

  protected readonly product = signal<AdminProductDetailResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected readonly attributeEntries = computed(() => {
    const attributes = this.product()?.attributes ?? {};
    return Object.entries(attributes).map(([key, value]) => ({
      key,
      value: this.formatAttributeValue(value)
    }));
  });

  protected readonly approveForm = this.formBuilder.group({
    overrideReason: ['']
  });

  protected readonly changesForm = this.formBuilder.group({
    reason: ['', [Validators.required]]
  });

  protected readonly rejectForm = this.formBuilder.group({
    reason: ['', [Validators.required]]
  });

  async ngOnInit(): Promise<void> {
    await this.loadProduct();
  }

  protected hasHighRiskModeration(): boolean {
    return this.product()?.moderationResults.some(result => result.needsAdminReview && result.riskLevel === 'High') ?? false;
  }

  protected async approve(): Promise<void> {
    const product = this.product();
    if (!product) {
      return;
    }

    const overrideReason = this.approveForm.getRawValue().overrideReason.trim();
    if (this.hasHighRiskModeration() && !overrideReason) {
      this.errorMessage.set('Override reason is required for high-risk products.');
      return;
    }

    await this.runAction(
      () => this.adminProductService.approveProduct(product.productId, { overrideReason: overrideReason || null }),
      'Product approved.');
    this.approveForm.reset();
  }

  protected async requestChanges(): Promise<void> {
    if (this.changesForm.invalid) {
      this.changesForm.markAllAsTouched();
      return;
    }

    const product = this.product();
    if (!product) {
      return;
    }

    const reason = this.changesForm.getRawValue().reason.trim();
    await this.runAction(
      () => this.adminProductService.requestChanges(product.productId, { reason }),
      'Changes requested.');
    this.changesForm.reset();
  }

  protected async reject(): Promise<void> {
    if (this.rejectForm.invalid) {
      this.rejectForm.markAllAsTouched();
      return;
    }

    const product = this.product();
    if (!product) {
      return;
    }

    const reason = this.rejectForm.getRawValue().reason.trim();
    await this.runAction(
      () => this.adminProductService.rejectProduct(product.productId, { reason }),
      'Product rejected.');
    this.rejectForm.reset();
  }

  private async loadProduct(): Promise<void> {
    const productId = this.route.snapshot.paramMap.get('productId');
    if (!productId) {
      this.errorMessage.set('Product id is missing.');
      this.isLoading.set(false);
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.product.set(await this.adminProductService.getProduct(productId));
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.product.set(null);
    } finally {
      this.isLoading.set(false);
    }
  }

  private async runAction(
    action: () => Promise<AdminProductDetailResponse>,
    message: string): Promise<void> {
    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      this.product.set(await action());
      this.successMessage.set(message);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
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
