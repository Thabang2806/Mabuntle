import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { AdminDashboardSummaryResponse } from '../admin/admin-dashboard.models';
import { AdminDashboardService } from '../admin/admin-dashboard.service';
import { AdminPageComponent } from './admin-page.component';

describe('AdminPageComponent', () => {
  let fixture: ComponentFixture<AdminPageComponent>;
  let adminDashboardService: jasmine.SpyObj<AdminDashboardService>;

  beforeEach(async () => {
    adminDashboardService = jasmine.createSpyObj<AdminDashboardService>('AdminDashboardService', ['getSummary']);
    adminDashboardService.getSummary.and.resolveTo(createSummary());

    await TestBed.configureTestingModule({
      imports: [AdminPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AdminDashboardService, useValue: adminDashboardService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminPageComponent);
  });

  it('loads and displays dashboard metrics', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Pending seller approvals');
    expect(compiled.textContent).toContain('Pending product reviews');
    expect(compiled.textContent).toContain('Open disputes');
    expect(compiled.textContent).toContain('3');
  });

  it('links to admin sections', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const links = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('a'))
      .map(link => link.getAttribute('href'));
    expect(links).toContain('/admin/sellers');
    expect(links).toContain('/admin/products');
    expect(links).toContain('/admin/orders');
    expect(links).toContain('/admin/payments');
    expect(links).toContain('/admin/refunds');
    expect(links).toContain('/admin/disputes');
    expect(links).toContain('/admin/payouts');
    expect(links).toContain('/admin/ads');
  });
});

function createSummary(): AdminDashboardSummaryResponse {
  return {
    pendingSellerApprovals: 1,
    pendingProductReviews: 2,
    newOrdersToday: 3,
    openDisputes: 4,
    pendingRefunds: 5,
    pendingPayouts: 6,
    totalGrossSalesPlaceholder: 0,
    platformCommissionPlaceholder: 0
  };
}
