import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { SellerAdCampaignResponse } from '../seller/seller-ad-campaign.models';
import { SellerAdCampaignService } from '../seller/seller-ad-campaign.service';
import { getApiErrorMessage } from '../auth/api-error';

@Component({
  selector: 'app-seller-ad-campaigns-page',
  imports: [CurrencyPipe, DatePipe, MatButtonModule, RouterLink],
  template: `
    <section class="page seller-products">
      <div class="page-header seller-products-header">
        <div>
          <span class="eyebrow">Seller advertising</span>
          <h1>Ad campaigns</h1>
          <p>Create promoted listing campaigns and monitor review status.</p>
        </div>
        <a mat-flat-button routerLink="/seller/ads/new">New campaign</a>
      </div>

      @if (isLoading()) {
        <div class="route-card">Loading campaigns...</div>
      } @else {
        @if (errorMessage()) {
          <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
        }

        @if (campaigns().length === 0 && !errorMessage()) {
          <div class="route-card">
            <span class="status-pill">Ads</span>
            <h2>No campaigns yet</h2>
            <p>Create a draft campaign once you have published products ready to promote.</p>
          </div>
        } @else {
          <div class="admin-table" role="table" aria-label="Seller ad campaigns">
            <div class="admin-table-row heading" role="row">
              <span role="columnheader">Campaign</span>
              <span role="columnheader">Budget</span>
              <span role="columnheader">Flight</span>
              <span role="columnheader">Status</span>
              <span role="columnheader">Action</span>
            </div>

            @for (campaign of campaigns(); track campaign.adCampaignId) {
              <div class="admin-table-row" role="row">
                <span role="cell">
                  <strong>{{ campaign.name }}</strong>
                  <small>{{ campaign.campaignType }} / {{ campaign.productIds.length }} product{{ campaign.productIds.length === 1 ? '' : 's' }}</small>
                </span>
                <span role="cell">
                  @if (campaign.budget) {
                    <strong>{{ campaign.budget.totalBudget | currency:campaign.budget.currency:'symbol-narrow' }}</strong>
                    <small>{{ campaign.budget.spentAmount | currency:campaign.budget.currency:'symbol-narrow' }} spent</small>
                  } @else {
                    Not set
                  }
                </span>
                <span role="cell">
                  <strong>{{ campaign.startsAtUtc | date:'mediumDate' }}</strong>
                  <small>to {{ campaign.endsAtUtc | date:'mediumDate' }}</small>
                </span>
                <span role="cell">
                  <span class="status-pill">{{ campaign.status }}</span>
                  @if (!campaign.eligibility.isEligible) {
                    <small>Eligibility warnings</small>
                  }
                </span>
                <span role="cell">
                  <a mat-stroked-button [routerLink]="['/seller/ads', campaign.adCampaignId]">Open</a>
                </span>
              </div>
            }
          </div>
        }
      }
    </section>
  `
})
export class SellerAdCampaignsPageComponent implements OnInit {
  private readonly adCampaignService = inject(SellerAdCampaignService);

  protected readonly campaigns = signal<SellerAdCampaignResponse[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.campaigns.set(await this.adCampaignService.listCampaigns());
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }
}
