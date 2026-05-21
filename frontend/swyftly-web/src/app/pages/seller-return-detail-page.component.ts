import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { getApiErrorMessage } from '../auth/api-error';
import { SellerReturnRequestResult } from '../seller/seller-return.models';
import { SellerReturnService } from '../seller/seller-return.service';
import { SellerWorkspaceNavComponent } from '../seller/seller-workspace-nav.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-seller-return-detail-page',
  imports: [
    DatePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    SellerWorkspaceNavComponent,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page seller-ops-page">
      <app-seller-workspace-nav />

      <a class="admin-back-link" routerLink="/seller/returns">Back to returns</a>

      <app-page-header
        eyebrow="Seller operations"
        [heading]="returnRequest() ? 'Return ' + returnRequest()!.returnRequestId : 'Return'"
        description="Review the buyer return request and respond with a seller decision."
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
          <div class="seller-detail-grid">
            <section class="seller-panel">
              <h2>Request summary</h2>
              <dl class="seller-facts">
                <div><dt>Order</dt><dd>{{ returnRequest()!.orderId }}</dd></div>
                <div><dt>Reason</dt><dd>{{ returnRequest()!.reason }}</dd></div>
                <div><dt>Requested</dt><dd>{{ returnRequest()!.requestedAtUtc | date:'medium' }}</dd></div>
                <div><dt>Details</dt><dd>{{ returnRequest()!.details ?? 'No extra details provided.' }}</dd></div>
                @if (returnRequest()!.disputeReason) {
                  <div><dt>Dispute</dt><dd>{{ returnRequest()!.disputeReason }}</dd></div>
                }
              </dl>
            </section>

            <section class="seller-panel">
              <h2>Seller response</h2>
              <p>Approve or reject the return based on the policy context and item condition provided by the buyer.</p>
              <form [formGroup]="responseForm" class="seller-form-grid" novalidate>
                <mat-form-field appearance="outline">
                  <mat-label>Response message</mat-label>
                  <textarea matInput rows="4" formControlName="message"></textarea>
                </mat-form-field>

                <div class="seller-action-row">
                  <button mat-flat-button type="button" [disabled]="isActing()" (click)="approveReturn()">Approve return</button>
                  <button mat-stroked-button type="button" [disabled]="isActing()" (click)="rejectReturn()">Reject return</button>
                </div>
              </form>
            </section>
          </div>

          <section class="seller-panel">
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

          <section class="seller-panel">
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
export class SellerReturnDetailPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly returnService = inject(SellerReturnService);
  private readonly route = inject(ActivatedRoute);

  protected readonly returnRequest = signal<SellerReturnRequestResult | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isActing = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  protected readonly responseForm = this.formBuilder.group({
    message: ['']
  });

  async ngOnInit(): Promise<void> {
    await this.loadReturn();
  }

  protected async approveReturn(): Promise<void> {
    await this.respond(
      () => this.returnService.approveReturn(this.returnRequestId(), { message: this.messageValue() }),
      'Return approved.');
  }

  protected async rejectReturn(): Promise<void> {
    await this.respond(
      () => this.returnService.rejectReturn(this.returnRequestId(), { message: this.messageValue() }),
      'Return rejected.');
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

  private async respond(action: () => Promise<SellerReturnRequestResult>, successMessage: string): Promise<void> {
    if (this.isActing()) {
      return;
    }

    this.isActing.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      this.returnRequest.set(await action());
      this.successMessage.set(successMessage);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isActing.set(false);
    }
  }

  private messageValue(): string | null {
    const trimmed = this.responseForm.controls.message.value.trim();
    return trimmed.length === 0 ? null : trimmed;
  }

  private returnRequestId(): string {
    return this.route.snapshot.paramMap.get('returnRequestId') ?? '';
  }
}
