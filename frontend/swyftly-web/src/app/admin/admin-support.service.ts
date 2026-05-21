import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AdminSupportMessageRequest,
  AdminSupportTicketResponse
} from './admin-support.models';

@Injectable({ providedIn: 'root' })
export class AdminSupportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/support/tickets`;

  listTickets(): Promise<AdminSupportTicketResponse[]> {
    return firstValueFrom(this.http.get<AdminSupportTicketResponse[]>(this.baseUrl));
  }

  getTicket(ticketId: string): Promise<AdminSupportTicketResponse> {
    return firstValueFrom(this.http.get<AdminSupportTicketResponse>(`${this.baseUrl}/${ticketId}`));
  }

  addPublicMessage(ticketId: string, request: AdminSupportMessageRequest): Promise<AdminSupportTicketResponse> {
    return firstValueFrom(this.http.post<AdminSupportTicketResponse>(`${this.baseUrl}/${ticketId}/messages`, request));
  }

  addInternalNote(ticketId: string, request: AdminSupportMessageRequest): Promise<AdminSupportTicketResponse> {
    return firstValueFrom(this.http.post<AdminSupportTicketResponse>(`${this.baseUrl}/${ticketId}/internal-notes`, request));
  }

  resolveTicket(ticketId: string): Promise<AdminSupportTicketResponse> {
    return firstValueFrom(this.http.post<AdminSupportTicketResponse>(`${this.baseUrl}/${ticketId}/resolve`, {}));
  }

  closeTicket(ticketId: string): Promise<AdminSupportTicketResponse> {
    return firstValueFrom(this.http.post<AdminSupportTicketResponse>(`${this.baseUrl}/${ticketId}/close`, {}));
  }
}
