import { Component } from '@angular/core';

@Component({
  selector: 'app-shop-page',
  template: `
    <section class="page">
      <div class="page-header">
        <span class="eyebrow">Shop</span>
        <h1>Shop</h1>
        <p>Public commerce route for marketplace discovery.</p>
      </div>
      <div class="route-grid">
        <article class="route-card">
          <span class="status-pill">Foundation</span>
          <h2>Catalog surface</h2>
          <p>Category, filter, search, and product listing work will start here.</p>
        </article>
      </div>
    </section>
  `
})
export class ShopPageComponent {}
