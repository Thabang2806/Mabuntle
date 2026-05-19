import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { SellerAnalyticsSummaryResponse } from './seller-analytics.models';

@Injectable({ providedIn: 'root' })
export class SellerAnalyticsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/seller/analytics`;

  getSummary(): Promise<SellerAnalyticsSummaryResponse> {
    return firstValueFrom(this.http.get<SellerAnalyticsSummaryResponse>(`${this.baseUrl}/summary`));
  }
}
