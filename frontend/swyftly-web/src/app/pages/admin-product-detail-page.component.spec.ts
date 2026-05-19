import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { AdminProductDetailResponse } from '../admin/admin-product.models';
import { AdminProductService } from '../admin/admin-product.service';
import { AdminProductDetailPageComponent } from './admin-product-detail-page.component';

describe('AdminProductDetailPageComponent', () => {
  let fixture: ComponentFixture<AdminProductDetailPageComponent>;
  let adminProductService: jasmine.SpyObj<AdminProductService>;

  beforeEach(async () => {
    adminProductService = jasmine.createSpyObj<AdminProductService>(
      'AdminProductService',
      ['getProduct', 'approveProduct', 'rejectProduct', 'requestChanges']);
    adminProductService.getProduct.and.resolveTo(createProductDetail());
    adminProductService.approveProduct.and.resolveTo(createProductDetail({
      status: 'Published',
      auditTrail: [{
        id: 'audit-approved',
        actionType: 'ProductApproved',
        actorUserId: 'admin-id',
        actorRole: 'Admin',
        reason: 'Manual review complete.',
        createdAtUtc: '2026-05-18T12:30:00Z'
      }]
    }));

    await TestBed.configureTestingModule({
      imports: [AdminProductDetailPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ productId: 'product-id' })
            }
          }
        },
        { provide: AdminProductService, useValue: adminProductService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminProductDetailPageComponent);
  });

  it('loads product review detail with images, attributes, and risk flags', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Summer Dress');
    expect(compiled.textContent).toContain('Seller Store');
    expect(compiled.textContent).toContain('Black');
    expect(compiled.textContent).toContain('Potential counterfeit wording detected.');
  });

  it('requires an override reason before approving a high-risk product', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const approveForm = (fixture.nativeElement as HTMLElement).querySelector('form') as HTMLFormElement;
    approveForm.dispatchEvent(new Event('submit'));

    await fixture.whenStable();
    fixture.detectChanges();

    expect(adminProductService.approveProduct).not.toHaveBeenCalled();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Override reason is required');
  });

  it('approves a high-risk product when an override reason is provided', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const textarea = (fixture.nativeElement as HTMLElement).querySelector('textarea[formControlName="overrideReason"]') as HTMLTextAreaElement;
    textarea.value = 'Manual review complete.';
    textarea.dispatchEvent(new Event('input'));

    const approveForm = (fixture.nativeElement as HTMLElement).querySelector('form') as HTMLFormElement;
    approveForm.dispatchEvent(new Event('submit'));

    await fixture.whenStable();
    fixture.detectChanges();

    expect(adminProductService.approveProduct).toHaveBeenCalledWith('product-id', { overrideReason: 'Manual review complete.' });
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Product approved.');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('ProductApproved');
  });
});

function createProductDetail(overrides: Partial<AdminProductDetailResponse> = {}): AdminProductDetailResponse {
  return {
    productId: 'product-id',
    sellerId: 'seller-id',
    seller: {
      displayName: 'Seller Store',
      contactEmail: 'seller@example.test',
      verificationStatus: 'Verified'
    },
    categoryId: 'category-id',
    categoryPath: 'Women > Clothing > Dresses',
    brandId: null,
    title: 'Summer Dress',
    slug: 'summer-dress',
    shortDescription: 'A lightweight summer dress.',
    fullDescription: 'A lightweight summer dress with a relaxed fit.',
    tags: ['summer'],
    status: 'NeedsAdminReview',
    rejectionReason: null,
    createdAtUtc: '2026-05-18T11:00:00Z',
    updatedAtUtc: '2026-05-18T12:00:00Z',
    publishedAtUtc: null,
    attributes: {
      colour: '"Black"',
      size: '"M"'
    },
    variants: [{
      variantId: 'variant-id',
      sku: 'SKU-1',
      size: 'M',
      colour: 'Black',
      price: 499.99,
      compareAtPrice: 699.99,
      stockQuantity: 10,
      reservedQuantity: 0,
      status: 'Active',
      availableQuantity: 10
    }],
    images: [{
      imageId: 'image-id',
      url: 'https://example.test/summer-dress.jpg',
      altText: 'Summer dress',
      sortOrder: 0,
      isPrimary: true,
      createdAtUtc: '2026-05-18T11:30:00Z'
    }],
    moderationResults: [{
      moderationResultId: 'moderation-id',
      riskLevel: 'High',
      needsAdminReview: true,
      reason: 'Potential counterfeit wording detected.',
      detectedTerms: ['designer inspired'],
      missingFields: [],
      flags: ['counterfeit-risk'],
      provider: 'local-rule-engine',
      createdAtUtc: '2026-05-18T12:00:00Z'
    }],
    auditTrail: [],
    ...overrides
  };
}
