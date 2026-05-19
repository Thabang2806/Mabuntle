import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { AdminProductService } from './admin-product.service';

describe('AdminProductService', () => {
  let service: AdminProductService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(AdminProductService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('loads pending review products', async () => {
    const promise = service.getPendingReviewProducts();

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/products/pending-review`);
    expect(request.request.method).toBe('GET');
    request.flush([createProductSummary()]);

    const response = await promise;
    expect(response[0].status).toBe('PendingReview');
  });

  it('approves with an optional override reason', async () => {
    const promise = service.approveProduct('product-id', { overrideReason: 'Manual review complete.' });

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/products/product-id/approve`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ overrideReason: 'Manual review complete.' });
    request.flush(createProductDetail({ status: 'Published' }));

    const response = await promise;
    expect(response.status).toBe('Published');
  });

  it('rejects and requests changes with reasons', async () => {
    const rejectPromise = service.rejectProduct('product-id', { reason: 'Policy issue.' });
    const rejectRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/products/product-id/reject`);
    expect(rejectRequest.request.method).toBe('POST');
    expect(rejectRequest.request.body).toEqual({ reason: 'Policy issue.' });
    rejectRequest.flush(createProductDetail({ status: 'Rejected' }));

    const changesPromise = service.requestChanges('product-id', { reason: 'Add measurements.' });
    const changesRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/products/product-id/request-changes`);
    expect(changesRequest.request.method).toBe('POST');
    expect(changesRequest.request.body).toEqual({ reason: 'Add measurements.' });
    changesRequest.flush(createProductDetail({ status: 'ChangesRequested' }));

    await expectAsync(rejectPromise).toBeResolved();
    await expectAsync(changesPromise).toBeResolved();
  });
});

function createProductSummary() {
  return {
    productId: 'product-id',
    sellerId: 'seller-id',
    sellerDisplayName: 'Seller Store',
    sellerVerificationStatus: 'Verified',
    title: 'Summer Dress',
    categoryPath: 'Women > Clothing > Dresses',
    status: 'PendingReview',
    highRiskFlagCount: 0,
    updatedAtUtc: '2026-05-18T12:00:00Z'
  };
}

function createProductDetail(overrides: Record<string, unknown> = {}) {
  return {
    ...createProductSummary(),
    seller: {
      displayName: 'Seller Store',
      contactEmail: 'seller@example.test',
      verificationStatus: 'Verified'
    },
    categoryId: 'category-id',
    brandId: null,
    slug: 'summer-dress',
    shortDescription: 'Short description',
    fullDescription: 'Full description',
    tags: [],
    rejectionReason: null,
    createdAtUtc: '2026-05-18T11:00:00Z',
    publishedAtUtc: null,
    attributes: {},
    variants: [],
    images: [],
    moderationResults: [],
    auditTrail: [],
    ...overrides
  };
}
