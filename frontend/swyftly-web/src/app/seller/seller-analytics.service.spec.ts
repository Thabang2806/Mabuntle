import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { SellerAnalyticsService } from './seller-analytics.service';

describe('SellerAnalyticsService', () => {
  let service: SellerAnalyticsService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(SellerAnalyticsService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('loads the seller analytics summary', async () => {
    const promise = service.getSummary();

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/analytics/summary`);
    expect(request.request.method).toBe('GET');
    request.flush(createSummary());

    const response = await promise;
    expect(response.totalSales).toBe(998);
    expect(response.adPerformance.clicks).toBe(5);
  });
});

function createSummary() {
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
    topProducts: [],
    lowStockProducts: [],
    adPerformance: {
      campaignCount: 1,
      impressions: 100,
      clicks: 5,
      clickThroughRate: 0.05,
      spend: 25,
      ordersGenerated: 1,
      revenueGenerated: 499,
      topCampaigns: []
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
