export interface SellerAnalyticsSummaryResponse {
  sellerId: string;
  totalSales: number;
  orderCount: number;
  averageOrderValue: number;
  conversionRatePlaceholder: number;
  productsSold: number;
  totalRefunded: number;
  refundRate: number;
  returnRate: number;
  topProducts: SellerTopProductResponse[];
  lowStockProducts: SellerLowStockProductResponse[];
  adPerformance: SellerAdAnalyticsResponse;
  aiUsage: SellerAiUsageAnalyticsResponse;
}

export interface SellerTopProductResponse {
  productId: string;
  productTitle: string | null;
  quantitySold: number;
  revenue: number;
}

export interface SellerLowStockProductResponse {
  productId: string;
  title: string | null;
  status: string;
  availableQuantity: number;
  lowStockVariantCount: number;
}

export interface SellerAdAnalyticsResponse {
  campaignCount: number;
  impressions: number;
  clicks: number;
  clickThroughRate: number;
  spend: number;
  ordersGenerated: number;
  revenueGenerated: number;
  topCampaigns: SellerAdCampaignAnalyticsResponse[];
}

export interface SellerAdCampaignAnalyticsResponse {
  adCampaignId: string;
  name: string;
  status: string;
  impressions: number;
  clicks: number;
  clickThroughRate: number;
  spend: number;
  ordersGenerated: number;
  revenueGenerated: number;
  returnOnAdSpend: number;
}

export interface SellerAiUsageAnalyticsResponse {
  requests: number;
  successfulRequests: number;
  failedRequests: number;
  estimatedCost: number;
  averageLatencyMs: number;
  suggestionsGenerated: number;
  suggestionsAccepted: number;
  suggestionAcceptanceRate: number;
  productsImprovedWithAi: number;
  averageListingQualityScore: number;
  averageQualityScoreImprovement: number | null;
  qualityScoreImprovementNote: string;
  fieldValuesAccepted: number;
  fieldValuesEdited: number;
}

export interface SellerAnalyticsPerformanceRequest {
  fromUtc?: string;
  toUtc?: string;
  bucket?: 'Day' | 'Week';
}

export type SellerAnalyticsCsvReport = 'Sales' | 'Products' | 'Inventory' | 'Ads' | 'Returns';

export interface SellerAnalyticsPerformanceResponse {
  sellerId: string;
  fromUtc: string;
  toUtc: string;
  bucket: 'Day' | 'Week';
  salesTrend: SellerSalesTrendBucketResponse[];
  productPerformance: SellerProductPerformanceResponse[];
  inventoryPerformance: SellerInventoryPerformanceResponse[];
  adPerformance: SellerAdPerformanceDetailResponse[];
  customerCareSummary: SellerCustomerCareSummaryResponse;
}

export interface SellerSalesTrendBucketResponse {
  periodStartUtc: string;
  periodEndUtc: string;
  orderCount: number;
  grossSales: number;
  refundedAmount: number;
  netSales: number;
  unitsSold: number;
}

export interface SellerProductPerformanceResponse {
  productId: string;
  productTitle: string | null;
  productSlug: string | null;
  status: string;
  unitsSold: number;
  grossSales: number;
  refundedAmount: number;
  returnCount: number;
  returnRate: number;
  stockQuantity: number;
  reservedQuantity: number;
  availableQuantity: number;
}

export interface SellerInventoryPerformanceResponse {
  productId: string;
  productTitle: string | null;
  productVariantId: string;
  sku: string;
  barcode: string | null;
  size: string;
  colour: string;
  status: string;
  stockQuantity: number;
  reservedQuantity: number;
  availableQuantity: number;
  isLowStock: boolean;
  isOutOfStock: boolean;
  lastMovementAtUtc: string | null;
}

export interface SellerAdPerformanceDetailResponse {
  adCampaignId: string;
  name: string;
  status: string;
  impressions: number;
  clicks: number;
  clickThroughRate: number;
  spend: number;
  ordersGenerated: number;
  revenueGenerated: number;
  returnOnAdSpend: number;
}

export interface SellerCustomerCareSummaryResponse {
  returnCount: number;
  openReturnCount: number;
  refundCount: number;
  refundedAmount: number;
  supportTicketCount: number;
  openSupportTicketCount: number;
  disputeCount: number;
  activeDisputeCount: number;
}
