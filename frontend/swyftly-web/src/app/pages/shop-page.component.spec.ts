import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { PublicCatalogService } from '../shop/public-catalog.service';
import { ShopPageComponent } from './shop-page.component';

describe('ShopPageComponent', () => {
  let fixture: ComponentFixture<ShopPageComponent>;
  let publicCatalogService: jasmine.SpyObj<PublicCatalogService>;

  beforeEach(async () => {
    publicCatalogService = jasmine.createSpyObj<PublicCatalogService>('PublicCatalogService', ['searchProducts']);
    publicCatalogService.searchProducts.and.resolveTo({
      items: [createProduct()],
      page: 1,
      pageSize: 24,
      totalCount: 1,
      sort: 'newest'
    });

    await TestBed.configureTestingModule({
      imports: [ShopPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: PublicCatalogService, useValue: publicCatalogService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ShopPageComponent);
  });

  it('loads and displays published products', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Summer Dress');
    expect(compiled.textContent).toContain('Seller Store');
    expect(compiled.textContent).toContain('1 result');
  });

  it('submits filters to product search', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const queryInput = (fixture.nativeElement as HTMLElement).querySelector('input[formControlName="query"]') as HTMLInputElement;
    queryInput.value = 'dress';
    queryInput.dispatchEvent(new Event('input'));

    const form = (fixture.nativeElement as HTMLElement).querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(publicCatalogService.searchProducts).toHaveBeenCalledWith(jasmine.objectContaining({
      query: 'dress',
      page: 1,
      pageSize: 24
    }));
  });
});

export function createProduct() {
  return {
    productId: 'product-id',
    sellerId: 'seller-id',
    sellerStoreName: 'Seller Store',
    sellerStoreSlug: 'seller-store',
    categoryId: 'category-id',
    categoryPath: 'Women > Dresses',
    brandId: null,
    title: 'Summer Dress',
    slug: 'summer-dress',
    shortDescription: 'Short description',
    primaryImageUrl: 'https://example.test/summer-dress.jpg',
    primaryImageAltText: 'Summer dress',
    priceMin: 499,
    compareAtPriceMin: 599,
    inStock: true,
    tags: ['summer'],
    publishedAtUtc: '2026-05-18T12:00:00Z'
  };
}
