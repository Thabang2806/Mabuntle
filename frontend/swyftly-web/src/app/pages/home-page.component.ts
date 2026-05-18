import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'app-home-page',
  imports: [MatButtonModule, RouterLink],
  template: `
    <section class="page">
      <div class="page-header">
        <span class="eyebrow">Marketplace foundation</span>
        <h1>Swyftly</h1>
        <p>Fashion, beauty, jewellery, and accessories in one transactional marketplace.</p>
      </div>

      <div class="route-grid">
        <article class="route-card">
          <div>
            <span class="status-pill">Public</span>
            <h2>Shop</h2>
            <p>Browse-ready shell for category, listing, and product detail pages.</p>
          </div>
          <a mat-button routerLink="/shop">Open shop</a>
        </article>

        <article class="route-card">
          <div>
            <span class="status-pill">Seller</span>
            <h2>Seller workspace</h2>
            <p>Placeholder for onboarding, storefront, products, orders, and payouts.</p>
          </div>
          <a mat-button routerLink="/seller">Open seller</a>
        </article>

        <article class="route-card">
          <div>
            <span class="status-pill">Admin</span>
            <h2>Admin console</h2>
            <p>Placeholder for approvals, moderation, support, and reports.</p>
          </div>
          <a mat-button routerLink="/admin">Open admin</a>
        </article>
      </div>
    </section>
  `
})
export class HomePageComponent {}
