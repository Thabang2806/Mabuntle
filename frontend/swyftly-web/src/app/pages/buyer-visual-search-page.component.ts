import { CurrencyPipe, PercentPipe } from '@angular/common';
import { Component, signal, inject } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { getApiErrorMessage } from '../auth/api-error';
import { BuyerVisualSearchProductCardResponse, BuyerVisualSearchResponse } from '../buyer/buyer-visual-search.models';
import { BuyerVisualSearchService } from '../buyer/buyer-visual-search.service';

@Component({
  selector: 'app-buyer-visual-search-page',
  imports: [
    CurrencyPipe,
    PercentPipe,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    ReactiveFormsModule,
    RouterLink
  ],
  template: `
    <section class="page shop-page">
      <div class="page-header">
        <span class="eyebrow">Visual search</span>
        <h1>Search from an image</h1>
        <p>Upload a product image or paste a reference so Swyftly can extract visual attributes and match real products.</p>
      </div>

      <form [formGroup]="form" (ngSubmit)="search()" class="route-card ai-assistant-form" novalidate>
        <label class="file-control">
          <span>Image upload</span>
          <input type="file" accept="image/png,image/jpeg,image/webp" (change)="onFileSelected($event)">
        </label>

        <mat-form-field appearance="outline">
          <mat-label>Image reference</mat-label>
          <input matInput formControlName="imageReference" placeholder="black formal maxi dress flatlay">
        </mat-form-field>

        @if (selectedFileName()) {
          <p class="muted-copy">Selected: {{ selectedFileName() }}</p>
        }

        <button mat-flat-button type="submit" [disabled]="isLoading()">
          {{ isLoading() ? 'Searching...' : 'Search visually' }}
        </button>
      </form>

      @if (errorMessage()) {
        <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
      }

      @if (response()) {
        <div class="route-card compact-card">
          <span class="status-pill">Confidence {{ response()!.attributes.confidence | percent:'1.0-0' }}</span>
          <p>{{ response()!.summary }}</p>
          <p>{{ response()!.imageRetentionNote }}</p>
          <div class="attribute-list">
            @for (attribute of extractedAttributes(); track attribute.label) {
              <span>{{ attribute.label }}: {{ attribute.value }}</span>
            }
          </div>
          @for (warning of response()!.attributes.warnings; track warning) {
            <p>{{ warning }}</p>
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
              <p>Try a clearer item photo or a more specific reference.</p>
            </div>
          }
        </div>
      }
    </section>
  `
})
export class BuyerVisualSearchPageComponent {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly visualSearchService = inject(BuyerVisualSearchService);

  protected readonly response = signal<BuyerVisualSearchResponse | null>(null);
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly selectedFileName = signal<string | null>(null);

  protected readonly form = this.formBuilder.group({
    imageReference: ['']
  });

  private imageDataBase64: string | null = null;
  private contentType: string | null = null;

  protected products(): BuyerVisualSearchProductCardResponse[] {
    return this.response()?.products ?? [];
  }

  protected extractedAttributes(): Array<{ label: string; value: string }> {
    const attributes = this.response()?.attributes;
    if (!attributes) {
      return [];
    }

    return [
      ['Category', attributes.category],
      ['Colour', attributes.colour],
      ['Style', attributes.style],
      ['Shape', attributes.shape],
      ['Pattern', attributes.pattern],
      ['Material guess', attributes.materialGuess]
    ]
      .filter((entry): entry is [string, string] => Boolean(entry[1]))
      .map(([label, value]) => ({ label, value }));
  }

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    this.imageDataBase64 = null;
    this.contentType = null;
    this.selectedFileName.set(null);

    if (!file) {
      return;
    }

    this.contentType = file.type;
    this.selectedFileName.set(file.name);

    const reader = new FileReader();
    reader.onload = () => {
      const result = typeof reader.result === 'string' ? reader.result : '';
      this.imageDataBase64 = result.includes(',') ? result.split(',')[1] : result;
    };
    reader.readAsDataURL(file);
  }

  protected async search(): Promise<void> {
    const rawReference = this.form.controls.imageReference.value.trim();
    if (!rawReference && !this.imageDataBase64) {
      this.errorMessage.set('Upload an image or enter an image reference.');
      this.response.set(null);
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.response.set(await this.visualSearchService.search({
        imageReference: rawReference || null,
        imageDataBase64: this.imageDataBase64,
        fileName: this.selectedFileName(),
        contentType: this.contentType
      }));
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.response.set(null);
    } finally {
      this.isLoading.set(false);
    }
  }
}
