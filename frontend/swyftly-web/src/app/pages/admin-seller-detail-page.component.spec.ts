import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { AdminSellerDetailResponse } from '../admin/admin-seller.models';
import { AdminSellerService } from '../admin/admin-seller.service';
import { AdminSellerDetailPageComponent } from './admin-seller-detail-page.component';

describe('AdminSellerDetailPageComponent', () => {
  let fixture: ComponentFixture<AdminSellerDetailPageComponent>;
  let adminSellerService: jasmine.SpyObj<AdminSellerService>;

  beforeEach(async () => {
    adminSellerService = jasmine.createSpyObj<AdminSellerService>(
      'AdminSellerService',
      ['getSeller', 'approveSeller', 'rejectSeller', 'suspendSeller']);
    adminSellerService.getSeller.and.resolveTo(createSellerDetail());
    adminSellerService.approveSeller.and.resolveTo(createSellerDetail({
      verificationStatus: 'Verified',
      payout: {
        payoutProviderReference: 'provider-ref-123',
        hasSubmittedPlaceholder: true,
        isAdminApproved: true
      },
      auditTrail: [{
        id: 'audit-id',
        actionType: 'SellerApproved',
        actorUserId: 'admin-id',
        actorRole: 'Admin',
        reason: null,
        createdAtUtc: '2026-05-18T12:30:00Z'
      }]
    }));

    await TestBed.configureTestingModule({
      imports: [AdminSellerDetailPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ sellerId: 'seller-id' })
            }
          }
        },
        { provide: AdminSellerService, useValue: adminSellerService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminSellerDetailPageComponent);
  });

  it('loads seller review details and audit trail', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Seller Store');
    expect(compiled.textContent).toContain('RegisteredBusiness');
    expect(compiled.textContent).toContain('SellerSubmitted');
  });

  it('approves the loaded seller and displays success state', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const approveButton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(button => button.textContent?.includes('Approve seller'));
    approveButton?.dispatchEvent(new Event('click'));

    await fixture.whenStable();
    fixture.detectChanges();

    expect(adminSellerService.approveSeller).toHaveBeenCalledWith('seller-id');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Seller approved.');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('SellerApproved');
  });
});

function createSellerDetail(overrides: Partial<AdminSellerDetailResponse> = {}): AdminSellerDetailResponse {
  return {
    sellerId: 'seller-id',
    userId: 'user-id',
    verificationStatus: 'UnderReview',
    displayName: 'Seller Store',
    contactEmail: 'seller@example.test',
    phoneNumber: '+27110000000',
    businessType: 'RegisteredBusiness',
    businessName: 'Seller Trading',
    storefront: {
      storeName: 'Seller Store',
      slug: 'seller-store',
      description: 'Seller storefront',
      logoUrl: null,
      bannerUrl: null,
      isPublished: false
    },
    address: {
      addressLine1: '1 Market Street',
      addressLine2: null,
      city: 'Johannesburg',
      province: 'Gauteng',
      postalCode: '2000',
      countryCode: 'ZA'
    },
    payout: {
      payoutProviderReference: 'provider-ref-123',
      hasSubmittedPlaceholder: true,
      isAdminApproved: false
    },
    auditTrail: [{
      id: 'audit-id',
      actionType: 'SellerSubmitted',
      actorUserId: 'seller-id',
      actorRole: 'Seller',
      reason: null,
      createdAtUtc: '2026-05-18T12:00:00Z'
    }],
    ...overrides
  };
}
