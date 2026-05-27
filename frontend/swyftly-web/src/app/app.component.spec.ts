import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AuthService } from './auth/auth.service';
import { AuthRole } from './auth/auth.models';
import { AppComponent } from './app.component';
import { BuyerNotificationResponse } from './buyer/buyer-engagement.models';
import { BuyerNotificationRealtimeService } from './buyer/buyer-notification-realtime.service';
import { SellerNotificationResponse } from './seller/seller-notification.models';
import { SellerNotificationRealtimeService } from './seller/seller-notification-realtime.service';

describe('AppComponent', () => {
  let authService: jasmine.SpyObj<Pick<AuthService, 'initialize' | 'logout' | 'hasAnyRole' | 'isAuthenticated'>>;
  let notificationRealtime: Pick<BuyerNotificationRealtimeService, 'unreadCount' | 'latestNotification' | 'dismissLatestNotification'>;
  let sellerNotificationRealtime: Pick<SellerNotificationRealtimeService, 'unreadCount' | 'latestNotification' | 'dismissLatestNotification'>;

  beforeEach(async () => {
    sessionStorage.clear();
    authService = jasmine.createSpyObj('AuthService', ['initialize', 'logout', 'hasAnyRole', 'isAuthenticated']);
    authService.initialize.and.resolveTo();
    authService.logout.and.resolveTo();
    authService.hasAnyRole.and.returnValue(false);
    authService.isAuthenticated.and.returnValue(false);
    notificationRealtime = {
      unreadCount: signal(0),
      latestNotification: signal<BuyerNotificationResponse | null>(null),
      dismissLatestNotification: jasmine.createSpy('dismissLatestNotification')
    };
    sellerNotificationRealtime = {
      unreadCount: signal(0),
      latestNotification: signal<SellerNotificationResponse | null>(null),
      dismissLatestNotification: jasmine.createSpy('dismissLatestNotification')
    };

    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AuthService, useValue: authService },
        { provide: BuyerNotificationRealtimeService, useValue: notificationRealtime },
        { provide: SellerNotificationRealtimeService, useValue: sellerNotificationRealtime }
      ]
    }).compileComponents();
  });

  afterEach(() => {
    sessionStorage.clear();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render the brand navigation', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.brand')?.textContent).toContain('Swyftly');
    expect(compiled.querySelector('.brand-mark')?.textContent?.trim()).toBe('S');
    expect(compiled.querySelector('.header-search')?.textContent).toContain('Search fashion');
    expect(compiled.querySelector('.nav-link--featured')?.textContent).toContain('AI Style Finder');
    expect(compiled.querySelector('a[href="/sell"]')?.textContent).toContain('Sell');
    expect(compiled.querySelector('app-mobile-bottom-nav')).not.toBeNull();
  });

  it('routes unauthenticated sell links to the seller landing page', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const sellLinks = Array.from(compiled.querySelectorAll('a[href="/sell"]'));

    expect(sellLinks.length).toBeGreaterThan(0);
    expect(sellLinks.some(link => link.textContent?.includes('Sell'))).toBeTrue();
  });

  it('should prioritize admin mobile navigation for multi-role users', () => {
    authService.hasAnyRole.and.callFake((roles: readonly AuthRole[]) => roles.includes('Admin') || roles.includes('Buyer'));
    authService.isAuthenticated.and.returnValue(true);

    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    const labels = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.hf-mobile-bottom-nav a'))
      .map(anchor => anchor.textContent?.trim());

    expect(labels).toEqual(['Home', 'Search', 'Admin', 'Queues', 'Finance']);
  });

  it('should render buyer notification badge and toast', () => {
    authService.hasAnyRole.and.callFake((roles: readonly AuthRole[]) => roles.includes('Buyer'));
    authService.isAuthenticated.and.returnValue(true);
    notificationRealtime.unreadCount.set(3);
    notificationRealtime.latestNotification.set({
      notificationId: 'notification-id',
      recipientUserId: 'buyer-user-id',
      type: 'OrderShipped',
      title: 'Order shipped',
      message: 'Your order is on the way.',
      relatedEntityType: 'Order',
      relatedEntityId: 'order-id',
      readAtUtc: null,
      createdAtUtc: '2026-05-21T10:00:00Z'
    });

    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.nav-badge')?.textContent?.trim()).toBe('3');
    expect(compiled.querySelector('.notification-toast')?.textContent).toContain('Order shipped');
    expect(compiled.querySelector('.notification-toast a')?.getAttribute('href')).toBe('/account/orders/order-id');
  });

  it('should render seller notification badge and toast', () => {
    authService.hasAnyRole.and.callFake((roles: readonly AuthRole[]) => roles.includes('Seller'));
    authService.isAuthenticated.and.returnValue(true);
    sellerNotificationRealtime.unreadCount.set(2);
    sellerNotificationRealtime.latestNotification.set({
      notificationId: 'seller-notification-id',
      recipientUserId: 'seller-user-id',
      type: 'ProductApproved',
      title: 'Product approved',
      message: 'Your product was approved.',
      relatedEntityType: 'Product',
      relatedEntityId: 'product-id',
      readAtUtc: null,
      createdAtUtc: '2026-05-26T10:00:00Z'
    });

    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.nav-badge')?.textContent?.trim()).toBe('2');
    expect(compiled.querySelector('.notification-toast')?.textContent).toContain('Product approved');
    expect(compiled.querySelector('.notification-toast a')?.getAttribute('href')).toBe('/seller/products/product-id/edit');
  });
});
