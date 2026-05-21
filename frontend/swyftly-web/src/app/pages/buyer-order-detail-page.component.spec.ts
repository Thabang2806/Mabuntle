import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { BuyerEngagementService } from '../buyer/buyer-engagement.service';
import { BuyerOrderResult } from '../buyer/buyer-order.models';
import { BuyerOrderService } from '../buyer/buyer-order.service';
import { BuyerPaymentRedirectService, BuyerPaymentService } from '../buyer/buyer-payment.service';
import { BuyerReturnService } from '../buyer/buyer-return.service';
import { BuyerOrderDetailPageComponent } from './buyer-order-detail-page.component';

describe('BuyerOrderDetailPageComponent', () => {
  let fixture: ComponentFixture<BuyerOrderDetailPageComponent>;
  let engagementService: jasmine.SpyObj<BuyerEngagementService>;
  let orderService: jasmine.SpyObj<BuyerOrderService>;
  let paymentRedirectService: jasmine.SpyObj<BuyerPaymentRedirectService>;
  let paymentService: jasmine.SpyObj<BuyerPaymentService>;
  let returnService: jasmine.SpyObj<BuyerReturnService>;
  let router: Router;

  beforeEach(async () => {
    engagementService = jasmine.createSpyObj<BuyerEngagementService>('BuyerEngagementService', ['createReview']);
    orderService = jasmine.createSpyObj<BuyerOrderService>('BuyerOrderService', ['getOrder']);
    paymentRedirectService = jasmine.createSpyObj<BuyerPaymentRedirectService>('BuyerPaymentRedirectService', ['redirect']);
    paymentService = jasmine.createSpyObj<BuyerPaymentService>('BuyerPaymentService', ['initiatePayment']);
    returnService = jasmine.createSpyObj<BuyerReturnService>('BuyerReturnService', ['createReturn']);
    engagementService.createReview.and.resolveTo({
      reviewId: 'review-id',
      productId: 'product-id',
      orderId: 'order-id',
      orderItemId: 'order-item-id',
      rating: 5,
      title: 'Great fit',
      body: 'Loved it.',
      status: 'PendingReview',
      moderationReason: null,
      moderatedAtUtc: null,
      createdAtUtc: '2026-05-19T10:00:00Z',
      updatedAtUtc: '2026-05-19T10:00:00Z',
      product: null
    });
    orderService.getOrder.and.resolveTo(createOrder());
    paymentService.initiatePayment.and.resolveTo({
      paymentId: 'payment-id',
      orderId: 'order-id',
      provider: 'Fake',
      providerReference: 'fake-reference',
      amount: 500,
      currency: 'ZAR',
      status: 'Pending',
      checkoutUrl: 'https://checkout.example.test/session'
    });
    returnService.createReturn.and.resolveTo({
      returnRequestId: 'return-id',
      orderId: 'order-id',
      buyerId: 'buyer-id',
      sellerId: 'seller-id',
      status: 'Requested',
      reason: 'Damaged',
      details: 'Box torn',
      requestedAtUtc: '2026-05-18T13:00:00Z',
      sellerRespondedAtUtc: null,
      sellerResponseReason: null,
      disputedAtUtc: null,
      disputeReason: null,
      items: [],
      messages: []
    });

    await TestBed.configureTestingModule({
      imports: [BuyerOrderDetailPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: BuyerEngagementService, useValue: engagementService },
        { provide: BuyerOrderService, useValue: orderService },
        { provide: BuyerPaymentRedirectService, useValue: paymentRedirectService },
        { provide: BuyerPaymentService, useValue: paymentService },
        { provide: BuyerReturnService, useValue: returnService },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ orderId: 'order-id' }) } }
        }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(BuyerOrderDetailPageComponent);
  });

  it('creates a return request for a delivered order', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      returnForm: { patchValue: (value: unknown) => void };
    };
    component.returnForm.patchValue({
      orderItemId: 'order-item-id',
      quantity: 1,
      reason: 'Damaged',
      isOpenedOrUnsealed: true,
      details: 'Box torn',
      note: 'Photo available'
    });

    const form = (fixture.nativeElement as HTMLElement).querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));

    await fixture.whenStable();

    expect(returnService.createReturn).toHaveBeenCalledWith('order-id', jasmine.objectContaining({
      reason: 'Damaged',
      details: 'Box torn',
      items: [jasmine.objectContaining({
        orderItemId: 'order-item-id',
        quantity: 1,
        isOpenedOrUnsealed: true,
        note: 'Photo available'
      })]
    }));
    expect(router.navigate).toHaveBeenCalledWith(['/account/returns', 'return-id']);
  });

  it('shows ineligible return messaging for non-delivered orders', async () => {
    orderService.getOrder.and.resolveTo(createOrder({ status: 'Paid' }));

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Returns can be requested after an order is delivered');
    expect(returnService.createReturn).not.toHaveBeenCalled();
  });

  it('creates a product review for a delivered order item', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      reviewForm: { patchValue: (value: unknown) => void };
      createReview: () => Promise<void>;
    };
    component.reviewForm.patchValue({
      orderItemId: 'order-item-id',
      rating: 5,
      title: 'Great fit',
      body: 'Loved it.'
    });
    await component.createReview();

    expect(engagementService.createReview).toHaveBeenCalledWith('order-id', 'order-item-id', {
      rating: 5,
      title: 'Great fit',
      body: 'Loved it.'
    });
  });

  it('retries payment for pending-payment orders', async () => {
    orderService.getOrder.and.resolveTo(createOrder({ status: 'PendingPayment' }));

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const retryButton = Array.from(compiled.querySelectorAll('button'))
      .find(button => button.textContent?.includes('Retry payment'));
    retryButton?.dispatchEvent(new Event('click'));
    await fixture.whenStable();

    expect(paymentService.initiatePayment).toHaveBeenCalledWith('order-id');
    expect(paymentRedirectService.redirect).toHaveBeenCalledWith('https://checkout.example.test/session');
  });

  it('does not show retry payment for cancelled orders', async () => {
    orderService.getOrder.and.resolveTo(createOrder({ status: 'Cancelled' }));

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Start checkout again');
    expect(text).not.toContain('Retry payment');
  });
});

function createOrder(overrides: Partial<BuyerOrderResult> = {}): BuyerOrderResult {
  return {
    orderId: 'order-id',
    buyerId: 'buyer-id',
    sellerId: 'seller-id',
    cartId: 'cart-id',
    status: 'Delivered',
    items: [{
      orderItemId: 'order-item-id',
      productId: 'product-id',
      productVariantId: 'variant-id',
      productTitle: 'Summer Dress',
      sku: 'SKU-1',
      size: 'M',
      colour: 'Black',
      unitPrice: 500,
      quantity: 1,
      lineTotal: 500
    }],
    itemsSubtotal: 500,
    shippingAmount: 0,
    platformFeeAmount: 0,
    discountAmount: 0,
    totalAmount: 500,
    statusHistory: [{ statusHistoryId: 'history-id', previousStatus: null, newStatus: 'Delivered', changedAtUtc: '2026-05-18T12:00:00Z', reason: null }],
    shipments: [],
    ...overrides
  };
}
