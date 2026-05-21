import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { DashboardCardComponent } from '../shared/ui/dashboard-card.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent } from '../shared/ui/status-badge.component';

@Component({
  selector: 'app-home-page',
  imports: [DashboardCardComponent, MatButtonModule, PageHeaderComponent, RouterLink, StatusBadgeComponent],
  template: `
    <section class="market-home">
      <section class="market-home-hero" aria-labelledby="home-title">
        <div class="market-home-hero-copy">
          <app-status-badge label="Fashion marketplace" tone="accent" />
          <h1 id="home-title">Shop fashion, beauty, jewellery, and accessories from trusted sellers.</h1>
          <p>
            Discover curated products, compare details quickly, and move from product discovery to checkout with clear seller and stock information.
          </p>

          <div class="market-home-actions">
            <a mat-flat-button routerLink="/shop">Shop products</a>
            <a mat-stroked-button routerLink="/register/seller">Start selling</a>
          </div>

          <a class="market-search-entry" routerLink="/shop" aria-label="Open product search">
            Search dresses, jewellery, skincare, accessories, and more
          </a>
        </div>

        <div class="market-home-showcase" aria-label="Featured marketplace categories">
          @for (item of showcaseItems; track item.title) {
            <article class="market-showcase-card" [class.market-showcase-card--large]="item.large">
              <span>{{ item.kicker }}</span>
              <strong>{{ item.title }}</strong>
              <small>{{ item.detail }}</small>
            </article>
          }
        </div>
      </section>

      <section class="page market-home-section">
        <app-page-header
          eyebrow="Shop by edit"
          heading="Start with what you need"
          description="Swyftly keeps discovery simple: find the product, check the seller, then move to cart when the details are right."
        />

        <div class="market-category-grid">
          @for (category of categoryCards; track category.title) {
            <a class="market-category-card" routerLink="/shop">
              <app-status-badge [label]="category.kicker" tone="accent" />
              <h2>{{ category.title }}</h2>
              <p>{{ category.description }}</p>
            </a>
          }
        </div>
      </section>

      <section class="page market-home-section">
        <app-page-header
          eyebrow="Marketplace trust"
          heading="Built for careful buying and selling"
          description="The product experience should make quality, seller status, stock, checkout, and support paths easy to understand."
        />

        <div class="route-grid">
          @for (item of trustCards; track item.heading) {
            <app-dashboard-card [eyebrow]="item.eyebrow" [heading]="item.heading" [description]="item.description">
              <a mat-button [routerLink]="item.route">{{ item.action }}</a>
            </app-dashboard-card>
          }
        </div>
      </section>

      <section class="page market-home-section">
        <div class="market-home-seller-band">
          <div>
            <app-status-badge label="Sellers" tone="accent" />
            <h2>List products with guided seller tools.</h2>
            <p>
              Sellers can create product drafts, add variants and stock, review AI suggestions, and submit listings for marketplace review.
            </p>
          </div>
          <div class="market-home-actions">
            <a mat-flat-button routerLink="/register/seller">Create seller account</a>
            <a mat-stroked-button routerLink="/login">Seller sign in</a>
          </div>
        </div>
      </section>
    </section>
  `
})
export class HomePageComponent {
  protected readonly showcaseItems = [
    { kicker: 'New edit', title: 'Occasionwear', detail: 'Dresses, sets, and finishing pieces', large: true },
    { kicker: 'Daily', title: 'Accessories', detail: 'Bags, belts, and jewellery', large: false },
    { kicker: 'Beauty', title: 'Skincare', detail: 'Care routines and essentials', large: false }
  ];

  protected readonly categoryCards = [
    {
      kicker: 'Fashion',
      title: 'Clothing',
      description: 'Browse everyday staples, occasion looks, and seasonal pieces from marketplace sellers.'
    },
    {
      kicker: 'Finish',
      title: 'Jewellery and accessories',
      description: 'Find pieces that complete the outfit, from statement jewellery to practical extras.'
    },
    {
      kicker: 'Care',
      title: 'Beauty',
      description: 'Explore beauty products with clear seller, stock, and product detail context.'
    }
  ];

  protected readonly trustCards = [
    {
      eyebrow: 'Buyers',
      heading: 'Product detail first',
      description: 'Product pages surface variants, seller information, stock state, and checkout actions in one place.',
      action: 'Browse shop',
      route: '/shop'
    },
    {
      eyebrow: 'Sellers',
      heading: 'Guided listing flow',
      description: 'Seller tools support onboarding, product drafts, images, variants, stock, and listing review.',
      action: 'Start selling',
      route: '/register/seller'
    },
    {
      eyebrow: 'Support',
      heading: 'Operational review',
      description: 'Admin workflows review sellers, products, campaigns, reports, audit history, and finance operations.',
      action: 'Sign in',
      route: '/login'
    }
  ];
}
