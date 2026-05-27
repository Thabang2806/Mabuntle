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
    analyticsService = jasmine.createSpyObj<SellerAnalyticsService>('SellerAnalyticsService', [
      'getSummary',
      'getPerformance',
      'getCsvExportUrl'
    ]);
    analyticsService.getSummary.and.resolveTo(createSummary());
    analyticsService.getPerformance.and.resolveTo(createPerformance());
    analyticsService.getCsvExportUrl.and.callFake(report => `/api/seller/analytics/export.csv?report=${report}`);

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
    expect(compiled.textContent).toContain('Product performance');
    expect(compiled.textContent).toContain('Sales trend');
    expect(compiled.textContent).toContain('Seller One Product');
    expect(compiled.textContent).toContain('Customer care');
    expect(compiled.textContent).toContain('AI usage');
    expect(compiled.querySelector('a[href*="report=Products"]')).not.toBeNull();
  });

  it('reloads performance data when filters are applied', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const form = fixture.nativeElement.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(analyticsService.getPerformance).toHaveBeenCalled();
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

function createPerformance() {
  return {
    sellerId: 'seller-id',
    fromUtc: '2026-05-01T00:00:00.000Z',
    toUtc: '2026-05-31T00:00:00.000Z',
    bucket: 'Day' as const,
    salesTrend: [{
      periodStartUtc: '2026-05-01T00:00:00.000Z',
      periodEndUtc: '2026-05-02T00:00:00.000Z',
      orderCount: 1,
      grossSales: 998,
      refundedAmount: 100,
      netSales: 898,
      unitsSold: 2
    }],
    productPerformance: [{
      productId: 'product-id',
      productTitle: 'Seller One Product',
      productSlug: 'seller-one-product',
      status: 'Published',
      unitsSold: 2,
      grossSales: 998,
      refundedAmount: 100,
      returnCount: 1,
      returnRate: 0.5,
      stockQuantity: 3,
      reservedQuantity: 0,
      availableQuantity: 3
    }],
    inventoryPerformance: [{
      productId: 'product-id',
      productTitle: 'Seller One Product',
      productVariantId: 'variant-id',
      sku: 'SKU-1',
      barcode: 'BARCODE-1',
      size: 'M',
      colour: 'Black',
      status: 'Active',
      stockQuantity: 3,
      reservedQuantity: 0,
      availableQuantity: 3,
      isLowStock: true,
      isOutOfStock: false,
      lastMovementAtUtc: '2026-05-01T00:00:00.000Z'
    }],
    adPerformance: [{
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
    }],
    customerCareSummary: {
      returnCount: 1,
      openReturnCount: 1,
      refundCount: 1,
      refundedAmount: 100,
      supportTicketCount: 1,
      openSupportTicketCount: 1,
      disputeCount: 1,
      activeDisputeCount: 1
    }
  };
}
