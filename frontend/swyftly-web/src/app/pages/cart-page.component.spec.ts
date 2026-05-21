import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { CartService } from '../cart/cart.service';
import { createCart } from '../cart/cart.service.spec';
import { CartPageComponent } from './cart-page.component';

describe('CartPageComponent', () => {
  let fixture: ComponentFixture<CartPageComponent>;
  let cartService: jasmine.SpyObj<CartService>;

  beforeEach(async () => {
    cartService = jasmine.createSpyObj<CartService>('CartService', ['getCart', 'updateItem', 'removeItem']);
    cartService.getCart.and.resolveTo(createCart());
    cartService.updateItem.and.resolveTo({ ...createCart(), totalQuantity: 3 });
    cartService.removeItem.and.resolveTo({ ...createCart(), items: [], totalQuantity: 0, subtotal: 0 });

    await TestBed.configureTestingModule({
      imports: [CartPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: CartService, useValue: cartService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CartPageComponent);
  });

  it('loads and displays the cart summary', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Summer Dress');
    expect(compiled.textContent).toContain('Seller Store');
    expect(compiled.textContent).toContain('Checkout');
    expect(compiled.textContent).toContain('Single-seller checkout');
    expect(compiled.textContent).toContain('Stock checked at checkout');
    expect(compiled.querySelector('.cart-item-media')?.textContent?.trim()).toBe('S');
  });

  it('updates item quantity', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const input = (fixture.nativeElement as HTMLElement).querySelector('input[type="number"]') as HTMLInputElement;
    input.value = '3';
    input.dispatchEvent(new Event('input'));
    const updateButton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(button => button.textContent?.includes('Update')) as HTMLButtonElement;
    updateButton.click();
    await fixture.whenStable();

    expect(cartService.updateItem).toHaveBeenCalledWith('cart-item-id', { quantity: 3 });
  });

  it('removes an item from the cart', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const removeButton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(button => button.textContent?.includes('Remove')) as HTMLButtonElement;
    removeButton.click();
    await fixture.whenStable();

    expect(cartService.removeItem).toHaveBeenCalledWith('cart-item-id');
  });
});
