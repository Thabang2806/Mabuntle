import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { getApiErrorMessage } from '../auth/api-error';
import { CartResponse } from '../cart/cart.models';
import { CartService } from '../cart/cart.service';

@Component({
  selector: 'app-checkout-page',
  imports: [CurrencyPipe, MatButtonModule, MatFormFieldModule, MatInputModule, ReactiveFormsModule, RouterLink],
  template: `
    <section class="page checkout-page">
      <a class="admin-back-link" routerLink="/cart">Back to cart</a>

      <div class="page-header">
        <span class="eyebrow">Checkout</span>
        <h1>Checkout</h1>
        <p>Start checkout by reserving inventory and creating a pending-payment order.</p>
      </div>

      @if (isLoading()) {
        <div class="route-card">Loading checkout...</div>
      } @else {
        @if (errorMessage()) {
          <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
        }

        @if (!cart()?.items?.length) {
          <div class="route-card compact-card">
            <span class="status-pill">Empty</span>
            <h2>No items to checkout</h2>
            <p>Add products to your cart before checkout.</p>
            <a mat-flat-button routerLink="/shop">Shop products</a>
          </div>
        } @else {
          <div class="checkout-layout">
            <form [formGroup]="shippingForm" class="checkout-form" (ngSubmit)="startCheckout()" novalidate>
              <section class="route-card compact-card">
                <h2>Shipping address</h2>
                <p>This address is a UI placeholder until fulfilment and shipping prompts add persistence.</p>

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

              <section class="route-card compact-card">
                <h2>Payment</h2>
                <p>Payment provider initiation is intentionally a placeholder. The next payment prompts will replace this with provider-backed checkout.</p>
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
                  <span>{{ item.quantity }} × {{ item.productTitle ?? 'Product' }}</span>
                  <strong>{{ item.lineTotal | currency:'ZAR':'symbol-narrow' }}</strong>
                </div>
              }
              <div class="summary-row total">
                <span>Total</span>
                <strong>{{ cart()?.subtotal | currency:'ZAR':'symbol-narrow' }}</strong>
              </div>
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
    try {
      const order = await this.cartService.createOrderFromCart({
        cartId: this.cart()?.cartId ?? null,
        reservationMinutes: null
      });
      await this.router.navigate(['/checkout/success'], {
        queryParams: { orderId: order.orderId }
      });
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      await this.router.navigate(['/checkout/failed']);
    } finally {
      this.isSubmitting.set(false);
    }
  }
}
