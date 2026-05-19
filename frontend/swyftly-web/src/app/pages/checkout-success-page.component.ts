import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'app-checkout-success-page',
  imports: [MatButtonModule, RouterLink],
  template: `
    <section class="page checkout-result-page">
      <div class="route-card compact-card">
        <span class="status-pill">Pending payment</span>
        <h1>Checkout started</h1>
        <p>Your order has been created and inventory is reserved while payment support is completed.</p>
        @if (orderId) {
          <p>Order reference: <strong>{{ orderId }}</strong></p>
        }
        <div class="auth-actions">
          <a mat-flat-button routerLink="/account">View account</a>
          <a mat-stroked-button routerLink="/shop">Continue shopping</a>
        </div>
      </div>
    </section>
  `
})
export class CheckoutSuccessPageComponent {
  private readonly route = inject(ActivatedRoute);
  protected readonly orderId = this.route.snapshot.queryParamMap.get('orderId');
}
