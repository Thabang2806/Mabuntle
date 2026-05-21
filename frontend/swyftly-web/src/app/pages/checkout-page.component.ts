import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { getApiErrorMessage } from '../auth/api-error';
import { BuyerPaymentRedirectService, BuyerPaymentService } from '../buyer/buyer-payment.service';
import { CartResponse } from '../cart/cart.models';
import { CartService } from '../cart/cart.service';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-checkout-page',
  imports: [
    CurrencyPipe,
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
    <section class="page checkout-page">
      <a class="admin-back-link" routerLink="/cart">Back to cart</a>

      <app-page-header
        eyebrow="Checkout"
        heading="Checkout"
        description="Confirm delivery details, review the order, and create a reserved order for payment."
      />

      @if (isLoading()) {
        <div class="route-card">Loading checkout...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (!cart()?.items?.length) {
          <app-empty-state
            eyebrow="Empty"
            heading="No items to checkout"
            message="Add products to your cart before checkout."
          >
            <a mat-flat-button routerLink="/shop">Shop products</a>
          </app-empty-state>
        } @else {
          <div class="checkout-steps" aria-label="Checkout progress">
            <app-status-badge label="1. Address" tone="accent" />
            <app-status-badge label="2. Delivery" />
            <app-status-badge label="3. Payment" />
            <app-status-badge label="4. Review" />
          </div>

          <div class="checkout-layout">
            <form [formGroup]="shippingForm" class="checkout-form" (ngSubmit)="startCheckout()" novalidate>
              <section class="route-card compact-card">
                <div class="checkout-section-heading">
                  <app-status-badge label="Step 1" tone="accent" />
                  <h2>Shipping address</h2>
                </div>
                <p>Use the delivery address for this order. Saved addresses can be added when account settings are expanded.</p>

                <div class="form-grid">
                  <mat-form-field appearance="outline">
                    <mat-label>Full name</mat-label>
                    <input matInput formControlName="fullName">
                    @if (shippingForm.controls.fullName.hasError('required')) {
                      <mat-error>Full name is required.</mat-error>
                    }
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Phone</mat-label>
                    <input matInput formControlName="phone">
                    @if (shippingForm.controls.phone.hasError('required')) {
                      <mat-error>Phone is required.</mat-error>
                    }
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Address line 1</mat-label>
                    <input matInput formControlName="addressLine1">
                    @if (shippingForm.controls.addressLine1.hasError('required')) {
                      <mat-error>Address is required.</mat-error>
                    }
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Address line 2</mat-label>
                    <input matInput formControlName="addressLine2">
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>City</mat-label>
                    <input matInput formControlName="city">
                    @if (shippingForm.controls.city.hasError('required')) {
                      <mat-error>City is required.</mat-error>
                    }
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Province</mat-label>
                    <input matInput formControlName="province">
                    @if (shippingForm.controls.province.hasError('required')) {
                      <mat-error>Province is required.</mat-error>
                    }
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Postal code</mat-label>
                    <input matInput formControlName="postalCode">
                    @if (shippingForm.controls.postalCode.hasError('required')) {
                      <mat-error>Postal code is required.</mat-error>
                    }
                  </mat-form-field>
                </div>
              </section>

              <section class="checkout-support-grid">
                <article class="route-card compact-card">
                  <div class="checkout-section-heading">
                    <app-status-badge label="Step 2" />
                    <h2>Delivery</h2>
                  </div>
                  <p>Delivery options and final delivery fees are confirmed as checkout support expands. This order currently keeps delivery context attached to the seller and address.</p>
                </article>

                <article class="route-card compact-card">
                  <div class="checkout-section-heading">
                    <app-status-badge label="Step 3" />
                    <h2>Payment</h2>
                  </div>
                  <p>Your order will be created with reserved stock. Payment confirmation is completed through the marketplace payment flow.</p>
                </article>
              </section>

              <section class="route-card compact-card">
                <div class="checkout-section-heading">
                  <app-status-badge label="Step 4" tone="success" />
                  <h2>Review and start checkout</h2>
                </div>
                <p>Review seller, items, and delivery details before creating the reserved order.</p>
                <button mat-flat-button type="submit" [disabled]="isSubmitting()">
                  {{ isSubmitting() ? 'Starting checkout...' : 'Start checkout' }}
                </button>
              </section>
            </form>

            <aside class="order-summary">
              <h2>Order summary</h2>
              <span class="product-card-seller">{{ cart()?.sellerStoreName ?? 'Seller' }}</span>
              @for (item of cart()?.items; track item.cartItemId) {
                <div class="summary-row">
                  <span>{{ item.quantity }} x {{ item.productTitle ?? 'Product' }}</span>
                  <strong>{{ item.lineTotal | currency:'ZAR':'symbol-narrow' }}</strong>
                </div>
              }
              <div class="summary-row">
                <span>Delivery</span>
                <strong>Confirmed next</strong>
              </div>
              <div class="summary-row">
                <span>Payment status</span>
                <strong>Pending after start</strong>
              </div>
              <div class="summary-row total">
                <span>Estimated total</span>
                <strong>{{ cart()?.subtotal | currency:'ZAR':'symbol-narrow' }}</strong>
              </div>
              <app-ui-alert tone="info">Stock is reserved when checkout starts. If checkout cannot start, return to cart and review item availability.</app-ui-alert>
            </aside>
          </div>
        }
      }
    </section>
  `
})
export class CheckoutPageComponent implements OnInit {
  private readonly cartService = inject(CartService);
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly paymentRedirectService = inject(BuyerPaymentRedirectService);
  private readonly paymentService = inject(BuyerPaymentService);
  private readonly router = inject(Router);

  protected readonly cart = signal<CartResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isSubmitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly shippingForm = this.formBuilder.group({
    fullName: ['', Validators.required],
    phone: ['', Validators.required],
    addressLine1: ['', Validators.required],
    addressLine2: [''],
    city: ['', Validators.required],
    province: ['', Validators.required],
    postalCode: ['', Validators.required]
  });

  async ngOnInit(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);
    try {
      this.cart.set(await this.cartService.getCart());
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.cart.set(null);
    } finally {
      this.isLoading.set(false);
    }
  }

  protected async startCheckout(): Promise<void> {
    this.shippingForm.markAllAsTouched();
    if (this.shippingForm.invalid || !this.cart()?.cartId) {
      return;
    }

    this.isSubmitting.set(true);
    this.errorMessage.set(null);
    let orderId: string | null = null;
    try {
      const order = await this.cartService.createOrderFromCart({
        cartId: this.cart()?.cartId ?? null,
        reservationMinutes: null
      });
      orderId = order.orderId;
      const payment = await this.paymentService.initiatePayment(order.orderId);
      if (payment.checkoutUrl) {
        this.paymentRedirectService.redirect(payment.checkoutUrl);
        return;
      }

      await this.router.navigate(['/checkout/success'], { queryParams: { orderId: order.orderId } });
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      await this.router.navigate(
        ['/checkout/failed'],
        orderId ? { queryParams: { orderId } } : undefined);
    } finally {
      this.isSubmitting.set(false);
    }
  }
}
