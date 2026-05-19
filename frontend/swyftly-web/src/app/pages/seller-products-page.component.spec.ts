import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { SellerProductService } from '../seller/seller-product.service';
import { SellerProductsPageComponent } from './seller-products-page.component';

describe('SellerProductsPageComponent', () => {
  let fixture: ComponentFixture<SellerProductsPageComponent>;
  let productService: jasmine.SpyObj<SellerProductService>;

  beforeEach(async () => {
    productService = jasmine.createSpyObj<SellerProductService>('SellerProductService', ['listProducts']);
    productService.listProducts.and.resolveTo([{
      productId: 'product-id',
      categoryId: 'category-id',
      title: 'Summer Dress',
      slug: 'summer-dress',
      status: 'Draft',
      updatedAtUtc: '2026-05-18T12:00:00Z'
    }]);

    await TestBed.configureTestingModule({
      imports: [SellerProductsPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: SellerProductService, useValue: productService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SellerProductsPageComponent);
  });

  it('loads and displays seller products', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Summer Dress');
    expect(compiled.textContent).toContain('Draft');
    expect(compiled.querySelector('a[href="/seller/products/new"]')).not.toBeNull();
  });
});
