import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { BuyerAiAssistantService } from '../buyer/buyer-ai-assistant.service';
import { BuyerAiAssistantPageComponent } from './buyer-ai-assistant-page.component';

describe('BuyerAiAssistantPageComponent', () => {
  let fixture: ComponentFixture<BuyerAiAssistantPageComponent>;
  let assistantService: jasmine.SpyObj<BuyerAiAssistantService>;

  beforeEach(async () => {
    assistantService = jasmine.createSpyObj<BuyerAiAssistantService>('BuyerAiAssistantService', ['search']);
    assistantService.search.and.resolveTo({
      intent: {
        category: 'Dresses',
        subcategory: null,
        budgetMax: 1500,
        budgetMin: null,
        size: 'M',
        colour: 'Black',
        occasion: null,
        style: null,
        material: null,
        brand: null,
        beautySkinType: null,
        beautyConcern: null,
        searchText: 'black dress',
        isVague: false,
        clarificationPrompt: null
      },
      products: [{
        productId: 'product-id',
        title: 'Black Wedding Dress',
        slug: 'black-wedding-dress',
        sellerDisplayName: 'Assistant Seller',
        imageUrl: null,
        price: 999,
        currency: 'ZAR',
        matchReasons: ['Available in Black.', 'Available in size M.']
      }],
      summary: 'These matches come only from published Swyftly products returned by the backend search.',
      safetyNote: null
    });

    await TestBed.configureTestingModule({
      imports: [BuyerAiAssistantPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: BuyerAiAssistantService, useValue: assistantService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(BuyerAiAssistantPageComponent);
  });

  it('submits a message and displays product cards', async () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const input = compiled.querySelector('textarea') as HTMLTextAreaElement;
    input.value = 'black dress';
    input.dispatchEvent(new Event('input'));

    const form = compiled.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(assistantService.search).toHaveBeenCalledWith({ message: 'black dress' });
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Black Wedding Dress');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Available in Black.');
  });
});
