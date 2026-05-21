import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  BuyerNotificationResponse,
  BuyerProductReviewResponse,
  BuyerWishlistItemResponse,
  NotificationsReadAllResponse,
  ProductReviewRequest,
  PublicProductReviewResponse,
  PublicProductReviewSummaryResponse
} from './buyer-engagement.models';

@Injectable({ providedIn: 'root' })
export class BuyerEngagementService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  listWishlist(): Promise<BuyerWishlistItemResponse[]> {
    return firstValueFrom(this.http.get<BuyerWishlistItemResponse[]>(`${this.baseUrl}/api/buyer/wishlist`));
  }

  addWishlistItem(productId: string): Promise<BuyerWishlistItemResponse> {
    return firstValueFrom(this.http.post<BuyerWishlistItemResponse>(`${this.baseUrl}/api/buyer/wishlist/${productId}`, null));
  }

  removeWishlistItem(productId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.baseUrl}/api/buyer/wishlist/${productId}`));
  }

  listBuyerReviews(): Promise<BuyerProductReviewResponse[]> {
    return firstValueFrom(this.http.get<BuyerProductReviewResponse[]>(`${this.baseUrl}/api/buyer/reviews`));
  }

  createReview(orderId: string, orderItemId: string, request: ProductReviewRequest): Promise<BuyerProductReviewResponse> {
    return firstValueFrom(
      this.http.post<BuyerProductReviewResponse>(
        `${this.baseUrl}/api/buyer/orders/${orderId}/items/${orderItemId}/review`,
        request));
  }

  updateReview(reviewId: string, request: ProductReviewRequest): Promise<BuyerProductReviewResponse> {
    return firstValueFrom(this.http.put<BuyerProductReviewResponse>(`${this.baseUrl}/api/buyer/reviews/${reviewId}`, request));
  }

  deleteReview(reviewId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.baseUrl}/api/buyer/reviews/${reviewId}`));
  }

  listProductReviews(slug: string): Promise<PublicProductReviewResponse[]> {
    return firstValueFrom(this.http.get<PublicProductReviewResponse[]>(`${this.baseUrl}/api/products/${slug}/reviews`));
  }

  getProductReviewSummary(slug: string): Promise<PublicProductReviewSummaryResponse> {
    return firstValueFrom(this.http.get<PublicProductReviewSummaryResponse>(`${this.baseUrl}/api/products/${slug}/review-summary`));
  }

  listNotifications(): Promise<BuyerNotificationResponse[]> {
    return firstValueFrom(this.http.get<BuyerNotificationResponse[]>(`${this.baseUrl}/api/buyer/notifications`));
  }

  markNotificationRead(notificationId: string): Promise<BuyerNotificationResponse> {
    return firstValueFrom(this.http.post<BuyerNotificationResponse>(`${this.baseUrl}/api/buyer/notifications/${notificationId}/read`, null));
  }

  markAllNotificationsRead(): Promise<NotificationsReadAllResponse> {
    return firstValueFrom(this.http.post<NotificationsReadAllResponse>(`${this.baseUrl}/api/buyer/notifications/read-all`, null));
  }
}
