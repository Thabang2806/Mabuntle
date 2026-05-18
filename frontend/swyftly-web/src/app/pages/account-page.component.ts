import { Component } from '@angular/core';

@Component({
  selector: 'app-account-page',
  template: `
    <section class="page">
      <div class="page-header">
        <span class="eyebrow">Account</span>
        <h1>Account</h1>
        <p>Private route for buyer account and order history workflows.</p>
      </div>
      <div class="route-grid">
        <article class="route-card">
          <span class="status-pill">Foundation</span>
          <h2>Buyer account</h2>
          <p>Registration, profile, wishlist, orders, and returns will start here.</p>
        </article>
      </div>
    </section>
  `
})
export class AccountPageComponent {}
