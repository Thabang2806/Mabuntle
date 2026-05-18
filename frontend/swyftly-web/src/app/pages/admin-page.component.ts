import { Component } from '@angular/core';

@Component({
  selector: 'app-admin-page',
  template: `
    <section class="page">
      <div class="page-header">
        <span class="eyebrow">Admin</span>
        <h1>Admin</h1>
        <p>Internal route for marketplace operations and review workflows.</p>
      </div>
      <div class="route-grid">
        <article class="route-card">
          <span class="status-pill">Foundation</span>
          <h2>Operations console</h2>
          <p>Seller approval, product approval, moderation, and support will start here.</p>
        </article>
      </div>
    </section>
  `
})
export class AdminPageComponent {}
