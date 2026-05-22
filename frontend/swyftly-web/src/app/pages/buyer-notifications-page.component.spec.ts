import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { BuyerNotificationResponse, NotificationReadRealtimeEvent, NotificationsReadAllRealtimeEvent } from '../buyer/buyer-engagement.models';
import { BuyerEngagementService } from '../buyer/buyer-engagement.service';
import { BuyerNotificationRealtimeService } from '../buyer/buyer-notification-realtime.service';
import { BuyerNotificationsPageComponent } from './buyer-notifications-page.component';

describe('BuyerNotificationsPageComponent', () => {
  let fixture: ComponentFixture<BuyerNotificationsPageComponent>;
  let engagementService: jasmine.SpyObj<BuyerEngagementService>;
  let notificationRealtime: Pick<
    BuyerNotificationRealtimeService,
    'latestNotification' | 'latestReadEvent' | 'latestReadAllEvent' | 'refreshUnreadCount' | 'applyReadAllSync'>;

  beforeEach(async () => {
    engagementService = jasmine.createSpyObj<BuyerEngagementService>('BuyerEngagementService', ['listNotifications', 'markNotificationRead', 'markAllNotificationsRead']);
    engagementService.listNotifications.and.resolveTo([createNotification()]);
    engagementService.markNotificationRead.and.resolveTo({ ...createNotification(), readAtUtc: '2026-05-19T10:05:00Z' });
    engagementService.markAllNotificationsRead.and.resolveTo({ updatedCount: 1 });
    notificationRealtime = {
      latestNotification: signal<BuyerNotificationResponse | null>(null),
      latestReadEvent: signal<NotificationReadRealtimeEvent | null>(null),
      latestReadAllEvent: signal<NotificationsReadAllRealtimeEvent | null>(null),
      refreshUnreadCount: jasmine.createSpy('refreshUnreadCount').and.resolveTo(),
      applyReadAllSync: jasmine.createSpy('applyReadAllSync')
    };

    await TestBed.configureTestingModule({
      imports: [BuyerNotificationsPageComponent],
      providers: [
        provideRouter([]),
        { provide: BuyerEngagementService, useValue: engagementService },
        { provide: BuyerNotificationRealtimeService, useValue: notificationRealtime }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(BuyerNotificationsPageComponent);
  });

  it('lists notifications and marks one read', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Order shipped');
    expect(compiled.textContent).toContain('unread notification');
    expect(compiled.textContent).toContain('Notification settings');

    const readButton = Array.from(compiled.querySelectorAll('button'))
      .find(button => button.textContent?.includes('Mark read')) as HTMLButtonElement;
    readButton.click();
    await fixture.whenStable();

    expect(engagementService.markNotificationRead).toHaveBeenCalledWith('notification-id');
    expect(notificationRealtime.refreshUnreadCount).toHaveBeenCalled();
  });

  it('marks all notifications read', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const allButton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(button => button.textContent?.includes('Mark all read')) as HTMLButtonElement;
    allButton.click();
    await fixture.whenStable();

    expect(engagementService.markAllNotificationsRead).toHaveBeenCalled();
    expect(notificationRealtime.applyReadAllSync).toHaveBeenCalledWith(jasmine.objectContaining({ updatedCount: 1 }));
  });

  it('prepends live notifications without duplicates', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    notificationRealtime.latestNotification.set({
      ...createNotification(),
      notificationId: 'new-notification-id',
      title: 'Return approved'
    });
    fixture.detectChanges();
    await new Promise(resolve => setTimeout(resolve, 0));
    await fixture.whenStable();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Return approved');
    expect(text.match(/Return approved/g)?.length).toBe(1);
  });
});

function createNotification() {
  return {
    notificationId: 'notification-id',
    recipientUserId: 'buyer-user-id',
    type: 'OrderUpdate',
    title: 'Order shipped',
    message: 'Your order has shipped.',
    relatedEntityType: 'Order',
    relatedEntityId: 'order-id',
    readAtUtc: null,
    createdAtUtc: '2026-05-19T10:00:00Z'
  };
}
