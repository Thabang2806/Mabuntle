import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { AdminSellerSummaryResponse } from '../admin/admin-seller.models';
import { AdminSellerService } from '../admin/admin-seller.service';
import { getApiErrorMessage } from '../auth/api-error';

@Component({
  selector: 'app-admin-sellers-page',
  imports: [DatePipe, MatButtonModule, RouterLink],
  template: `
    <section class="page admin-review">
      <a class="admin-back-link" routerLink="/admin">Back to dashboard</a>

      <div class="page-header">
        <span class="eyebrow">Admin review</span>
        <h1>Seller approvals</h1>
        <p>Review seller verification submissions before they can operate as verified sellers.</p>
        <a mat-stroked-button routerLink="/admin/products">Open product review queue</a>
        <a mat-stroked-button routerLink="/admin/audit-logs">View audit logs</a>
      </div>

      @if (isLoading()) {
        <div class="route-card">Loading pending sellers...</div>
      } @else {
        @if (errorMessage()) {
          <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
        }

        @if (pendingSellers().length === 0 && !errorMessage()) {
          <div class="route-card">
            <span class="status-pill">Clear</span>
            <h2>No pending sellers</h2>
            <p>New seller submissions will appear here after onboarding is submitted for verification.</p>
          </div>
        } @else {
          <div class="admin-table" role="table" aria-label="Pending seller approvals">
            <div class="admin-table-row heading" role="row">
              <span role="columnheader">Seller</span>
              <span role="columnheader">Storefront</span>
              <span role="columnheader">Submitted</span>
              <span role="columnheader">Status</span>
              <span role="columnheader">Action</span>
            </div>

            @for (seller of pendingSellers(); track seller.sellerId) {
              <div class="admin-table-row" role="row">
                <span role="cell">
                  <strong>{{ seller.displayName ?? 'Unnamed seller' }}</strong>
                  <small>{{ seller.contactEmail ?? 'No contact email' }}</small>
                </span>
                <span role="cell">
                  <strong>{{ seller.storeName ?? 'No storefront' }}</strong>
                  <small>{{ seller.storeSlug ?? 'No slug' }}</small>
                </span>
                <span role="cell">{{ seller.submittedAtUtc ? (seller.submittedAtUtc | date:'medium') : 'Not recorded' }}</span>
                <span role="cell"><span class="status-pill">{{ seller.verificationStatus }}</span></span>
                <span role="cell">
                  <a mat-stroked-button [routerLink]="['/admin/sellers', seller.sellerId]">Review</a>
                </span>
              </div>
            }
          </div>
        }
      }
    </section>
  `
})
export class AdminSellersPageComponent implements OnInit {
  private readonly adminSellerService = inject(AdminSellerService);

  protected readonly pendingSellers = signal<AdminSellerSummaryResponse[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    await this.loadPendingSellers();
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
