import { Component } from '@angular/core';

@Component({
  selector: 'app-seller-page',
  template: `
    <section class="page">
      <div class="page-header">
        <span class="eyebrow">Seller</span>
        <h1>Seller</h1>
        <p>Private workspace route for marketplace sellers.</p>
      </div>
      <div class="route-grid">
        <article class="route-card">
          <span class="status-pill">Foundation</span>
          <h2>Seller operations</h2>
          <p>Onboarding, storefront, listings, inventory, and orders will start here.</p>
        </article>
      </div>
    </section>
  `
})
export class SellerPageComponent {}
