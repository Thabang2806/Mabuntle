import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AdminCategoryResponse } from '../admin/admin-category.models';
import { AdminCategoryService } from '../admin/admin-category.service';
import { AdminCategoriesPageComponent } from './admin-categories-page.component';

describe('AdminCategoriesPageComponent', () => {
  let fixture: ComponentFixture<AdminCategoriesPageComponent>;
  let categoryService: jasmine.SpyObj<AdminCategoryService>;

  beforeEach(async () => {
    categoryService = jasmine.createSpyObj<AdminCategoryService>('AdminCategoryService', ['listCategories']);
    categoryService.listCategories.and.resolveTo([
      createCategory(),
      createCategory({
        categoryId: 'child-category-id',
        parentCategoryId: 'category-id',
        name: 'Dresses',
        slug: 'dresses'
      })
    ]);

    await TestBed.configureTestingModule({
      imports: [AdminCategoriesPageComponent],
      providers: [
        provideRouter([]),
        { provide: AdminCategoryService, useValue: categoryService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminCategoriesPageComponent);
  });

  it('renders read-only category and attribute metadata', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('write APIs are not available yet');
    expect(compiled.textContent).toContain('Fashion');
    expect(compiled.textContent).toContain('Dresses');
    expect(compiled.textContent).toContain('Colour *');
  });
});

function createCategory(overrides: Partial<AdminCategoryResponse> = {}): AdminCategoryResponse {
  return {
    categoryId: 'category-id',
    parentCategoryId: null,
    name: 'Fashion',
    slug: 'fashion',
    displayOrder: 1,
    isActive: true,
    attributes: [{
      attributeId: 'attribute-id',
      name: 'Colour',
      key: 'colour',
      dataType: 'Text',
      isRequired: true,
      allowedValues: ['Black', 'White'],
      displayOrder: 1,
      isActive: true
    }],
    ...overrides
  };
}
