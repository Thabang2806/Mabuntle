import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Router, provideRouter } from '@angular/router';
import { BuyerSupportTicketResponse } from '../buyer/buyer-support.models';
import { BuyerSupportService } from '../buyer/buyer-support.service';
import { BuyerSupportPageComponent } from './buyer-support-page.component';

describe('BuyerSupportPageComponent', () => {
  let fixture: ComponentFixture<BuyerSupportPageComponent>;
  let supportService: jasmine.SpyObj<BuyerSupportService>;
  let router: Router;

  beforeEach(async () => {
    supportService = jasmine.createSpyObj<BuyerSupportService>('BuyerSupportService', ['listTickets', 'createTicket']);
    supportService.listTickets.and.resolveTo([createTicket()]);
    supportService.createTicket.and.resolveTo(createTicket({ supportTicketId: 'created-ticket-id', subject: 'Created ticket' }));

    await TestBed.configureTestingModule({
      imports: [BuyerSupportPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: BuyerSupportService, useValue: supportService }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(BuyerSupportPageComponent);
  });

  it('creates a buyer support ticket with linked order context', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      ticketForm: { patchValue: (value: unknown) => void };
    };
    component.ticketForm.patchValue({
      category: 'OrderIssue',
      subject: 'Order arrived damaged',
      description: 'The box arrived damaged.',
      linkedOrderId: 'order-id',
      linkedSellerId: ''
    });

    const form = (fixture.nativeElement as HTMLElement).querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));

    await fixture.whenStable();

    expect(supportService.createTicket).toHaveBeenCalledWith({
      category: 'OrderIssue',
      subject: 'Order arrived damaged',
      description: 'The box arrived damaged.',
      linkedOrderId: 'order-id',
      linkedProductId: null,
      linkedSellerId: null,
      linkedPaymentId: null
    });
    expect(router.navigate).toHaveBeenCalledWith(['/account/support', 'created-ticket-id']);
  });
});

function createTicket(overrides: Partial<BuyerSupportTicketResponse> = {}): BuyerSupportTicketResponse {
  return {
    supportTicketId: 'ticket-id',
    createdByUserId: 'buyer-user-id',
    createdByRole: 'Buyer',
    buyerId: 'buyer-id',
    sellerId: null,
    category: 'OrderIssue',
    status: 'Open',
    subject: 'Need help',
    description: 'Order help',
    linkedOrderId: 'order-id',
    linkedProductId: null,
    linkedSellerId: null,
    linkedPaymentId: null,
    assignedSupportUserId: null,
    openedAtUtc: '2026-05-18T12:00:00Z',
    resolvedAtUtc: null,
    closedAtUtc: null,
    messages: [],
    ...overrides
  };
}
