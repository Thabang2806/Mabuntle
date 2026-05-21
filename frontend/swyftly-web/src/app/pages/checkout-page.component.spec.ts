import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Router, provideRouter } from '@angular/router';
import { BuyerPaymentRedirectService, BuyerPaymentService } from '../buyer/buyer-payment.service';
import { CartService } from '../cart/cart.service';
import { createCart } from '../cart/cart.service.spec';
import { CheckoutPageComponent } from './checkout-page.component';

describe('CheckoutPageComponent', () => {
  let fixture: ComponentFixture<CheckoutPageComponent>;
  let paymentRedirectService: jasmine.SpyObj<BuyerPaymentRedirectService>;
  let paymentService: jasmine.SpyObj<BuyerPaymentService>;
  let cartService: jasmine.SpyObj<CartService>;
  let router: Router;

  beforeEach(async () => {
    cartService = jasmine.createSpyObj<CartService>('CartService', ['getCart', 'createOrderFromCart']);
    paymentRedirectService = jasmine.createSpyObj<BuyerPaymentRedirectService>('BuyerPaymentRedirectService', ['redirect']);
    paymentService = jasmine.createSpyObj<BuyerPaymentService>('BuyerPaymentService', ['initiatePayment']);
    cartService.getCart.and.resolveTo(createCart());
    cartService.createOrderFromCart.and.resolveTo({
      orderId: 'order-id',
      buyerId: 'buyer-id',
      sellerId: 'seller-id',
      cartId: 'cart-id',
      status: 'PendingPayment',
      items: [],
      itemsSubtotal: 998,
      shippingAmount: 0,
      platformFeeAmount: 0,
      discountAmount: 0,
      totalAmount: 998,
      statusHistory: []
    });
    paymentService.initiatePayment.and.resolveTo({
      paymentId: 'payment-id',
      orderId: 'order-id',
      provider: 'Fake',
      providerReference: 'fake-reference',
      amount: 998,
      currency: 'ZAR',
      status: 'Pending',
      checkoutUrl: 'https://checkout.example.test/session'
    });

    await TestBed.configureTestingModule({
      imports: [CheckoutPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: BuyerPaymentRedirectService, useValue: paymentRedirectService },
        { provide: BuyerPaymentService, useValue: paymentService },
        { provide: CartService, useValue: cartService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CheckoutPageComponent);
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
  });

  it('loads checkout summary', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Shipping address');
    expect(compiled.textContent).toContain('Delivery');
    expect(compiled.textContent).toContain('Summer Dress');
    expect(compiled.textContent).toContain('Payment');
    expect(compiled.textContent).toContain('Review and start checkout');
    expect(compiled.textContent).toContain('Stock is reserved when checkout starts');
  });

  it('starts checkout, initiates payment, and redirects to checkout url', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    setInput(compiled, 'input[formControlName="fullName"]', 'Buyer One');
    setInput(compiled, 'input[formControlName="phone"]', '+27110000000');
    setInput(compiled, 'input[formControlName="addressLine1"]', '1 Market Street');
    setInput(compiled, 'input[formControlName="city"]', 'Johannesburg');
    setInput(compiled, 'input[formControlName="province"]', 'Gauteng');
    setInput(compiled, 'input[formControlName="postalCode"]', '2000');

    const form = compiled.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(cartService.createOrderFromCart).toHaveBeenCalledWith({
      cartId: 'cart-id',
      reservationMinutes: null
    });
    expect(paymentService.initiatePayment).toHaveBeenCalledWith('order-id');
    expect(paymentRedirectService.redirect).toHaveBeenCalledWith('https://checkout.example.test/session');
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('navigates to success when payment initiation has no checkout url', async () => {
    paymentService.initiatePayment.and.resolveTo({
      paymentId: 'payment-id',
      orderId: 'order-id',
      provider: 'Fake',
      providerReference: 'fake-reference',
      amount: 998,
      currency: 'ZAR',
      status: 'Pending',
      checkoutUrl: null
    });

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    setInput(compiled, 'input[formControlName="fullName"]', 'Buyer One');
    setInput(compiled, 'input[formControlName="phone"]', '+27110000000');
    setInput(compiled, 'input[formControlName="addressLine1"]', '1 Market Street');
    setInput(compiled, 'input[formControlName="city"]', 'Johannesburg');
    setInput(compiled, 'input[formControlName="province"]', 'Gauteng');
    setInput(compiled, 'input[formControlName="postalCode"]', '2000');

    const form = compiled.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(router.navigate).toHaveBeenCalledWith(['/checkout/success'], {
      queryParams: { orderId: 'order-id' }
    });
  });

  it('navigates to failed with order id when payment initiation fails', async () => {
    paymentService.initiatePayment.and.rejectWith({ error: { detail: 'Payment failed.' } });

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    setInput(compiled, 'input[formControlName="fullName"]', 'Buyer One');
    setInput(compiled, 'input[formControlName="phone"]', '+27110000000');
    setInput(compiled, 'input[formControlName="addressLine1"]', '1 Market Street');
    setInput(compiled, 'input[formControlName="city"]', 'Johannesburg');
    setInput(compiled, 'input[formControlName="province"]', 'Gauteng');
    setInput(compiled, 'input[formControlName="postalCode"]', '2000');

    const form = compiled.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(router.navigate).toHaveBeenCalledWith(['/checkout/failed'], {
      queryParams: { orderId: 'order-id' }
    });
  });
});

function setInput(compiled: HTMLElement, selector: string, value: string): void {
  const input = compiled.querySelector(selector) as HTMLInputElement;
  input.value = value;
  input.dispatchEvent(new Event('input'));
}
