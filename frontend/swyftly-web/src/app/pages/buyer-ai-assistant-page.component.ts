import { CurrencyPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { BuyerAiProductCardResponse, BuyerAiShoppingAssistantResponse } from '../buyer/buyer-ai-assistant.models';
import { BuyerAiAssistantService } from '../buyer/buyer-ai-assistant.service';
import { getApiErrorMessage } from '../auth/api-error';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-buyer-ai-assistant-page',
  imports: [
    CurrencyPipe,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    EmptyStateComponent,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page ai-discovery-page">
      <app-page-header
        eyebrow="Shopping assistant"
        heading="Find products with a natural request"
        description="Search the Swyftly catalog using budget, size, colour, occasion, style, material, or beauty needs."
      >
        <a mat-stroked-button routerLink="/shop" pageHeaderActions>Browse shop</a>
      </app-page-header>

      <section class="ai-discovery-shell">
        <form [formGroup]="form" (ngSubmit)="search()" class="ai-discovery-form" novalidate>
          <div class="ai-discovery-form-copy">
            <app-status-badge label="Buyer tool" tone="accent" />
            <h2>Describe what you need</h2>
            <p>Results come from published Swyftly products returned by backend search. The assistant does not reserve stock or create orders.</p>
          </div>

          <mat-form-field appearance="outline">
            <mat-label>What are you looking for?</mat-label>
            <textarea matInput rows="4" formControlName="message" placeholder="Show me a black dress in size medium under R1,500"></textarea>
            @if (form.controls.message.hasError('required')) {
              <mat-error>Enter a shopping request.</mat-error>
            }
          </mat-form-field>

          <div class="ai-example-row" aria-label="Example shopping requests">
            @for (example of examplePrompts; track example) {
              <button mat-stroked-button type="button" (click)="useExamplePrompt(example)">
                {{ example }}
              </button>
            }
          </div>

          <button mat-flat-button type="submit" [disabled]="form.invalid || isLoading()">
            {{ isLoading() ? 'Searching...' : 'Search products' }}
          </button>
        </form>

        <aside class="ai-discovery-guide" aria-label="Assistant guidance">
          <strong>Better prompts include:</strong>
          <span>Category or item type</span>
          <span>Size, colour, or material</span>
          <span>Budget and occasion</span>
          <span>Beauty skin type or concern where relevant</span>
        </aside>
      </section>

      @if (errorMessage()) {
        <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
      }

      @if (isLoading()) {
        <app-ui-alert>Searching published products and extracting intent...</app-ui-alert>
      }

      @if (response()) {
        <section class="ai-result-panel" aria-label="Assistant result summary">
          <div class="ai-result-copy">
            <app-status-badge
              [label]="response()!.intent.isVague ? 'Needs more detail' : 'Intent extracted'"
              [tone]="response()!.intent.isVague ? 'warning' : 'success'"
            />
            <h2>Assistant summary</h2>
            <p>{{ response()!.summary }}</p>
            @if (response()!.intent.clarificationPrompt) {
              <app-ui-alert tone="warning">{{ response()!.intent.clarificationPrompt }}</app-ui-alert>
            }
            @if (response()!.safetyNote) {
              <app-ui-alert>{{ response()!.safetyNote }}</app-ui-alert>
            }
          </div>

          <div class="ai-intent-grid" aria-label="Extracted shopping intent">
            @for (item of intentItems(); track item.label) {
              <span>
                <small>{{ item.label }}</small>
                <strong>{{ item.value }}</strong>
              </span>
            } @empty {
              <span>
                <small>Search text</small>
                <strong>{{ response()!.intent.searchText }}</strong>
              </span>
            }
          </div>
        </section>

        <section class="ai-results-section" aria-label="Assistant product matches">
          <div class="ai-results-header">
            <div>
              <h2>Product matches</h2>
              <p>{{ products().length }} {{ products().length === 1 ? 'match' : 'matches' }} returned by backend search.</p>
            </div>
            <a mat-stroked-button routerLink="/shop">Search manually</a>
          </div>

          @if (products().length > 0) {
            <div class="ai-result-grid">
              @for (product of products(); track product.productId) {
                <a class="ai-product-result-card" [routerLink]="['/product', product.slug]">
                  <div class="ai-product-result-media">
                    @if (product.imageUrl) {
                      <img [src]="product.imageUrl" [alt]="product.title">
                    } @else {
                      <div class="product-card-fallback">
                        <span>Product match</span>
                        <strong>{{ product.title }}</strong>
                      </div>
                    }
                  </div>
                  <div class="ai-product-result-body">
                    <span>{{ product.sellerDisplayName ?? 'Swyftly seller' }}</span>
                    <h3>{{ product.title }}</h3>
                    <strong>{{ product.price | currency:product.currency:'symbol':'1.2-2' }}</strong>
                    <ul>
                      @for (reason of product.matchReasons; track reason) {
                        <li>{{ reason }}</li>
                      }
                    </ul>
                  </div>
                </a>
              }
            </div>
          } @else {
            <app-empty-state
              eyebrow="No matches"
              heading="No product cards to show"
              message="Try a broader category, colour, size, style, or budget."
            >
              <button mat-stroked-button type="button" (click)="useExamplePrompt(examplePrompts[0])">Use an example</button>
            </app-empty-state>
          }
        </section>
      }
    </section>
  `
})
export class BuyerAiAssistantPageComponent {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly assistantService = inject(BuyerAiAssistantService);

  protected readonly response = signal<BuyerAiShoppingAssistantResponse | null>(null);
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly examplePrompts = [
    'Black dress in size medium under R1,500',
    'Minimal gold earrings for everyday wear',
    'Gentle cleanser for oily skin'
  ];

  protected readonly form = this.formBuilder.group({
    message: ['', Validators.required]
  });

  protected products(): BuyerAiProductCardResponse[] {
    return this.response()?.products ?? [];
  }

  protected useExamplePrompt(prompt: string): void {
    this.form.controls.message.setValue(prompt);
    this.form.controls.message.markAsDirty();
    this.form.controls.message.markAsTouched();
  }

  protected intentItems(): Array<{ label: string; value: string }> {
    const intent = this.response()?.intent;
    if (!intent) {
      return [];
    }

    return [
      ['Category', intent.category],
      ['Subcategory', intent.subcategory],
      ['Size', intent.size],
      ['Colour', intent.colour],
      ['Occasion', intent.occasion],
      ['Style', intent.style],
      ['Material', intent.material],
      ['Brand', intent.brand],
      ['Budget', this.formatBudget(intent.budgetMin, intent.budgetMax)],
      ['Beauty skin type', intent.beautySkinType],
      ['Beauty concern', intent.beautyConcern],
      ['Search text', intent.searchText]
    ]
      .filter((entry): entry is [string, string] => Boolean(entry[1]))
      .map(([label, value]) => ({ label, value }));
  }

  protected async search(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.response.set(await this.assistantService.search(this.form.getRawValue()));
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.response.set(null);
    } finally {
      this.isLoading.set(false);
    }
  }

  private formatBudget(min: number | null, max: number | null): string | null {
    if (min !== null && max !== null) {
      return `R${min.toLocaleString()} - R${max.toLocaleString()}`;
    }

    if (min !== null) {
      return `From R${min.toLocaleString()}`;
    }

    if (max !== null) {
      return `Up to R${max.toLocaleString()}`;
    }

    return null;
  }
}
