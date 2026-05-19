import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { SellerProductService } from '../seller/seller-product.service';
import { SellerProductFormPageComponent } from './seller-product-form-page.component';

describe('SellerProductFormPageComponent', () => {
  let fixture: ComponentFixture<SellerProductFormPageComponent>;
  let productService: jasmine.SpyObj<SellerProductService>;

  beforeEach(async () => {
    productService = jasmine.createSpyObj<SellerProductService>(
      'SellerProductService',
      [
        'getCategories',
        'getProduct',
        'createProduct',
        'updateProduct',
        'addVariant',
        'deleteVariant',
        'addImage',
        'deleteImage',
        'submitForReview',
        'generateAiSuggestion',
        'applyAiSuggestion'
      ]);
    productService.getCategories.and.resolveTo([createCategory()]);
    productService.createProduct.and.resolveTo(createProductDetail());
    productService.updateProduct.and.resolveTo(createProductDetail());
    productService.getProduct.and.resolveTo(createProductDetail());
    productService.generateAiSuggestion.and.resolveTo(createAiSuggestion());
    productService.applyAiSuggestion.and.resolveTo(createProductDetail({
      title: 'Seller reviewed AI title',
      tags: ['summer']
    }));
    productService.addVariant.and.resolveTo(createProductDetail({
      variants: [{
        variantId: 'variant-id',
        sku: 'SKU-1',
        size: 'M',
        colour: 'Black',
        price: 100,
        compareAtPrice: null,
        stockQuantity: 10,
        reservedQuantity: 0,
        status: 'Active',
        barcode: null,
        availableQuantity: 10
      }]
    }));

    await TestBed.configureTestingModule({
      imports: [SellerProductFormPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({})
            }
          }
        },
        { provide: SellerProductService, useValue: productService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SellerProductFormPageComponent);
    spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
  });

  it('loads categories and builds required dynamic attribute controls', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance as unknown as {
      onCategoryChanged(categoryId: string): void;
      attributeForm: { controls: Record<string, unknown> };
    };

    component.onCategoryChanged('category-id');

    expect(component.attributeForm.controls['size']).toBeDefined();
  });

  it('creates a product draft with category attributes', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance as unknown as {
      onCategoryChanged(categoryId: string): void;
      basicForm: { patchValue(value: Record<string, unknown>): void };
      attributeForm: { controls: Record<string, { setValue(value: unknown): void }> };
      saveDraft(): Promise<unknown>;
    };

    component.onCategoryChanged('category-id');
    component.basicForm.patchValue({
      categoryId: 'category-id',
      title: 'Summer Dress',
      slug: 'summer-dress',
      shortDescription: 'Short',
      fullDescription: 'Full'
    });
    component.attributeForm.controls['size'].setValue('M');

    await component.saveDraft();

    expect(productService.createProduct).toHaveBeenCalledWith(jasmine.objectContaining({
      categoryId: 'category-id',
      attributes: jasmine.objectContaining({ size: 'M' })
    }));
  });

  it('generates an AI suggestion from the saved product draft', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance as unknown as {
      onCategoryChanged(categoryId: string): void;
      basicForm: { patchValue(value: Record<string, unknown>): void };
      attributeForm: { controls: Record<string, { setValue(value: unknown): void }> };
      aiForm: { patchValue(value: Record<string, unknown>): void };
      generateAiSuggestion(): Promise<void>;
      aiSuggestion(): { recommendedTitle: string | null } | null;
    };

    fillValidProductForm(component);
    component.aiForm.patchValue({ sellerNotes: 'Lightweight dress', productTypeHint: 'Dress' });

    await component.generateAiSuggestion();

    expect(productService.generateAiSuggestion).toHaveBeenCalledWith('product-id', jasmine.objectContaining({
      sellerNotes: 'Lightweight dress',
      productTypeHint: 'Dress',
      selectedCategoryId: 'category-id',
      knownAttributes: jasmine.objectContaining({ size: 'M' })
    }));
    expect(component.aiSuggestion()?.recommendedTitle).toBe('AI title');
  });

  it('applies selected AI suggestions with seller edits', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance as unknown as {
      onCategoryChanged(categoryId: string): void;
      basicForm: { patchValue(value: Record<string, unknown>): void };
      attributeForm: { controls: Record<string, { setValue(value: unknown): void }> };
      generateAiSuggestion(): Promise<void>;
      applyAiSuggestion(): Promise<void>;
      aiApplyForm: { patchValue(value: Record<string, unknown>): void };
      aiEditForm: { patchValue(value: Record<string, unknown>): void };
    };

    fillValidProductForm(component);

    await component.generateAiSuggestion();
    component.aiApplyForm.patchValue({ title: true, tags: true });
    component.aiEditForm.patchValue({ title: 'Seller reviewed AI title', tags: 'summer' });

    await component.applyAiSuggestion();

    expect(productService.applyAiSuggestion).toHaveBeenCalledWith(
      'product-id',
      'suggestion-id',
      jasmine.objectContaining({
        fieldsToApply: ['title', 'tags'],
        editedValues: jasmine.objectContaining({
          title: 'Seller reviewed AI title',
          tags: ['summer']
        })
      }));
  });
});

function fillValidProductForm(component: {
  onCategoryChanged(categoryId: string): void;
  basicForm: { patchValue(value: Record<string, unknown>): void };
  attributeForm: { controls: Record<string, { setValue(value: unknown): void }> };
}): void {
  component.onCategoryChanged('category-id');
  component.basicForm.patchValue({
    categoryId: 'category-id',
    title: 'Summer Dress',
    slug: 'summer-dress',
    shortDescription: 'Short',
    fullDescription: 'Full'
  });
  component.attributeForm.controls['size'].setValue('M');
}

function createCategory() {
  return {
    categoryId: 'category-id',
    parentCategoryId: null,
    name: 'Dresses',
    slug: 'dresses',
    displayOrder: 10,
    attributes: [{
      attributeId: 'attribute-id',
      name: 'Size',
      key: 'size',
      dataType: 'Select' as const,
      isRequired: true,
      allowedValues: ['S', 'M', 'L'],
      displayOrder: 10
    }]
  };
}

function createProductDetail(overrides: Record<string, unknown> = {}) {
  return {
    productId: 'product-id',
    sellerId: 'seller-id',
    categoryId: 'category-id',
    brandId: null,
    title: 'Summer Dress',
    slug: 'summer-dress',
    shortDescription: 'Short',
    fullDescription: 'Full',
    tags: [],
    status: 'Draft',
    rejectionReason: null,
    createdAtUtc: '2026-05-18T12:00:00Z',
    updatedAtUtc: '2026-05-18T12:00:00Z',
    publishedAtUtc: null,
    attributes: { size: '"M"' },
    variants: [],
    images: [],
    ...overrides
  };
}

function createAiSuggestion() {
  return {
    suggestionId: 'suggestion-id',
    recommendedTitle: 'AI title',
    titleSuggestions: ['AI title'],
    shortDescription: 'AI short',
    fullDescription: 'AI full',
    suggestedCategoryId: 'category-id',
    suggestedCategoryPath: 'Dresses',
    attributes: { size: 'M' },
    tags: ['summer'],
    seo: {},
    imageAltText: {},
    missingFields: ['brand'],
    riskFlags: [],
    qualityScore: 70
  };
}
