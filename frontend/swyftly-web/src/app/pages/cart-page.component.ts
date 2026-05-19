import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { getApiErrorMessage } from '../auth/api-error';
import { CartResponse } from '../cart/cart.models';
import { CartService } from '../cart/cart.service';

@Component({
  selector: 'app-cart-page',
  imports: [CurrencyPipe, FormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, RouterLink],
  template: `
    <section class="page cart-page">
      <div class="page-header cart-header">
        <div>
          <span class="eyebrow">Cart</span>
          <h1>Cart</h1>
          <p>Review quantities before checkout. This MVP cart supports products from one seller at a time.</p>
        </div>
        <a mat-stroked-button routerLink="/shop">Continue shopping</a>
      </div>

      @if (isLoading()) {
        <div class="route-card">Loading cart...</div>
      } @else {
        @if (errorMessage()) {
          <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
        }

        @if (!cart()?.items?.length) {
          <div class="route-card compact-card">
            <span class="status-pill">Empty</span>
            <h2>Your cart is empty</h2>
            <p>Products you add from the shop will appear here.</p>
            <a mat-flat-button routerLink="/shop">Shop products</a>
          </div>
        } @else {
          <div class="cart-layout">
            <div class="cart-items">
              <div class="single-seller-notice">
                <strong>{{ cart()?.sellerStoreName ?? 'Single seller checkout' }}</strong>
                <span>Only items from this seller can be checked out together.</span>
              </div>

              @for (item of cart()?.items; track item.cartItemId) {
                <article class="cart-item">
                  <div>
                    <strong>{{ item.productTitle ?? 'Product' }}</strong>
                    <span>{{ item.size }} / {{ item.colour }} · {{ item.sku }}</span>
                    <small>{{ item.unitPrice | currency:'ZAR':'symbol-narrow' }} each</small>
                  </div>

                  <mat-form-field appearance="outline">
                    <mat-label>Qty</mat-label>
                    <input
                      matInput
                      type="number"
                      min="1"
                      [ngModel]="item.quantity"
                      (ngModelChange)="setLocalQuantity(item.cartItemId, $event)"
                      [disabled]="updatingItemId() === item.cartItemId"
                    >
                  </mat-form-field>

                  <strong>{{ item.lineTotal | currency:'ZAR':'symbol-narrow' }}</strong>

                  <div class="cart-item-actions">
                    <button
                      mat-stroked-button
                      type="button"
                      [disabled]="updatingItemId() === item.cartItemId"
                      (click)="updateQuantity(item.cartItemId)"
                    >
                      Update
                    </button>
                    <button
                      mat-button
                      type="button"
                      [disabled]="updatingItemId() === item.cartItemId"
                      (click)="removeItem(item.cartItemId)"
                    >
                      Remove
                    </button>
                  </div>
                </article>
              }
            </div>

            <aside class="order-summary">
              <h2>Order summary</h2>
              <div class="summary-row">
                <span>Items</span>
                <strong>{{ cart()?.totalQuantity }}</strong>
              </div>
              <div class="summary-row">
                <span>Subtotal</span>
                <strong>{{ cart()?.subtotal | currency:'ZAR':'symbol-narrow' }}</strong>
              </div>
              <p>Shipping, discounts, fees, and payment confirmation are handled in later checkout prompts.</p>
              <a mat-flat-button routerLink="/checkout" [class.disabled-link]="!cart()?.items?.length">Checkout</a>
            </aside>
          </div>
        }
      }
    </section>
  `
})
export class CartPageComponent implements OnInit {
  private readonly cartService = inject(CartService);

  protected readonly cart = signal<CartResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly updatingItemId = signal<string | null>(null);
  private readonly localQuantities = new Map<string, number>();

  async ngOnInit(): Promise<void> {
    await this.loadCart();
  }

  protected setLocalQuantity(cartItemId: string, rawValue: string | number): void {
    const quantity = Number(rawValue);
    if (Number.isFinite(quantity)) {
      this.localQuantities.set(cartItemId, quantity);
    }
  }

  protected async updateQuantity(cartItemId: string): Promise<void> {
    const currentItem = this.cart()?.items.find(item => item.cartItemId === cartItemId);
    const quantity = this.localQuantities.get(cartItemId) ?? currentItem?.quantity ?? 0;
    if (quantity <= 0) {
      this.errorMessage.set('Quantity must be at least 1.');
      return;
    }

    this.updatingItemId.set(cartItemId);
    this.errorMessage.set(null);
    try {
      this.cart.set(await this.cartService.updateItem(cartItemId, { quantity }));
      this.localQuantities.delete(cartItemId);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.updatingItemId.set(null);
    }
  }

  protected async removeItem(cartItemId: string): Promise<void> {
    this.updatingItemId.set(cartItemId);
    this.errorMessage.set(null);
    try {
      this.cart.set(await this.cartService.removeItem(cartItemId));
      this.localQuantities.delete(cartItemId);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.updatingItemId.set(null);
    }
  }

  private async loadCart(): Promise<void> {
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
}
