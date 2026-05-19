import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AdminAdCampaignDetailResponse,
  AdminAdCampaignReasonRequest,
  AdminAdCampaignSummaryResponse
} from './admin-ad-campaign.models';

@Injectable({ providedIn: 'root' })
export class AdminAdCampaignService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/admin/ad-campaigns`;

  getPendingCampaigns(): Promise<AdminAdCampaignSummaryResponse[]> {
    return firstValueFrom(this.http.get<AdminAdCampaignSummaryResponse[]>(`${this.baseUrl}/pending`));
  }

  getCampaign(campaignId: string): Promise<AdminAdCampaignDetailResponse> {
    return firstValueFrom(this.http.get<AdminAdCampaignDetailResponse>(`${this.baseUrl}/${campaignId}`));
  }

  approveCampaign(campaignId: string): Promise<AdminAdCampaignDetailResponse> {
    return firstValueFrom(this.http.post<AdminAdCampaignDetailResponse>(`${this.baseUrl}/${campaignId}/approve`, {}));
  }

  rejectCampaign(
    campaignId: string,
    request: AdminAdCampaignReasonRequest): Promise<AdminAdCampaignDetailResponse> {
    return firstValueFrom(this.http.post<AdminAdCampaignDetailResponse>(`${this.baseUrl}/${campaignId}/reject`, request));
  }
}
