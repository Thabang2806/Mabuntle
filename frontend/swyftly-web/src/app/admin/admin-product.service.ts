import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AdminProductApproveRequest,
  AdminProductDetailResponse,
  AdminProductReasonRequest,
  AdminProductSummaryResponse
} from './admin-product.models';

@Injectable({ providedIn: 'root' })
export class AdminProductService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/admin/products`;

  getPendingReviewProducts(): Promise<AdminProductSummaryResponse[]> {
    return firstValueFrom(this.http.get<AdminProductSummaryResponse[]>(`${this.baseUrl}/pending-review`));
  }

  getProduct(productId: string): Promise<AdminProductDetailResponse> {
    return firstValueFrom(this.http.get<AdminProductDetailResponse>(`${this.baseUrl}/${productId}`));
  }

  approveProduct(productId: string, request: AdminProductApproveRequest = {}): Promise<AdminProductDetailResponse> {
    return firstValueFrom(this.http.post<AdminProductDetailResponse>(`${this.baseUrl}/${productId}/approve`, request));
  }

  rejectProduct(productId: string, request: AdminProductReasonRequest): Promise<AdminProductDetailResponse> {
    return firstValueFrom(this.http.post<AdminProductDetailResponse>(`${this.baseUrl}/${productId}/reject`, request));
  }

  requestChanges(productId: string, request: AdminProductReasonRequest): Promise<AdminProductDetailResponse> {
    return firstValueFrom(this.http.post<AdminProductDetailResponse>(`${this.baseUrl}/${productId}/request-changes`, request));
  }
}
