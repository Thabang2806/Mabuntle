import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { BuyerReturnRequestResult } from '../buyer/buyer-return.models';
import { BuyerReturnService } from '../buyer/buyer-return.service';
import { BuyerReturnDetailPageComponent } from './buyer-return-detail-page.component';

describe('BuyerReturnDetailPageComponent', () => {
  let fixture: ComponentFixture<BuyerReturnDetailPageComponent>;
  let returnService: jasmine.SpyObj<BuyerReturnService>;

  beforeEach(async () => {
    returnService = jasmine.createSpyObj<BuyerReturnService>('BuyerReturnService', ['getReturn', 'disputeReturn']);
    returnService.getReturn.and.resolveTo(createReturn());
    returnService.disputeReturn.and.resolveTo(createReturn({ status: 'Disputed', disputeReason: 'Please review.' }));

    await TestBed.configureTestingModule({
      imports: [BuyerReturnDetailPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: BuyerReturnService, useValue: returnService },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ returnRequestId: 'return-id' }) } }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(BuyerReturnDetailPageComponent);
  });

  it('opens a dispute for a rejected return', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      disputeForm: { patchValue: (value: unknown) => void };
    };
    component.disputeForm.patchValue({ reason: 'Please review.' });

    const form = (fixture.nativeElement as HTMLElement).querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));

    await fixture.whenStable();
    fixture.detectChanges();

    expect(returnService.disputeReturn).toHaveBeenCalledWith('return-id', { reason: 'Please review.' });
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Return dispute opened.');
  });
});

function createReturn(overrides: Partial<BuyerReturnRequestResult> = {}): BuyerReturnRequestResult {
  return {
    returnRequestId: 'return-id',
    orderId: 'order-id',
    buyerId: 'buyer-id',
    sellerId: 'seller-id',
    status: 'Rejected',
    reason: 'Damaged',
    details: 'Box torn',
    requestedAtUtc: '2026-05-18T12:00:00Z',
    sellerRespondedAtUtc: '2026-05-18T13:00:00Z',
    sellerResponseReason: 'Rejected by seller.',
    disputedAtUtc: null,
    disputeReason: null,
    items: [{
      returnItemId: 'return-item-id',
      orderItemId: 'order-item-id',
      productId: 'product-id',
      productVariantId: 'variant-id',
      quantity: 1,
      reason: 'Damaged',
      isOpenedOrUnsealed: true,
      note: 'Photo available'
    }],
    messages: [],
    ...overrides
  };
}
