import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { getApiErrorMessage } from '../auth/api-error';
import { BuyerReturnRequestResult } from '../buyer/buyer-return.models';
import { BuyerReturnService } from '../buyer/buyer-return.service';
import { BuyerWorkspaceNavComponent } from '../buyer/buyer-workspace-nav.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-buyer-return-detail-page',
  imports: [
    BuyerWorkspaceNavComponent,
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
    <section class="page buyer-ops-page">
      <app-buyer-workspace-nav />

      <a class="admin-back-link" routerLink="/account/returns">Back to returns</a>

      <app-page-header
        eyebrow="Buyer account"
        [heading]="returnRequest() ? 'Return ' + returnRequest()!.returnRequestId : 'Return'"
        description="Review return status, seller response, messages, and dispute options."
      >
        <div pageHeaderActions>
          @if (returnRequest()) {
            <app-status-badge [label]="returnRequest()!.status" [tone]="statusTone(returnRequest()!.status)" />
          }
        </div>
      </app-page-header>

      @if (isLoading()) {
        <div class="route-card">Loading return...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (successMessage()) {
          <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
        }

        @if (returnRequest()) {
          <div class="buyer-detail-grid">
            <section class="buyer-panel">
              <h2>Return summary</h2>
              <dl class="seller-facts">
                <div><dt>Order</dt><dd>{{ returnRequest()!.orderId }}</dd></div>
                <div><dt>Reason</dt><dd>{{ returnRequest()!.reason }}</dd></div>
                <div><dt>Requested</dt><dd>{{ returnRequest()!.requestedAtUtc | date:'medium' }}</dd></div>
                <div><dt>Details</dt><dd>{{ returnRequest()!.details ?? 'No extra details provided.' }}</dd></div>
                <div><dt>Seller response</dt><dd>{{ returnRequest()!.sellerResponseReason ?? 'No seller response yet.' }}</dd></div>
                @if (returnRequest()!.disputeReason) {
                  <div><dt>Dispute</dt><dd>{{ returnRequest()!.disputeReason }}</dd></div>
                }
              </dl>
            </section>

            <section class="buyer-panel">
              <h2>Dispute escalation</h2>
              @if (canDispute()) {
                <p>If you disagree with the seller response, escalate this return for admin review.</p>
                <form [formGroup]="disputeForm" (ngSubmit)="disputeReturn()" class="buyer-form-grid" novalidate>
                  <mat-form-field appearance="outline">
                    <mat-label>Dispute reason</mat-label>
                    <textarea matInput rows="4" formControlName="reason"></textarea>
                  </mat-form-field>
                  <button mat-flat-button type="submit" [disabled]="isSaving()">Open dispute</button>
                </form>
              } @else {
                <app-ui-alert tone="info">Dispute escalation is available after a return is rejected by the seller.</app-ui-alert>
                <a mat-stroked-button routerLink="/account/support">Contact support</a>
              }
            </section>

            <section class="buyer-panel">
              <h2>Store policy snapshot</h2>
              <p>This is the seller policy copied when the original order was created.</p>
              @if (sellerPolicySnapshotEntries().length > 0) {
                <dl class="seller-facts">
                  @for (entry of sellerPolicySnapshotEntries(); track entry.label) {
                    <div><dt>{{ entry.label }}</dt><dd>{{ entry.value }}</dd></div>
                  }
                </dl>
              } @else {
                <app-ui-alert tone="info">This return does not have checkout-time store-policy context.</app-ui-alert>
              }
            </section>
          </div>

          <section class="buyer-panel">
            <h2>Return items</h2>
            <div class="seller-item-list">
              @for (item of returnRequest()!.items; track item.returnItemId) {
                <div class="seller-item-row">
                  <span>
                    <strong>{{ item.quantity }} requested</strong>
                    <small>Order item {{ item.orderItemId }}</small>
                  </span>
                  <span>{{ item.reason }}</span>
                  <span>{{ item.isOpenedOrUnsealed ? 'Opened or unsealed' : 'Unopened' }}</span>
                  @if (item.note) {
                    <small>{{ item.note }}</small>
                  }
                </div>
              }
            </div>
          </section>

          <section class="buyer-panel">
            <h2>Messages</h2>
            @if (returnRequest()!.messages.length === 0) {
              <p>No return messages yet.</p>
            } @else {
              <div class="seller-message-list">
                @for (message of returnRequest()!.messages; track message.returnMessageId) {
                  <article>
                    <strong>{{ message.senderRole }}</strong>
                    <span>{{ message.createdAtUtc | date:'medium' }}</span>
                    <p>{{ message.message }}</p>
                  </article>
                }
              </div>
            }
          </section>
        }
      }
    </section>
  `
})
export class BuyerReturnDetailPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly returnService = inject(BuyerReturnService);
  private readonly route = inject(ActivatedRoute);

  protected readonly returnRequest = signal<BuyerReturnRequestResult | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  protected readonly disputeForm = this.formBuilder.group({
    reason: ['', [Validators.required]]
  });

  async ngOnInit(): Promise<void> {
    await this.loadReturn();
  }

  protected canDispute(): boolean {
    return this.returnRequest()?.status === 'Rejected';
  }

  protected async disputeReturn(): Promise<void> {
    if (this.disputeForm.invalid || this.isSaving()) {
      this.disputeForm.markAllAsTouched();
      return;
    }

    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      this.returnRequest.set(await this.returnService.disputeReturn(this.returnRequestId(), this.disputeForm.getRawValue()));
      this.disputeForm.reset({ reason: '' });
      this.successMessage.set('Return dispute opened.');
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  protected statusTone(status: string): StatusBadgeTone {
    if (['Requested', 'AwaitingSellerResponse', 'ReturnInTransit', 'ReturnedToSeller', 'RefundPending'].includes(status)) {
      return 'warning';
    }

    if (['Approved', 'Refunded', 'Closed'].includes(status)) {
      return 'success';
    }

    if (['Rejected', 'Disputed'].includes(status)) {
      return 'danger';
    }

    return 'neutral';
  }

  protected sellerPolicySnapshotEntries(): { label: string; value: string }[] {
    const snapshot = this.returnRequest()?.sellerPolicySnapshot;
    if (!snapshot) {
      return [];
    }

    return [
      snapshot.returnWindowDays === null ? null : { label: 'Return window', value: `${snapshot.returnWindowDays} day${snapshot.returnWindowDays === 1 ? '' : 's'}` },
      snapshot.returnPolicy ? { label: 'Returns', value: snapshot.returnPolicy } : null,
      snapshot.exchangePolicy ? { label: 'Exchanges', value: snapshot.exchangePolicy } : null,
      snapshot.fulfilmentPolicy ? { label: 'Fulfilment', value: snapshot.fulfilmentPolicy } : null,
      snapshot.supportPolicy ? { label: 'Support', value: snapshot.supportPolicy } : null,
      snapshot.careInstructions ? { label: 'Care', value: snapshot.careInstructions } : null,
      snapshot.productDisclaimer ? { label: 'Disclaimer', value: snapshot.productDisclaimer } : null
    ].filter((entry): entry is { label: string; value: string } => entry !== null);
  }

  private async loadReturn(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.returnRequest.set(await this.returnService.getReturn(this.returnRequestId()));
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  private returnRequestId(): string {
    return this.route.snapshot.paramMap.get('returnRequestId') ?? '';
  }
}
