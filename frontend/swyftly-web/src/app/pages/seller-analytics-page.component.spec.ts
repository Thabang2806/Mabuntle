import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { SellerAnalyticsSummaryResponse } from '../seller/seller-analytics.models';
import { SellerAnalyticsService } from '../seller/seller-analytics.service';
import { SellerAnalyticsPageComponent } from './seller-analytics-page.component';

describe('SellerAnalyticsPageComponent', () => {
  let fixture: ComponentFixture<SellerAnalyticsPageComponent>;
  let analyticsService: jasmine.SpyObj<SellerAnalyticsService>;

  beforeEach(async () => {
    analyticsService = jasmine.createSpyObj<SellerAnalyticsService>('SellerAnalyticsService', ['getSummary']);
    analyticsService.getSummary.and.resolveTo(createSummary());

    await TestBed.configureTestingModule({
      imports: [SellerAnalyticsPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: SellerAnalyticsService, useValue: analyticsService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SellerAnalyticsPageComponent);
  });

  it('loads seller analytics cards and tables', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Total sales');
    expect(compiled.textContent).toContain('Top products');
    expect(compiled.textContent).toContain('Seller One Product');
    expect(compiled.textContent).toContain('Ad campaign performance');
    expect(compiled.textContent).toContain('AI usage');
    expect(compiled.textContent).toContain('Products improved');
  });
});

function createSummary(): SellerAnalyticsSummaryResponse {
  return {
    sellerId: 'seller-id',
    totalSales: 998,
    orderCount: 1,
    averageOrderValue: 998,
    conversionRatePlaceholder: 0,
    productsSold: 2,
    totalRefunded: 100,
    refundRate: 1,
    returnRate: 1,
    topProducts: [{
      productId: 'product-id',
      productTitle: 'Seller One Product',
      quantitySold: 2,
      revenue: 998
    }],
    lowStockProducts: [{
      productId: 'product-id',
      title: 'Seller One Product',
      status: 'Published',
      availableQuantity: 3,
      lowStockVariantCount: 1
    }],
    adPerformance: {
      campaignCount: 1,
      impressions: 100,
      clicks: 5,
      clickThroughRate: 0.05,
      spend: 25,
      ordersGenerated: 1,
      revenueGenerated: 499,
      topCampaigns: [{
        adCampaignId: 'campaign-id',
        name: 'Launch campaign',
        status: 'Active',
        impressions: 100,
        clicks: 5,
        clickThroughRate: 0.05,
        spend: 25,
        ordersGenerated: 1,
        revenueGenerated: 499,
        returnOnAdSpend: 19.96
      }]
    },
    aiUsage: {
      requests: 3,
      successfulRequests: 2,
      failedRequests: 1,
      estimatedCost: 0.02,
      averageLatencyMs: 100,
      suggestionsGenerated: 2,
      suggestionsAccepted: 1,
      suggestionAcceptanceRate: 0.5,
      productsImprovedWithAi: 1,
      averageListingQualityScore: 70,
      averageQualityScoreImprovement: null,
      qualityScoreImprovementNote: 'Pre-AI baseline quality scores are not captured yet.',
      fieldValuesAccepted: 1,
      fieldValuesEdited: 1
    }
  };
}
