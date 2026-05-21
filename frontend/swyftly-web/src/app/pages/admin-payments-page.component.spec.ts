import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AdminPaymentSummaryResponse } from '../admin/admin-order-payment.models';
import { AdminOrderPaymentService } from '../admin/admin-order-payment.service';
import { AdminPaymentsPageComponent } from './admin-payments-page.component';

describe('AdminPaymentsPageComponent', () => {
  let fixture: ComponentFixture<AdminPaymentsPageComponent>;
  let service: jasmine.SpyObj<AdminOrderPaymentService>;

  beforeEach(async () => {
    service = jasmine.createSpyObj<AdminOrderPaymentService>('AdminOrderPaymentService', [
      'getPayments',
      'getPaymentReconciliationCandidates'
    ]);
    service.getPayments.and.resolveTo([createAdminPayment()]);
    service.getPaymentReconciliationCandidates.and.resolveTo([{
      ...createAdminPayment({
        paymentId: 'reconciliation-payment-id',
        status: 'Pending',
        paidAtUtc: null
      }),
      reasonCode: 'StalePendingPayment',
      recommendedAction: 'Check the provider dashboard.',
      latestEvent: null
    }]);

    await TestBed.configureTestingModule({
      imports: [AdminPaymentsPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AdminOrderPaymentService, useValue: service },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({ orderId: 'order-id' }) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminPaymentsPageComponent);
  });

  it('renders admin payments and respects the order query filter', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(service.getPayments).toHaveBeenCalledWith('', 'order-id');
    expect(service.getPaymentReconciliationCandidates).toHaveBeenCalled();
    expect(compiled.textContent).toContain('fake-pay');
    expect(compiled.textContent).toContain('provider-payment-1');
    expect(compiled.textContent).toContain('Manual reconciliation');
    expect(compiled.textContent).toContain('StalePendingPayment');
    expect(compiled.querySelector('a[href="/admin/payments/payment-id"]')).not.toBeNull();
    expect(compiled.querySelector('a[href="/admin/payments/reconciliation-payment-id"]')).not.toBeNull();
  });

  it('sends status and order filters to the admin payment API', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance as unknown as {
      filtersForm: { setValue(value: { search: string; status: string; orderId: string }): void };
      applyFilters(): Promise<void>;
    };
    component.filtersForm.setValue({ search: '', status: 'Paid', orderId: 'order-id' });
    await component.applyFilters();

    expect(service.getPayments).toHaveBeenCalledWith('Paid', 'order-id');
  });
});

export function createAdminPayment(overrides: Partial<AdminPaymentSummaryResponse> = {}): AdminPaymentSummaryResponse {
  return {
    paymentId: 'payment-id',
    orderId: 'order-id',
    buyerId: 'buyer-id',
    provider: 'fake-pay',
    providerReference: 'provider-payment-1',
    amount: 140,
    currency: 'ZAR',
    status: 'Paid',
    paidAtUtc: '2026-05-19T10:05:00Z',
    failedAtUtc: null,
    createdAtUtc: '2026-05-19T10:00:00Z',
    updatedAtUtc: '2026-05-19T10:05:00Z',
    ...overrides
  };
}
