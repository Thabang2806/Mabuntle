import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { getApiErrorMessage } from '../auth/api-error';
import { SellerOrderResult } from '../seller/seller-order.models';
import { SellerOrderService } from '../seller/seller-order.service';
import { SellerWorkspaceNavComponent } from '../seller/seller-workspace-nav.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-seller-order-detail-page',
  imports: [
    CurrencyPipe,
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

      <a class="admin-back-link" routerLink="/seller/orders">Back to orders</a>

      <app-page-header
        eyebrow="Seller operations"
        [heading]="order() ? 'Order ' + order()!.orderId : 'Order'"
        description="Manage manual fulfilment actions for this seller order."
      >
        <div pageHeaderActions>
          @if (order()) {
            <app-status-badge [label]="order()!.status" [tone]="statusTone(order()!.status)" />
          }
        </div>
      </app-page-header>

      @if (isLoading()) {
        <div class="route-card">Loading order...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (successMessage()) {
          <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
        }

        @if (order()) {
          <div class="seller-detail-grid">
            <section class="seller-panel">
              <h2>Order summary</h2>
              <dl class="seller-facts">
                <div><dt>Items subtotal</dt><dd>{{ order()!.itemsSubtotal | currency:'ZAR':'symbol-narrow' }}</dd></div>
                <div><dt>Shipping</dt><dd>{{ order()!.shippingAmount | currency:'ZAR':'symbol-narrow' }}</dd></div>
                <div><dt>Platform fee</dt><dd>{{ order()!.platformFeeAmount | currency:'ZAR':'symbol-narrow' }}</dd></div>
                <div><dt>Discount</dt><dd>{{ order()!.discountAmount | currency:'ZAR':'symbol-narrow' }}</dd></div>
                <div><dt>Total</dt><dd>{{ order()!.totalAmount | currency:'ZAR':'symbol-narrow' }}</dd></div>
              </dl>
            </section>

            <section class="seller-panel">
              <h2>Fulfilment actions</h2>
              <p>Use these controls for manual marketplace fulfilment. Carrier automation is not connected yet.</p>
              <div class="seller-action-row">
                <button mat-flat-button type="button" [disabled]="isActing()" (click)="markProcessing()">Mark processing</button>
                <button mat-stroked-button type="button" [disabled]="isActing()" (click)="markShipped()">Mark shipped</button>
                <button mat-stroked-button type="button" [disabled]="isActing() || !canMarkDelivered()" (click)="markDelivered()">Mark delivered</button>
              </div>

              <form [formGroup]="trackingForm" (ngSubmit)="addTracking()" class="seller-form-grid" novalidate>
                <mat-form-field appearance="outline">
                  <mat-label>Carrier name</mat-label>
                  <input matInput formControlName="carrierName" />
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>Tracking number</mat-label>
                  <input matInput formControlName="trackingNumber" />
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>Tracking URL</mat-label>
                  <input matInput formControlName="trackingUrl" />
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>Note</mat-label>
                  <textarea matInput rows="3" formControlName="note"></textarea>
                </mat-form-field>

                <button mat-flat-button type="submit" [disabled]="isActing()">Add tracking</button>
              </form>
            </section>
          </div>

          <section class="seller-panel">
            <h2>Items</h2>
            <div class="seller-item-list">
              @for (item of order()!.items; track item.orderItemId) {
                <div class="seller-item-row">
                  <span>
                    <strong>{{ item.productTitle ?? 'Untitled product' }}</strong>
                    <small>SKU {{ item.sku }} - {{ item.size }} / {{ item.colour }}</small>
                  </span>
                  <span>{{ item.quantity }} x {{ item.unitPrice | currency:'ZAR':'symbol-narrow' }}</span>
                  <strong>{{ item.lineTotal | currency:'ZAR':'symbol-narrow' }}</strong>
                </div>
              }
            </div>
          </section>

          <div class="seller-detail-grid">
            <section class="seller-panel">
              <h2>Status history</h2>
              <div class="seller-timeline">
                @for (history of order()!.statusHistory; track history.statusHistoryId) {
                  <div>
                    <app-status-badge [label]="history.newStatus" [tone]="statusTone(history.newStatus)" />
                    <span>{{ history.changedAtUtc | date:'medium' }}</span>
                    @if (history.reason) {
                      <small>{{ history.reason }}</small>
                    }
                  </div>
                }
              </div>
            </section>

            <section class="seller-panel">
              <h2>Shipments</h2>
              @if (order()!.shipments.length === 0) {
                <p>No shipment has been created for this order yet.</p>
              } @else {
                <div class="seller-timeline">
                  @for (shipment of order()!.shipments; track shipment.shipmentId) {
                    <div>
                      <app-status-badge [label]="shipment.status" [tone]="statusTone(shipment.status)" />
                      <span>{{ shipment.carrierName ?? 'Carrier not set' }}</span>
                      @if (shipment.trackingNumber) {
                        <small>{{ shipment.trackingNumber }}</small>
                      }
                    </div>
                  }
                </div>
              }
            </section>
          </div>
        }
      }
    </section>
  `
})
export class SellerOrderDetailPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly orderService = inject(SellerOrderService);
  private readonly route = inject(ActivatedRoute);

  protected readonly order = signal<SellerOrderResult | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isActing = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  protected readonly trackingForm = this.formBuilder.group({
    carrierName: ['', [Validators.required]],
    trackingNumber: ['', [Validators.required]],
    trackingUrl: [''],
    note: ['']
  });

  async ngOnInit(): Promise<void> {
    await this.loadOrder();
  }

  protected async markProcessing(): Promise<void> {
    await this.runAction(
      () => this.orderService.markProcessing(this.orderId()),
      'Order marked as processing.');
  }

  protected async markShipped(): Promise<void> {
    await this.runAction(
      () => this.orderService.markShipped(this.orderId()),
      'Order marked as shipped.');
  }

  protected async markDelivered(): Promise<void> {
    await this.runAction(
      () => this.orderService.markDelivered(this.orderId()),
      'Order marked as delivered.');
  }

  protected canMarkDelivered(): boolean {
    const order = this.order();
    if (!order || order.status !== 'Shipped') {
      return false;
    }

    const latestShipment = [...order.shipments]
      .sort((left, right) => (left.shippedAtUtc ?? '').localeCompare(right.shippedAtUtc ?? ''))
      .at(-1);
    return latestShipment?.status === 'InTransit';
  }

  protected async addTracking(): Promise<void> {
    if (this.trackingForm.invalid || this.isActing()) {
      this.trackingForm.markAllAsTouched();
      return;
    }

    const value = this.trackingForm.getRawValue();
    await this.runAction(
      () => this.orderService.addTracking(this.orderId(), {
        carrierName: value.carrierName,
        trackingNumber: value.trackingNumber,
        trackingUrl: emptyToNull(value.trackingUrl),
        note: emptyToNull(value.note)
      }),
      'Tracking added.');

    if (!this.errorMessage()) {
      this.trackingForm.reset();
    }
  }

  protected statusTone(status: string): StatusBadgeTone {
    if (['Paid', 'Processing', 'ReadyToShip', 'AwaitingFulfilment'].includes(status)) {
      return 'accent';
    }

    if (['Shipped', 'Delivered', 'Completed', 'InTransit'].includes(status)) {
      return 'success';
    }

    if (['Cancelled', 'Refunded', 'Disputed', 'Failed'].includes(status)) {
      return 'danger';
    }

    return 'neutral';
  }

  private async loadOrder(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.order.set(await this.orderService.getOrder(this.orderId()));
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  private async runAction(action: () => Promise<SellerOrderResult>, successMessage: string): Promise<void> {
    if (this.isActing()) {
      return;
    }

    this.isActing.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      this.order.set(await action());
      this.successMessage.set(successMessage);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isActing.set(false);
    }
  }

  private orderId(): string {
    return this.route.snapshot.paramMap.get('orderId') ?? '';
  }
}

function emptyToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}
