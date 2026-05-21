import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { AdminWorkspaceNavComponent } from '../admin/admin-workspace-nav.component';
import { AdminSellerSummaryResponse } from '../admin/admin-seller.models';
import { AdminSellerService } from '../admin/admin-seller.service';
import { getApiErrorMessage } from '../auth/api-error';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-admin-sellers-page',
  imports: [
    AdminWorkspaceNavComponent,
    DatePipe,
    EmptyStateComponent,
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

      <app-page-header
        eyebrow="Admin review"
        heading="Seller approvals"
        description="Triage seller verification submissions before they can operate as verified sellers."
      >
        <div pageHeaderActions>
          <a mat-stroked-button routerLink="/admin/products">Product queue</a>
          <a mat-stroked-button routerLink="/admin/audit-logs">Audit logs</a>
        </div>
      </app-page-header>

      <form [formGroup]="filtersForm" (ngSubmit)="applyFilters()" class="route-card admin-moderation-filters" novalidate>
        <mat-form-field appearance="outline">
          <mat-label>Search sellers</mat-label>
          <input matInput formControlName="search" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Status</mat-label>
          <input matInput formControlName="status" placeholder="UnderReview" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Storefront</mat-label>
          <input matInput formControlName="storefront" placeholder="Store name or slug" />
        </mat-form-field>

        <div class="admin-audit-actions">
          <button mat-flat-button type="submit">Apply filters</button>
          <button mat-stroked-button type="button" (click)="clearFilters()">Clear</button>
        </div>
      </form>

      @if (isLoading()) {
        <div class="route-card">Loading pending sellers...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (filteredSellers().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Clear"
            heading="No pending sellers"
            message="New seller submissions will appear here after onboarding is submitted for verification."
          />
        } @else {
          <div class="admin-table admin-moderation-table" role="table" aria-label="Pending seller approvals">
            <div class="admin-table-row heading admin-moderation-table-row" role="row">
              <span role="columnheader">Seller</span>
              <span role="columnheader">Storefront</span>
              <span role="columnheader">Submitted</span>
              <span role="columnheader">Status</span>
              <span role="columnheader">Action</span>
            </div>

            @for (seller of filteredSellers(); track seller.sellerId) {
              <div class="admin-table-row admin-moderation-table-row" role="row">
                <span role="cell">
                  <strong>{{ seller.displayName ?? 'Unnamed seller' }}</strong>
                  <small>{{ seller.contactEmail ?? 'No contact email' }}</small>
                </span>
                <span role="cell">
                  <strong>{{ seller.storeName ?? 'No storefront' }}</strong>
                  <small>{{ seller.storeSlug ?? 'No slug' }}</small>
                </span>
                <span role="cell">
                  <strong>{{ seller.submittedAtUtc ? (seller.submittedAtUtc | date:'mediumDate') : 'Not recorded' }}</strong>
                  <small>{{ seller.submittedAtUtc ? (seller.submittedAtUtc | date:'shortTime') : 'No submission time' }}</small>
                </span>
                <span role="cell">
                  <app-status-badge [label]="seller.verificationStatus" [tone]="sellerStatusTone(seller.verificationStatus)" />
                </span>
                <span role="cell">
                  <a mat-stroked-button [routerLink]="['/admin/sellers', seller.sellerId]">Review</a>
                </span>
              </div>
            }
          </div>

          <p class="audit-count">{{ filteredSellers().length }} of {{ pendingSellers().length }} seller{{ pendingSellers().length === 1 ? '' : 's' }}</p>
        }
      }
    </section>
  `
})
export class AdminSellersPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly adminSellerService = inject(AdminSellerService);

  protected readonly pendingSellers = signal<AdminSellerSummaryResponse[]>([]);
  protected readonly filters = signal({ search: '', status: '', storefront: '' });
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly filtersForm = this.formBuilder.group({
    search: [''],
    status: [''],
    storefront: ['']
  });

  protected readonly filteredSellers = computed(() => {
    const { search, status, storefront } = this.filters();
    const normalizedSearch = search.trim().toLowerCase();
    const normalizedStatus = status.trim().toLowerCase();
    const normalizedStorefront = storefront.trim().toLowerCase();

    return this.pendingSellers().filter(seller => {
      const haystack = [
        seller.displayName,
        seller.contactEmail,
        seller.storeName,
        seller.storeSlug,
        seller.verificationStatus
      ]
        .filter(Boolean)
        .join(' ')
        .toLowerCase();

      const matchesSearch = normalizedSearch.length === 0 || haystack.includes(normalizedSearch);
      const matchesStatus = normalizedStatus.length === 0 || seller.verificationStatus.toLowerCase().includes(normalizedStatus);
      const matchesStorefront = normalizedStorefront.length === 0 ||
        [seller.storeName, seller.storeSlug].filter(Boolean).join(' ').toLowerCase().includes(normalizedStorefront);

      return matchesSearch && matchesStatus && matchesStorefront;
    });
  });

  async ngOnInit(): Promise<void> {
    await this.loadPendingSellers();
  }

  protected applyFilters(): void {
    this.filters.set(this.filtersForm.getRawValue());
  }

  protected clearFilters(): void {
    this.filtersForm.reset({ search: '', status: '', storefront: '' });
    this.applyFilters();
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

  private async loadPendingSellers(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.pendingSellers.set(await this.adminSellerService.getPendingSellers());
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }
}
