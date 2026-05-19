import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'app-checkout-failed-page',
  imports: [MatButtonModule, RouterLink],
  template: `
    <section class="page checkout-result-page">
      <div class="route-card compact-card">
        <span class="status-pill">Checkout issue</span>
        <h1>Checkout could not start</h1>
        <p>Your cart may have changed or stock may no longer be available. Review your cart before trying again.</p>
        <div class="auth-actions">
          <a mat-flat-button routerLink="/cart">Review cart</a>
          <a mat-stroked-button routerLink="/shop">Continue shopping</a>
        </div>
      </div>
    </section>
  `
})
export class CheckoutFailedPageComponent {}
