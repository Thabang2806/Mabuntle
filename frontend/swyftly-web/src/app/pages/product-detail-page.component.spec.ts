import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { CartService } from '../cart/cart.service';
import { PublicCatalogService } from '../shop/public-catalog.service';
import { ProductDetailPageComponent } from './product-detail-page.component';
import { createProduct } from './shop-page.component.spec';

describe('ProductDetailPageComponent', () => {
  let fixture: ComponentFixture<ProductDetailPageComponent>;
  let authService: jasmine.SpyObj<AuthService>;
  let cartService: jasmine.SpyObj<CartService>;
  let publicCatalogService: jasmine.SpyObj<PublicCatalogService>;

  beforeEach(async () => {
    authService = jasmine.createSpyObj<AuthService>('AuthService', ['initialize', 'hasAnyRole']);
    authService.initialize.and.resolveTo();
    authService.hasAnyRole.and.returnValue(true);
    cartService = jasmine.createSpyObj<CartService>('CartService', ['addItem']);
    cartService.addItem.and.resolveTo({
      cartId: 'cart-id',
      buyerId: 'buyer-id',
      sellerId: 'seller-id',
      sellerStoreName: 'Seller Store',
      items: [],
      totalQuantity: 1,
      subtotal: 499
    });
    publicCatalogService = jasmine.createSpyObj<PublicCatalogService>('PublicCatalogService', ['getProduct']);
    publicCatalogService.getProduct.and.resolveTo({
      product: createProduct(),
      fullDescription: 'A full product description.',
      attributes: {
        material: '"Cotton"'
      },
      images: [{
        imageId: 'image-id',
        url: 'https://example.test/summer-dress.jpg',
        altText: 'Summer dress',
        isPrimary: true
      }],
      variants: [{
        variantId: 'variant-id',
        size: 'M',
        colour: 'Black',
        price: 499,
        compareAtPrice: 599,
        inStock: true
      }]
    });

    await TestBed.configureTestingModule({
      imports: [ProductDetailPageComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ slug: 'summer-dress' })
            }
          }
        },
        { provide: AuthService, useValue: authService },
        { provide: CartService, useValue: cartService },
        { provide: PublicCatalogService, useValue: publicCatalogService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ProductDetailPageComponent);
  });

  it('loads product detail by slug', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(publicCatalogService.getProduct).toHaveBeenCalledWith('summer-dress');
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Summer Dress');
    expect(compiled.textContent).toContain('A full product description.');
    expect(compiled.textContent).toContain('Cotton');
  });

  it('adds the selected variant to cart', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const button = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(element => element.textContent?.includes('Add to cart')) as HTMLButtonElement;
    button.click();
    await fixture.whenStable();

    expect(cartService.addItem).toHaveBeenCalledWith({
      productVariantId: 'variant-id',
      quantity: 1
    });
  });
});
