import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AdminProductSummaryResponse } from '../admin/admin-product.models';
import { AdminProductService } from '../admin/admin-product.service';
import { AdminProductsPageComponent } from './admin-products-page.component';

describe('AdminProductsPageComponent', () => {
  let fixture: ComponentFixture<AdminProductsPageComponent>;
  let adminProductService: jasmine.SpyObj<AdminProductService>;

  beforeEach(async () => {
    adminProductService = jasmine.createSpyObj<AdminProductService>('AdminProductService', ['getPendingReviewProducts']);
    adminProductService.getPendingReviewProducts.and.resolveTo([createProductSummary()]);

    await TestBed.configureTestingModule({
      imports: [AdminProductsPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AdminProductService, useValue: adminProductService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminProductsPageComponent);
  });

  it('loads and displays pending review products', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Summer Dress');
    expect(compiled.textContent).toContain('PendingReview');
    expect(compiled.textContent).toContain('1 high-risk flag');
    expect(compiled.querySelector('a[mat-stroked-button]')?.getAttribute('href')).toBe('/admin/products/product-id');
  });

  it('shows an empty state when there are no pending products', async () => {
    adminProductService.getPendingReviewProducts.and.resolveTo([]);

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No products pending review');
  });
});

function createProductSummary(): AdminProductSummaryResponse {
  return {
    productId: 'product-id',
    sellerId: 'seller-id',
    sellerDisplayName: 'Seller Store',
    sellerVerificationStatus: 'Verified',
    title: 'Summer Dress',
    categoryPath: 'Women > Clothing > Dresses',
    status: 'PendingReview',
    highRiskFlagCount: 1,
    updatedAtUtc: '2026-05-18T12:00:00Z'
  };
}
