import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { SellerReturnService } from '../seller/seller-return.service';
import { SellerReturnDetailPageComponent } from './seller-return-detail-page.component';
import { createReturnRequest } from './seller-returns-page.component.spec';

describe('SellerReturnDetailPageComponent', () => {
  let fixture: ComponentFixture<SellerReturnDetailPageComponent>;
  let returnService: jasmine.SpyObj<SellerReturnService>;

  beforeEach(async () => {
    returnService = jasmine.createSpyObj<SellerReturnService>(
      'SellerReturnService',
      ['getReturn', 'approveReturn', 'rejectReturn']);
    returnService.getReturn.and.resolveTo(createReturnRequest());
    returnService.approveReturn.and.resolveTo(createReturnRequest({ status: 'Approved' }));
    returnService.rejectReturn.and.resolveTo(createReturnRequest({ status: 'Rejected' }));

    await TestBed.configureTestingModule({
      imports: [SellerReturnDetailPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: SellerReturnService, useValue: returnService },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ returnRequestId: 'return-id' }) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SellerReturnDetailPageComponent);
  });

  it('loads return details and approves a return', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Changed mind');

    const approveButton = Array.from(compiled.querySelectorAll('button'))
      .find(button => button.textContent?.includes('Approve return'));
    approveButton?.dispatchEvent(new Event('click'));
    await fixture.whenStable();

    expect(returnService.approveReturn).toHaveBeenCalledWith('return-id', { message: null });
  });
});
