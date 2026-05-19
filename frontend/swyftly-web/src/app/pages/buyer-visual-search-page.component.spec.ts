import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { BuyerVisualSearchService } from '../buyer/buyer-visual-search.service';
import { BuyerVisualSearchPageComponent } from './buyer-visual-search-page.component';

describe('BuyerVisualSearchPageComponent', () => {
  let fixture: ComponentFixture<BuyerVisualSearchPageComponent>;
  let visualSearchService: jasmine.SpyObj<BuyerVisualSearchService>;

  beforeEach(async () => {
    visualSearchService = jasmine.createSpyObj<BuyerVisualSearchService>('BuyerVisualSearchService', ['search']);
    visualSearchService.search.and.resolveTo({
      attributes: {
        category: 'Dresses',
        colour: 'Black',
        style: 'Formal',
        shape: 'Maxi',
        pattern: null,
        materialGuess: null,
        materialConfidence: null,
        confidence: 0.72,
        searchText: 'Dresses Black Formal Maxi',
        warnings: ['Material and brand are not inferred unless visible context is explicit.']
      },
      products: [{
        productId: 'product-id',
        title: 'Black Formal Maxi Dress',
        slug: 'black-formal-maxi-dress',
        sellerDisplayName: 'Visual Seller',
        imageUrl: null,
        price: 999,
        currency: 'ZAR',
        matchReasons: ['Available in Black.']
      }],
      summary: 'These matches use extracted visual attributes against published Swyftly products only.',
      imageRetentionNote: 'Uploaded image data is processed for this request only and is not persisted by the visual search MVP.'
    });

    await TestBed.configureTestingModule({
      imports: [BuyerVisualSearchPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: BuyerVisualSearchService, useValue: visualSearchService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(BuyerVisualSearchPageComponent);
  });

  it('submits an image reference and displays product cards', async () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const input = compiled.querySelector('input[formcontrolname="imageReference"]') as HTMLInputElement;
    input.value = 'black formal maxi dress';
    input.dispatchEvent(new Event('input'));

    const form = compiled.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(visualSearchService.search).toHaveBeenCalledWith({
      imageReference: 'black formal maxi dress',
      imageDataBase64: null,
      fileName: null,
      contentType: null
    });
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Black Formal Maxi Dress');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('not persisted');
  });

  it('requires an upload or image reference', async () => {
    fixture.detectChanges();
    const form = (fixture.nativeElement as HTMLElement).querySelector('form') as HTMLFormElement;

    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(visualSearchService.search).not.toHaveBeenCalled();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Upload an image or enter an image reference.');
  });
});
