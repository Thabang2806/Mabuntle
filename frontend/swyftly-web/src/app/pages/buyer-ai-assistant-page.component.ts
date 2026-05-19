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

@Component({
  selector: 'app-buyer-ai-assistant-page',
  imports: [
    CurrencyPipe,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    ReactiveFormsModule,
    RouterLink
  ],
  template: `
    <section class="page shop-page">
      <div class="page-header">
        <span class="eyebrow">Shopping assistant</span>
        <h1>Find products with a natural request</h1>
        <p>Search the Swyftly catalog using budget, size, colour, occasion, style, or beauty needs.</p>
      </div>

      <form [formGroup]="form" (ngSubmit)="search()" class="route-card ai-assistant-form" novalidate>
        <mat-form-field appearance="outline">
          <mat-label>What are you looking for?</mat-label>
          <textarea matInput rows="3" formControlName="message" placeholder="Show me a black dress in size medium under R1,500"></textarea>
          @if (form.controls.message.hasError('required')) {
            <mat-error>Enter a shopping request.</mat-error>
          }
        </mat-form-field>

        <button mat-flat-button type="submit" [disabled]="form.invalid || isLoading()">
          {{ isLoading() ? 'Searching...' : 'Search' }}
        </button>
      </form>

      @if (errorMessage()) {
        <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
      }

      @if (response()) {
        <div class="route-card compact-card">
          <span class="status-pill">{{ response()!.intent.isVague ? 'Needs detail' : 'Intent extracted' }}</span>
          <p>{{ response()!.summary }}</p>
          @if (response()!.intent.clarificationPrompt) {
            <p>{{ response()!.intent.clarificationPrompt }}</p>
          }
          @if (response()!.safetyNote) {
            <p>{{ response()!.safetyNote }}</p>
          }
        </div>

        <div class="product-grid">
          @for (product of products(); track product.productId) {
            <a class="product-card" [routerLink]="['/product', product.slug]">
              @if (product.imageUrl) {
                <img [src]="product.imageUrl" [alt]="product.title">
              }
              <div class="product-card-body">
                <span class="status-pill">{{ product.sellerDisplayName ?? 'Swyftly seller' }}</span>
                <h2>{{ product.title }}</h2>
                <strong>{{ product.price | currency:product.currency:'symbol':'1.2-2' }}</strong>
                <ul>
                  @for (reason of product.matchReasons; track reason) {
                    <li>{{ reason }}</li>
                  }
                </ul>
              </div>
            </a>
          } @empty {
            <div class="route-card compact-card">
              <span class="status-pill">No matches</span>
              <h2>No product cards to show</h2>
              <p>Try a broader category, colour, size, or budget.</p>
            </div>
          }
        </div>
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

  protected readonly form = this.formBuilder.group({
    message: ['', Validators.required]
  });

  protected products(): BuyerAiProductCardResponse[] {
    return this.response()?.products ?? [];
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
}
