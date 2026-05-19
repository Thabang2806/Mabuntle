import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { PublicCatalogService } from '../shop/public-catalog.service';
import { SellerStorefrontPageComponent } from './seller-storefront-page.component';
import { createProduct } from './shop-page.component.spec';

describe('SellerStorefrontPageComponent', () => {
  let fixture: ComponentFixture<SellerStorefrontPageComponent>;
  let publicCatalogService: jasmine.SpyObj<PublicCatalogService>;

  beforeEach(async () => {
    publicCatalogService = jasmine.createSpyObj<PublicCatalogService>('PublicCatalogService', ['getSellerStorefront']);
    publicCatalogService.getSellerStorefront.and.resolveTo({
      sellerId: 'seller-id',
      storeName: 'Seller Store',
      slug: 'seller-store',
      description: 'Curated dresses.',
      logoUrl: null,
      bannerUrl: 'https://example.test/banner.jpg',
      products: [createProduct()]
    });

    await TestBed.configureTestingModule({
      imports: [SellerStorefrontPageComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ storeSlug: 'seller-store' })
            }
          }
        },
        { provide: PublicCatalogService, useValue: publicCatalogService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SellerStorefrontPageComponent);
  });

  it('loads storefront products by slug', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(publicCatalogService.getSellerStorefront).toHaveBeenCalledWith('seller-store');
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Seller Store');
    expect(compiled.textContent).toContain('Curated dresses.');
    expect(compiled.textContent).toContain('Summer Dress');
  });
});
