import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { SellOnSwyftlyPageComponent } from './sell-on-swyftly-page.component';

describe('SellOnSwyftlyPageComponent', () => {
  let fixture: ComponentFixture<SellOnSwyftlyPageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SellOnSwyftlyPageComponent],
      providers: [provideNoopAnimations(), provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(SellOnSwyftlyPageComponent);
  });

  it('renders seller acquisition content and account CTAs', () => {
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const links = Array.from(compiled.querySelectorAll('a')).map(anchor => anchor.getAttribute('href'));

    expect(compiled.querySelector('.sell-hero')).not.toBeNull();
    expect(compiled.textContent).toContain('Build a reviewed fashion, beauty, or jewellery storefront.');
    expect(compiled.textContent).toContain('How selling works');
    expect(compiled.textContent).toContain('Real carrier adapters');
    expect(links).toContain('/register/seller');
    expect(links).toContain('/login');
  });
});
