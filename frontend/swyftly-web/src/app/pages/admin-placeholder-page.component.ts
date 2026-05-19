import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'app-admin-placeholder-page',
  imports: [MatButtonModule, RouterLink],
  template: `
    <section class="page">
      <a class="admin-back-link" routerLink="/admin">Back to dashboard</a>

      <div class="page-header">
        <span class="eyebrow">Admin operations</span>
        <h1>{{ title }}</h1>
        <p>{{ description }}</p>
      </div>

      <div class="route-card compact-card">
        <span class="status-pill">Foundation</span>
        <h2>Detail workflow pending</h2>
        <p>This protected landing area is reserved for the dedicated workflow prompt.</p>
      </div>
    </section>
  `
})
export class AdminPlaceholderPageComponent {
  private readonly route = inject(ActivatedRoute);

  protected readonly title = this.route.snapshot.data['title'] ?? 'Admin section';
  protected readonly description = this.route.snapshot.data['description'] ?? 'This section is prepared for a future workflow.';
}
