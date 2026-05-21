import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { AdminWorkspaceNavComponent } from '../admin/admin-workspace-nav.component';
import { AdminSellerDetailResponse } from '../admin/admin-seller.models';
import { AdminSellerService } from '../admin/admin-seller.service';
import { getApiErrorMessage } from '../auth/api-error';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-admin-seller-detail-page',
  imports: [
    AdminWorkspaceNavComponent,
    DatePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page admin-review">
      <app-admin-workspace-nav />
      <a class="admin-back-link" routerLink="/admin/sellers">Back to seller approvals</a>

      @if (isLoading()) {
        <div class="route-card">Loading seller review...</div>
      } @else if (seller()) {
        <app-page-header
          eyebrow="Seller review"
          [heading]="seller()?.displayName ?? seller()?.storefront?.storeName ?? 'Seller review'"
          [description]="seller()?.contactEmail ?? 'No contact email'"
        >
          <div pageHeaderActions>
            <app-status-badge [label]="seller()!.verificationStatus" [tone]="sellerStatusTone(seller()!.verificationStatus)" />
          </div>
        </app-page-header>

        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (successMessage()) {
          <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
        }

        <div class="admin-detail-layout">
          <div class="admin-detail-main">
            <article class="route-card admin-detail-card">
              <app-status-badge [label]="seller()!.verificationStatus" [tone]="sellerStatusTone(seller()!.verificationStatus)" />
              <h2>Profile</h2>
              <dl class="admin-facts">
                <div><dt>Business type</dt><dd>{{ seller()?.businessType ?? 'Not provided' }}</dd></div>
                <div><dt>Business name</dt><dd>{{ seller()?.businessName ?? 'Not provided' }}</dd></div>
                <div><dt>Phone</dt><dd>{{ seller()?.phoneNumber ?? 'Not provided' }}</dd></div>
              </dl>
            </article>

            <article class="route-card admin-detail-card">
              <h2>Review completeness</h2>
              <div class="admin-checklist">
                @for (item of completenessItems(); track item.label) {
                  <div>
                    <app-status-badge [label]="item.isComplete ? 'Ready' : 'Missing'" [tone]="item.isComplete ? 'success' : 'warning'" />
                    <span>{{ item.label }}</span>
                    <small>{{ item.description }}</small>
                  </div>
                }
              </div>
            </article>

            <article class="route-card admin-detail-card">
              <h2>Storefront</h2>
              <dl class="admin-facts">
                <div><dt>Store name</dt><dd>{{ seller()?.storefront?.storeName ?? 'Not provided' }}</dd></div>
                <div><dt>Slug</dt><dd>{{ seller()?.storefront?.slug ?? 'Not provided' }}</dd></div>
                <div><dt>Description</dt><dd>{{ seller()?.storefront?.description ?? 'Not provided' }}</dd></div>
                <div><dt>Published</dt><dd>{{ seller()?.storefront?.isPublished ? 'Yes' : 'No' }}</dd></div>
                <div><dt>Logo</dt><dd>{{ seller()?.storefront?.logoUrl ? 'Provided' : 'Not provided' }}</dd></div>
                <div><dt>Banner</dt><dd>{{ seller()?.storefront?.bannerUrl ? 'Provided' : 'Not provided' }}</dd></div>
              </dl>
            </article>

            <article class="route-card admin-detail-card">
              <h2>Address</h2>
              <dl class="admin-facts">
                <div><dt>Line 1</dt><dd>{{ seller()?.address?.addressLine1 ?? 'Not provided' }}</dd></div>
                <div><dt>Line 2</dt><dd>{{ seller()?.address?.addressLine2 ?? 'Not provided' }}</dd></div>
                <div><dt>City</dt><dd>{{ seller()?.address?.city ?? 'Not provided' }}</dd></div>
                <div><dt>Province</dt><dd>{{ seller()?.address?.province ?? 'Not provided' }}</dd></div>
                <div><dt>Postal code</dt><dd>{{ seller()?.address?.postalCode ?? 'Not provided' }}</dd></div>
                <div><dt>Country</dt><dd>{{ seller()?.address?.countryCode ?? 'Not provided' }}</dd></div>
              </dl>
            </article>

            <article class="route-card admin-detail-card">
              <h2>Payout setup</h2>
              <dl class="admin-facts">
                <div><dt>Provider reference</dt><dd>{{ seller()?.payout?.payoutProviderReference ?? 'Not provided' }}</dd></div>
                <div><dt>Submitted</dt><dd>{{ seller()?.payout?.hasSubmittedPlaceholder ? 'Yes' : 'No' }}</dd></div>
                <div><dt>Admin approved</dt><dd>{{ seller()?.payout?.isAdminApproved ? 'Yes' : 'No' }}</dd></div>
              </dl>
            </article>
          </div>

          <aside class="admin-actions">
            <div class="route-card admin-action-card">
              <h2>Review actions</h2>
              <button mat-flat-button type="button" [disabled]="isSaving()" (click)="approve()">Approve seller</button>

              <form [formGroup]="rejectForm" (ngSubmit)="reject()" class="admin-reason-form" novalidate>
                <mat-form-field appearance="outline">
                  <mat-label>Rejection reason</mat-label>
                  <textarea matInput rows="3" formControlName="reason"></textarea>
                  @if (rejectForm.controls.reason.hasError('required')) {
                    <mat-error>Reason is required.</mat-error>
                  }
                </mat-form-field>
                <button mat-stroked-button type="submit" [disabled]="isSaving()">Reject seller</button>
              </form>

              <form [formGroup]="suspendForm" (ngSubmit)="suspend()" class="admin-reason-form" novalidate>
                <mat-form-field appearance="outline">
                  <mat-label>Suspension reason</mat-label>
                  <textarea matInput rows="3" formControlName="reason"></textarea>
                  @if (suspendForm.controls.reason.hasError('required')) {
                    <mat-error>Reason is required.</mat-error>
                  }
                </mat-form-field>
                <button mat-stroked-button type="submit" [disabled]="isSaving()">Suspend seller</button>
              </form>
            </div>

            <div class="route-card admin-action-card">
              <h2>Audit trail</h2>
              @if ((seller()?.auditTrail?.length ?? 0) === 0) {
                <p>No admin actions have been recorded for this seller.</p>
              } @else {
                <ol class="audit-list">
                  @for (entry of seller()?.auditTrail; track entry.id) {
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
        <app-ui-alert tone="error">{{ errorMessage() ?? 'Seller was not found.' }}</app-ui-alert>
      }
    </section>
  `
})
export class AdminSellerDetailPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly adminSellerService = inject(AdminSellerService);

  protected readonly seller = signal<AdminSellerDetailResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  protected readonly rejectForm = this.formBuilder.group({
    reason: ['', [Validators.required]]
  });

  protected readonly suspendForm = this.formBuilder.group({
    reason: ['', [Validators.required]]
  });

  async ngOnInit(): Promise<void> {
    await this.loadSeller();
  }

  protected async approve(): Promise<void> {
    const seller = this.seller();
    if (!seller) {
      return;
    }

    await this.runAction(
      () => this.adminSellerService.approveSeller(seller.sellerId),
      'Seller approved.');
  }

  protected async reject(): Promise<void> {
    if (this.rejectForm.invalid) {
      this.rejectForm.markAllAsTouched();
      return;
    }

    const seller = this.seller();
    if (!seller) {
      return;
    }

    const reason = this.rejectForm.getRawValue().reason.trim();
    await this.runAction(
      () => this.adminSellerService.rejectSeller(seller.sellerId, { reason }),
      'Seller rejected.');
    this.rejectForm.reset();
  }

  protected async suspend(): Promise<void> {
    if (this.suspendForm.invalid) {
      this.suspendForm.markAllAsTouched();
      return;
    }

    const seller = this.seller();
    if (!seller) {
      return;
    }

    const reason = this.suspendForm.getRawValue().reason.trim();
    await this.runAction(
      () => this.adminSellerService.suspendSeller(seller.sellerId, { reason }),
      'Seller suspended.');
    this.suspendForm.reset();
  }

  protected completenessItems(): { label: string; description: string; isComplete: boolean }[] {
    const seller = this.seller();
    if (!seller) {
      return [];
    }

    return [
      {
        label: 'Profile',
        description: 'Business type, business name, contact email, and phone are present.',
        isComplete: Boolean(seller.businessType && seller.businessName && seller.contactEmail && seller.phoneNumber)
      },
      {
        label: 'Storefront',
        description: 'Store name, slug, description, and publication state are available for review.',
        isComplete: Boolean(seller.storefront?.storeName && seller.storefront.slug && seller.storefront.description)
      },
      {
        label: 'Address',
        description: 'Primary address, city, province, postal code, and country are present.',
        isComplete: Boolean(seller.address?.addressLine1 && seller.address.city && seller.address.province && seller.address.postalCode && seller.address.countryCode)
      },
      {
        label: 'Payout setup',
        description: 'Provider reference placeholder was submitted for admin approval.',
        isComplete: Boolean(seller.payout?.payoutProviderReference && seller.payout.hasSubmittedPlaceholder)
      }
    ];
  }

  protected sellerStatusTone(status: string): StatusBadgeTone {
    if (['Verified', 'Approved'].includes(status)) {
      return 'success';
    }

    if (['Rejected', 'Suspended'].includes(status)) {
      return 'danger';
    }

    return 'warning';
  }

  private async loadSeller(): Promise<void> {
    const sellerId = this.route.snapshot.paramMap.get('sellerId');
    if (!sellerId) {
      this.errorMessage.set('Seller id is missing.');
      this.isLoading.set(false);
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.seller.set(await this.adminSellerService.getSeller(sellerId));
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.seller.set(null);
    } finally {
      this.isLoading.set(false);
    }
  }

  private async runAction(
    action: () => Promise<AdminSellerDetailResponse>,
    message: string): Promise<void> {
    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      this.seller.set(await action());
      this.successMessage.set(message);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }
}
