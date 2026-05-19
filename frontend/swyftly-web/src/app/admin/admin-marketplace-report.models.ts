export interface AdminMarketplaceReportResponse {
  fromUtc: string;
  toUtc: string;
  generatedAtUtc: string;
  currency: string;
  finance: AdminMarketplaceFinanceSummaryResponse;
  operations: AdminMarketplaceOperationsSummaryResponse;
  topSellers: AdminTopSellerReportRowResponse[];
  topCategories: AdminTopCategoryReportRowResponse[];
  csvExportUrl: string;
}

export interface AdminMarketplaceFinanceSummaryResponse {
  grossMerchandiseValue: number;
  platformCommissionEarned: number;
  paymentProcessingFees: number;
  refunds: number;
  sellerPendingBalances: number;
  sellerAvailableBalances: number;
  sellerHeldBalances: number;
  payoutsProcessed: number;
  failedPayouts: number;
}

export interface AdminMarketplaceOperationsSummaryResponse {
  orderCount: number;
  refundCount: number;
  payoutsProcessedCount: number;
  failedPayoutCount: number;
  disputeCount: number;
  activeDisputeCount: number;
}

export interface AdminTopSellerReportRowResponse {
  sellerId: string;
  sellerDisplayName: string | null;
  orderCount: number;
  grossMerchandiseValue: number;
  itemsSold: number;
}

export interface AdminTopCategoryReportRowResponse {
  categoryId: string | null;
  categoryName: string | null;
  quantitySold: number;
  revenue: number;
}

export interface AdminMarketplaceReportRequest {
  fromUtc?: string;
  toUtc?: string;
}
