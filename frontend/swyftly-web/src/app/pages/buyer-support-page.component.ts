import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { getApiErrorMessage } from '../auth/api-error';
import { BuyerSupportTicketCategory, BuyerSupportTicketResponse } from '../buyer/buyer-support.models';
import { BuyerSupportService } from '../buyer/buyer-support.service';
import { BuyerWorkspaceNavComponent } from '../buyer/buyer-workspace-nav.component';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-buyer-support-page',
  imports: [
    BuyerWorkspaceNavComponent,
    DatePipe,
    EmptyStateComponent,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page buyer-ops-page">
      <app-buyer-workspace-nav />

      <app-page-header
        eyebrow="Buyer support"
        heading="Support tickets"
        description="Create and track marketplace support requests for orders, products, payments, and returns."
      />

      @if (isLoading()) {
        <div class="route-card">Loading support tickets...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (successMessage()) {
          <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
        }

        <div class="buyer-detail-grid">
          <section class="buyer-panel">
            <h2>Create ticket</h2>
            <form [formGroup]="ticketForm" (ngSubmit)="createTicket()" class="buyer-form-grid" novalidate>
              <mat-form-field appearance="outline">
                <mat-label>Category</mat-label>
                <mat-select formControlName="category">
                  @for (category of categories; track category) {
                    <mat-option [value]="category">{{ category }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Subject</mat-label>
                <input matInput formControlName="subject" />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Description</mat-label>
                <textarea matInput rows="4" formControlName="description"></textarea>
              </mat-form-field>

              <div class="form-grid">
                <mat-form-field appearance="outline">
                  <mat-label>Linked order ID</mat-label>
                  <input matInput formControlName="linkedOrderId" />
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>Linked seller ID</mat-label>
                  <input matInput formControlName="linkedSellerId" />
                </mat-form-field>
              </div>

              <button mat-flat-button type="submit" [disabled]="isSaving()">Create ticket</button>
            </form>
          </section>

          <section class="buyer-panel">
            <h2>Support path</h2>
            <p>Support tickets are visible to marketplace support staff. Internal support notes are not shown in buyer responses.</p>
            <div class="seller-result-steps">
              <div>
                <strong>Open</strong>
                <span>Create a ticket with clear order or product context.</span>
              </div>
              <div>
                <strong>Respond</strong>
                <span>Add buyer-side messages as support asks for details.</span>
              </div>
              <div>
                <strong>Resolve</strong>
                <span>Resolved and closed states are managed by support staff.</span>
              </div>
            </div>
          </section>
        </div>

        @if (tickets().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Support"
            heading="No support tickets"
            message="Create a ticket when you need help with an order, return, payment, seller, or product."
          />
        } @else {
          <div class="admin-table buyer-ops-table" role="table" aria-label="Buyer support tickets">
            <div class="admin-table-row heading buyer-ops-table-row" role="row">
              <span role="columnheader">Ticket</span>
              <span role="columnheader">Category</span>
              <span role="columnheader">Opened</span>
              <span role="columnheader">Status</span>
              <span role="columnheader">Action</span>
            </div>

            @for (ticket of tickets(); track ticket.supportTicketId) {
              <div class="admin-table-row buyer-ops-table-row" role="row">
                <span role="cell">
                  <strong>{{ ticket.subject }}</strong>
                  <small>{{ ticket.description }}</small>
                </span>
                <span role="cell">{{ ticket.category }}</span>
                <span role="cell">{{ ticket.openedAtUtc | date:'medium' }}</span>
                <span role="cell">
                  <app-status-badge [label]="ticket.status" [tone]="statusTone(ticket.status)" />
                  <small>{{ ticket.messages.length }} message{{ ticket.messages.length === 1 ? '' : 's' }}</small>
                </span>
                <span role="cell">
                  <a mat-stroked-button [routerLink]="['/account/support', ticket.supportTicketId]">Open</a>
                </span>
              </div>
            }
          </div>
        }
      }
    </section>
  `
})
export class BuyerSupportPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly router = inject(Router);
  private readonly supportService = inject(BuyerSupportService);

  protected readonly categories: readonly BuyerSupportTicketCategory[] = [
    'OrderIssue',
    'PaymentIssue',
    'ReturnIssue',
    'SellerIssue',
    'ProductIssue',
    'TechnicalIssue',
    'Other'
  ];

  protected readonly tickets = signal<BuyerSupportTicketResponse[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  protected readonly ticketForm = this.formBuilder.group({
    category: ['OrderIssue' as BuyerSupportTicketCategory, [Validators.required]],
    subject: ['', [Validators.required]],
    description: ['', [Validators.required]],
    linkedOrderId: [''],
    linkedSellerId: ['']
  });

  async ngOnInit(): Promise<void> {
    await this.loadTickets();
  }

  protected async createTicket(): Promise<void> {
    if (this.ticketForm.invalid || this.isSaving()) {
      this.ticketForm.markAllAsTouched();
      return;
    }

    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    const value = this.ticketForm.getRawValue();

    try {
      const ticket = await this.supportService.createTicket({
        category: value.category,
        subject: value.subject,
        description: value.description,
        linkedOrderId: emptyToNull(value.linkedOrderId),
        linkedProductId: null,
        linkedSellerId: emptyToNull(value.linkedSellerId),
        linkedPaymentId: null
      });
      this.tickets.set([ticket, ...this.tickets()]);
      this.ticketForm.reset({
        category: 'OrderIssue',
        subject: '',
        description: '',
        linkedOrderId: '',
        linkedSellerId: ''
      });
      this.successMessage.set('Support ticket created.');
      await this.router.navigate(['/account/support', ticket.supportTicketId]);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  protected statusTone(status: string): StatusBadgeTone {
    if (['Open', 'WaitingForBuyer', 'Escalated'].includes(status)) {
      return 'warning';
    }

    if (['Resolved', 'Closed'].includes(status)) {
      return 'success';
    }

    return 'neutral';
  }

  private async loadTickets(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.tickets.set(await this.supportService.listTickets());
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }
}

function emptyToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}
