export type AdminSupportTicketStatus =
  'Open' |
  'WaitingForCustomer' |
  'WaitingForSeller' |
  'Escalated' |
  'Resolved' |
  'Closed';

export type AdminSupportTicketCategory =
  'OrderIssue' |
  'PaymentIssue' |
  'ReturnIssue' |
  'SellerIssue' |
  'ProductIssue' |
  'TechnicalIssue' |
  'Other';

export interface AdminSupportTicketResponse {
  supportTicketId: string;
  createdByUserId: string;
  createdByRole: string;
  buyerId: string | null;
  sellerId: string | null;
  category: AdminSupportTicketCategory | string;
  status: AdminSupportTicketStatus | string;
  subject: string;
  description: string;
  linkedOrderId: string | null;
  linkedProductId: string | null;
  linkedSellerId: string | null;
  linkedPaymentId: string | null;
  assignedSupportUserId: string | null;
  openedAtUtc: string;
  resolvedAtUtc: string | null;
  closedAtUtc: string | null;
  messages: AdminSupportMessageResponse[];
}

export interface AdminSupportMessageResponse {
  supportMessageId: string;
  senderUserId: string;
  senderRole: string;
  message: string;
  isInternal: boolean;
  createdAtUtc: string;
}

export interface AdminSupportMessageRequest {
  message: string;
}
