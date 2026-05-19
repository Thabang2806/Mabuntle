import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AdminMarketplaceReportRequest,
  AdminMarketplaceReportResponse
} from './admin-marketplace-report.models';

@Injectable({ providedIn: 'root' })
export class AdminMarketplaceReportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/admin/reports/marketplace`;

  getReport(request: AdminMarketplaceReportRequest = {}): Promise<AdminMarketplaceReportResponse> {
    return firstValueFrom(this.http.get<AdminMarketplaceReportResponse>(this.baseUrl, {
      params: this.createParams(request)
    }));
  }

  getCsvExportUrl(request: AdminMarketplaceReportRequest = {}): string {
    const params = this.createParams(request).toString();
    return params ? `${this.baseUrl}/export.csv?${params}` : `${this.baseUrl}/export.csv`;
  }

  private createParams(request: AdminMarketplaceReportRequest): HttpParams {
    let params = new HttpParams();

    for (const [key, value] of Object.entries(request)) {
      if (value !== null && value !== undefined && value !== '') {
        params = params.set(key, value);
      }
    }

    return params;
  }
}
