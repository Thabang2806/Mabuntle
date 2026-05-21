import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { BuyerEngagementService } from '../buyer/buyer-engagement.service';
import { BuyerWishlistPageComponent } from './buyer-wishlist-page.component';

describe('BuyerWishlistPageComponent', () => {
  let fixture: ComponentFixture<BuyerWishlistPageComponent>;
  let engagementService: jasmine.SpyObj<BuyerEngagementService>;

  beforeEach(async () => {
    engagementService = jasmine.createSpyObj<BuyerEngagementService>('BuyerEngagementService', ['listWishlist', 'removeWishlistItem']);
    engagementService.listWishlist.and.resolveTo([{ wishlistItemId: 'wishlist-id', createdAtUtc: '2026-05-19T10:00:00Z', product: createProduct() }]);
    engagementService.removeWishlistItem.and.resolveTo();

    await TestBed.configureTestingModule({
      imports: [BuyerWishlistPageComponent],
      providers: [
        provideRouter([]),
        { provide: BuyerEngagementService, useValue: engagementService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(BuyerWishlistPageComponent);
  });

  it('loads and removes wishlist items', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Summer Dress');

    const removeButton = Array.from(compiled.querySelectorAll('button'))
      .find(button => button.textContent?.includes('Remove')) as HTMLButtonElement;
    removeButton.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(engagementService.removeWishlistItem).toHaveBeenCalledWith('product-id');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No saved products yet');
  });
});

function createProduct() {
  return {
    productId: 'product-id',
    sellerId: 'seller-id',
    sellerStoreName: 'Seller Store',
    sellerStoreSlug: 'seller-store',
    categoryId: null,
    categoryPath: 'Women > Dresses',
    brandId: null,
    title: 'Summer Dress',
    slug: 'summer-dress',
    shortDescription: null,
    primaryImageUrl: null,
    primaryImageAltText: null,
    priceMin: 499,
    compareAtPriceMin: null,
    inStock: true,
    tags: [],
    publishedAtUtc: '2026-05-19T10:00:00Z'
  };
}
