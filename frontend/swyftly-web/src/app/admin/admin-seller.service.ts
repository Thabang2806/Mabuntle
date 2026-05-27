import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AdminSellerDetailResponse,
  AdminSellerReasonRequest,
  AdminSellerSummaryResponse
} from './admin-seller.models';

@Injectable({ providedIn: 'root' })
export class AdminSellerService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/admin/sellers`;

  getPendingSellers(): Promise<AdminSellerSummaryResponse[]> {
    return firstValueFrom(this.http.get<AdminSellerSummaryResponse[]>(`${this.baseUrl}/pending`));
  }

  getSeller(sellerId: string): Promise<AdminSellerDetailResponse> {
    return firstValueFrom(this.http.get<AdminSellerDetailResponse>(`${this.baseUrl}/${sellerId}`));
  }

  approveSeller(sellerId: string): Promise<AdminSellerDetailResponse> {
    return firstValueFrom(this.http.post<AdminSellerDetailResponse>(`${this.baseUrl}/${sellerId}/approve`, {}));
  }

  rejectSeller(sellerId: string, request: AdminSellerReasonRequest): Promise<AdminSellerDetailResponse> {
    return firstValueFrom(this.http.post<AdminSellerDetailResponse>(`${this.baseUrl}/${sellerId}/reject`, request));
  }

  suspendSeller(sellerId: string, request: AdminSellerReasonRequest): Promise<AdminSellerDetailResponse> {
    return firstValueFrom(this.http.post<AdminSellerDetailResponse>(`${this.baseUrl}/${sellerId}/suspend`, request));
  }

  downloadVerificationEvidence(sellerId: string, evidenceId: string): Promise<Blob> {
    return firstValueFrom(
      this.http.get(`${this.baseUrl}/${sellerId}/verification-evidence/${evidenceId}/download`, { responseType: 'blob' })
    );
  }
}
