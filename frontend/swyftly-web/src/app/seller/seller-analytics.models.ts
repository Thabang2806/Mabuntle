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
