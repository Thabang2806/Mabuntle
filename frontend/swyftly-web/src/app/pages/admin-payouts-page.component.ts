import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { AdminPayoutResponse } from '../admin/admin-payout.models';
import { AdminPayoutService } from '../admin/admin-payout.service';
import { getApiErrorMessage } from '../auth/api-error';
import { AuthService } from '../auth/auth.service';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

type PayoutAction = 'hold' | 'release' | 'make-available' | 'process' | 'reconcile';

@Component({
  selector: 'app-admin-payouts-page',
  imports: [
    CurrencyPipe,
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
    <section class="page admin-finance-page">
      <a class="admin-back-link" routerLink="/admin">Back to dashboard</a>

      <app-page-header
        eyebrow="Admin finance"
        heading="Payouts"
        description="Review pending and held seller payouts, with finance role checks visible before each action."
      >
        <div pageHeaderActions>
          <a mat-stroked-button routerLink="/admin/refunds">Refunds</a>
          <a mat-stroked-button routerLink="/admin/reports">Reports</a>
          <a mat-stroked-button routerLink="/admin/audit-logs">Audit logs</a>
        </div>
      </app-page-header>

      <div class="admin-finance-policy">
        <app-status-badge [label]="roleSummary()" tone="accent" />
        <span>Hold and make available require finance operate. Release, process, and reconcile require finance approve.</span>
      </div>

      @if (isLoading()) {
        <div class="route-card">Loading payouts...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (successMessage()) {
          <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
        }

        @if (payouts().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Payouts"
            heading="No pending payouts"
            message="Pending or held payout records will appear here when seller ledger activity is ready for finance review."
          />
        } @else {
          <div class="admin-finance-layout">
            <div class="admin-table admin-finance-table" role="table" aria-label="Pending payouts">
              <div class="admin-table-row heading admin-finance-table-row" role="row">
                <span role="columnheader">Payout</span>
                <span role="columnheader">Seller</span>
                <span role="columnheader">Amount</span>
                <span role="columnheader">Status</span>
                <span role="columnheader">Action</span>
              </div>

              @for (payout of payouts(); track payout.payoutId) {
                <div class="admin-table-row admin-finance-table-row" role="row">
                  <span role="cell">
                    <strong>{{ payout.payoutId }}</strong>
                    <small>{{ payout.createdAtUtc | date:'medium' }}</small>
                  </span>
                  <span role="cell">
                    <strong>{{ payout.sellerId }}</strong>
                    <small>{{ payout.items.length }} payout item{{ payout.items.length === 1 ? '' : 's' }}</small>
                  </span>
                  <span role="cell">
                    <strong>{{ payout.amount | currency:payout.currency:'symbol-narrow' }}</strong>
                    <small>{{ payoutItemSummary(payout) }}</small>
                  </span>
                  <span role="cell">
                    <app-status-badge [label]="payout.status" [tone]="statusTone(payout.status)" />
                    @if (payout.holdReason) {
                      <small>Hold: {{ payout.holdReason }}</small>
                    }
                  </span>
                  <span role="cell">
                    <button mat-stroked-button type="button" (click)="selectPayout(payout)">Select</button>
                  </span>
                </div>
              }
            </div>

            <aside class="admin-finance-action-panel">
              <h2>Payout action</h2>
              @if (!selectedPayout()) {
                <p>Select a payout to review available finance actions.</p>
              } @else {
                <app-status-badge [label]="selectedPayout()!.status" [tone]="statusTone(selectedPayout()!.status)" />
                <strong>{{ selectedPayout()!.amount | currency:selectedPayout()!.currency:'symbol-narrow' }}</strong>
                <small>{{ selectedPayout()!.payoutId }}</small>

                <form [formGroup]="reasonForm" class="admin-finance-form" novalidate>
                  <mat-form-field appearance="outline">
                    <mat-label>Reason</mat-label>
                    <textarea matInput rows="4" formControlName="reason"></textarea>
                  </mat-form-field>

                  <div class="admin-finance-actions">
                    <button mat-stroked-button type="button" [disabled]="!canOperate() || isActing()" (click)="runAction('hold')">Hold</button>
                    <button mat-stroked-button type="button" [disabled]="!canApprove() || isActing()" (click)="runAction('release')">Release</button>
                    <button mat-stroked-button type="button" [disabled]="!canOperate() || isActing()" (click)="runAction('make-available')">Make available</button>
                    <button mat-flat-button type="button" [disabled]="!canApprove() || isActing()" (click)="runAction('process')">Process</button>
                    <button mat-stroked-button type="button" [disabled]="!canApprove() || isActing()" (click)="runAction('reconcile')">Reconcile</button>
                  </div>
                </form>

                @if (!canOperate() || !canApprove()) {
                  <p class="admin-finance-note">Unavailable actions are still enforced by the API. Dual-control conflicts are shown as finance errors when returned by the server.</p>
                }
              }
            </aside>
          </div>
        }
      }
    </section>
  `
})
export class AdminPayoutsPageComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly payoutService = inject(AdminPayoutService);

  protected readonly payouts = signal<AdminPayoutResponse[]>([]);
  protected readonly selectedPayout = signal<AdminPayoutResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isActing = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  protected readonly canOperate = computed(() => this.authService.hasAnyRole(['FinanceOperator', 'SuperAdmin']));
  protected readonly canApprove = computed(() => this.authService.hasAnyRole(['FinanceApprover', 'SuperAdmin']));

  protected readonly reasonForm = this.formBuilder.group({
    reason: ['', [Validators.required]]
  });

  async ngOnInit(): Promise<void> {
    await this.loadPayouts();
  }

  protected selectPayout(payout: AdminPayoutResponse): void {
    this.selectedPayout.set(payout);
    this.reasonForm.reset({ reason: '' });
  }

  protected async runAction(action: PayoutAction): Promise<void> {
    if (!this.canRunAction(action)) {
      this.errorMessage.set('You can review payouts, but you do not have permission for this finance action.');
      return;
    }

    const payout = this.selectedPayout();
    if (!payout || this.reasonForm.invalid || this.isActing()) {
      this.reasonForm.markAllAsTouched();
      return;
    }

    const request = this.reasonForm.getRawValue();
    this.isActing.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const updated = await this.executeAction(action, payout.payoutId, request);
      this.replacePayout(updated);
      this.selectedPayout.set(updated);
      this.successMessage.set('Payout action completed.');
      this.reasonForm.reset({ reason: '' });
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isActing.set(false);
    }
  }

  protected payoutItemSummary(payout: AdminPayoutResponse): string {
    const firstItem = payout.items[0];
    if (!firstItem) {
      return 'No payout items';
    }

    return firstItem.orderId ? `Order ${firstItem.orderId}` : `Ledger ${firstItem.ledgerEntryId}`;
  }

  protected roleSummary(): string {
    if (this.canOperate() && this.canApprove()) {
      return 'Operate and approve';
    }

    if (this.canOperate()) {
      return 'Operate only';
    }

    if (this.canApprove()) {
      return 'Approve only';
    }

    return 'Read only';
  }

  protected statusTone(status: string): StatusBadgeTone {
    if (['Pending', 'Available', 'Processing'].includes(status)) {
      return 'accent';
    }

    if (status === 'PaidOut') {
      return 'success';
    }

    if (['OnHold', 'Failed', 'Reversed'].includes(status)) {
      return 'warning';
    }

    return 'neutral';
  }

  private async loadPayouts(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.payouts.set(await this.payoutService.getPendingPayouts());
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  private executeAction(
    action: PayoutAction,
    payoutId: string,
    request: { reason: string }
  ): Promise<AdminPayoutResponse> {
    switch (action) {
      case 'hold':
        return this.payoutService.holdPayout(payoutId, request);
      case 'release':
        return this.payoutService.releasePayout(payoutId, request);
      case 'make-available':
        return this.payoutService.makePayoutAvailable(payoutId, request);
      case 'process':
        return this.payoutService.processPayout(payoutId, request);
      case 'reconcile':
        return this.payoutService.reconcilePayout(payoutId, request);
    }
  }

  private canRunAction(action: PayoutAction): boolean {
    if (action === 'hold' || action === 'make-available') {
      return this.canOperate();
    }

    return this.canApprove();
  }

  private replacePayout(updated: AdminPayoutResponse): void {
    this.payouts.set(this.payouts().map(payout => payout.payoutId === updated.payoutId ? updated : payout));
  }
}
