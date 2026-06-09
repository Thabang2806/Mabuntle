import { Routes } from '@angular/router';
import { requireRoleGuard } from './auth/auth.guard';
import { ADMIN_ROLES, FINANCE_READ_ROLES, SUPPORT_ROLES } from './route-roles';

const adminConsoleRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/admin-page.component').then(component => component.AdminPageComponent),
    title: 'Admin | Swyftly',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'sellers',
    loadComponent: () => import('./pages/admin-sellers-page.component').then(component => component.AdminSellersPageComponent),
    title: 'Seller approvals | Swyftly',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'sellers/:sellerId',
    loadComponent: () => import('./pages/admin-seller-detail-page.component').then(component => component.AdminSellerDetailPageComponent),
    title: 'Seller review | Swyftly',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'products',
    loadComponent: () => import('./pages/admin-products-page.component').then(component => component.AdminProductsPageComponent),
    title: 'Product review queue | Swyftly',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'products/revisions/:revisionId',
    loadComponent: () => import('./pages/admin-product-revision-detail-page.component').then(component => component.AdminProductRevisionDetailPageComponent),
    title: 'Product revision review | Swyftly',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'products/variant-revisions/:revisionId',
    loadComponent: () => import('./pages/admin-product-variant-revision-detail-page.component').then(component => component.AdminProductVariantRevisionDetailPageComponent),
    title: 'Product variant revision review | Swyftly',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'products/:productId',
    loadComponent: () => import('./pages/admin-product-detail-page.component').then(component => component.AdminProductDetailPageComponent),
    title: 'Product review | Swyftly',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'reviews',
    loadComponent: () => import('./pages/admin-reviews-page.component').then(component => component.AdminReviewsPageComponent),
    title: 'Buyer review moderation | Swyftly',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'audit-logs',
    loadComponent: () => import('./pages/admin-audit-logs-page.component').then(component => component.AdminAuditLogsPageComponent),
    title: 'Audit logs | Swyftly',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'reports',
    loadComponent: () => import('./pages/admin-marketplace-reports-page.component').then(component => component.AdminMarketplaceReportsPageComponent),
    title: 'Marketplace reports | Swyftly',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'ai-usage',
    loadComponent: () => import('./pages/admin-ai-usage-analytics-page.component').then(component => component.AdminAiUsageAnalyticsPageComponent),
    title: 'AI usage analytics | Swyftly',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'orders',
    loadComponent: () => import('./pages/admin-orders-page.component').then(component => component.AdminOrdersPageComponent),
    title: 'Admin orders | Swyftly',
    canActivate: [requireRoleGuard(FINANCE_READ_ROLES)]
  },
  {
    path: 'orders/:orderId',
    loadComponent: () => import('./pages/admin-order-detail-page.component').then(component => component.AdminOrderDetailPageComponent),
    title: 'Admin order | Swyftly',
    canActivate: [requireRoleGuard(FINANCE_READ_ROLES)]
  },
  {
    path: 'payments',
    loadComponent: () => import('./pages/admin-payments-page.component').then(component => component.AdminPaymentsPageComponent),
    title: 'Admin payments | Swyftly',
    canActivate: [requireRoleGuard(FINANCE_READ_ROLES)]
  },
  {
    path: 'payments/:paymentId',
    loadComponent: () => import('./pages/admin-payment-detail-page.component').then(component => component.AdminPaymentDetailPageComponent),
    title: 'Admin payment | Swyftly',
    canActivate: [requireRoleGuard(FINANCE_READ_ROLES)]
  },
  {
    path: 'refunds',
    loadComponent: () => import('./pages/admin-refunds-page.component').then(component => component.AdminRefundsPageComponent),
    title: 'Admin refunds | Swyftly',
    canActivate: [requireRoleGuard(FINANCE_READ_ROLES)]
  },
  {
    path: 'disputes',
    loadComponent: () => import('./pages/admin-disputes-page.component').then(component => component.AdminDisputesPageComponent),
    title: 'Admin disputes | Swyftly',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'payouts',
    loadComponent: () => import('./pages/admin-payouts-page.component').then(component => component.AdminPayoutsPageComponent),
    title: 'Admin payouts | Swyftly',
    canActivate: [requireRoleGuard(FINANCE_READ_ROLES)]
  },
  {
    path: 'payout-profile-changes',
    loadComponent: () => import('./pages/admin-payout-profile-changes-page.component').then(component => component.AdminPayoutProfileChangesPageComponent),
    title: 'Payout profile changes | Swyftly',
    canActivate: [requireRoleGuard(FINANCE_READ_ROLES)]
  },
  {
    path: 'support',
    loadComponent: () => import('./pages/admin-support-page.component').then(component => component.AdminSupportPageComponent),
    title: 'Support tickets | Swyftly',
    canActivate: [requireRoleGuard(SUPPORT_ROLES)]
  },
  {
    path: 'support/:ticketId',
    loadComponent: () => import('./pages/admin-support-detail-page.component').then(component => component.AdminSupportDetailPageComponent),
    title: 'Support ticket | Swyftly',
    canActivate: [requireRoleGuard(SUPPORT_ROLES)]
  },
  {
    path: 'categories',
    loadComponent: () => import('./pages/admin-categories-page.component').then(component => component.AdminCategoriesPageComponent),
    title: 'Catalog categories | Swyftly',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'pickup-points',
    loadComponent: () => import('./pages/admin-pickup-points-page.component').then(component => component.AdminPickupPointsPageComponent),
    title: 'Pickup points | Swyftly',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'ads',
    loadComponent: () => import('./pages/admin-ad-campaigns-page.component').then(component => component.AdminAdCampaignsPageComponent),
    title: 'Ad campaign review queue | Swyftly',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'ads/:id',
    loadComponent: () => import('./pages/admin-ad-campaign-detail-page.component').then(component => component.AdminAdCampaignDetailPageComponent),
    title: 'Ad campaign review | Swyftly',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  }
];

const adminCompatibilityRedirects: Routes = [
  { path: 'admin', redirectTo: '', pathMatch: 'full' },
  { path: 'admin/sellers', redirectTo: 'sellers', pathMatch: 'full' },
  { path: 'admin/sellers/:sellerId', redirectTo: 'sellers/:sellerId', pathMatch: 'full' },
  { path: 'admin/products', redirectTo: 'products', pathMatch: 'full' },
  { path: 'admin/products/revisions/:revisionId', redirectTo: 'products/revisions/:revisionId', pathMatch: 'full' },
  { path: 'admin/products/variant-revisions/:revisionId', redirectTo: 'products/variant-revisions/:revisionId', pathMatch: 'full' },
  { path: 'admin/products/:productId', redirectTo: 'products/:productId', pathMatch: 'full' },
  { path: 'admin/reviews', redirectTo: 'reviews', pathMatch: 'full' },
  { path: 'admin/audit-logs', redirectTo: 'audit-logs', pathMatch: 'full' },
  { path: 'admin/reports', redirectTo: 'reports', pathMatch: 'full' },
  { path: 'admin/ai-usage', redirectTo: 'ai-usage', pathMatch: 'full' },
  { path: 'admin/orders', redirectTo: 'orders', pathMatch: 'full' },
  { path: 'admin/orders/:orderId', redirectTo: 'orders/:orderId', pathMatch: 'full' },
  { path: 'admin/payments', redirectTo: 'payments', pathMatch: 'full' },
  { path: 'admin/payments/:paymentId', redirectTo: 'payments/:paymentId', pathMatch: 'full' },
  { path: 'admin/refunds', redirectTo: 'refunds', pathMatch: 'full' },
  { path: 'admin/disputes', redirectTo: 'disputes', pathMatch: 'full' },
  { path: 'admin/payouts', redirectTo: 'payouts', pathMatch: 'full' },
  { path: 'admin/payout-profile-changes', redirectTo: 'payout-profile-changes', pathMatch: 'full' },
  { path: 'admin/support', redirectTo: 'support', pathMatch: 'full' },
  { path: 'admin/support/:ticketId', redirectTo: 'support/:ticketId', pathMatch: 'full' },
  { path: 'admin/categories', redirectTo: 'categories', pathMatch: 'full' },
  { path: 'admin/pickup-points', redirectTo: 'pickup-points', pathMatch: 'full' },
  { path: 'admin/ads', redirectTo: 'ads', pathMatch: 'full' },
  { path: 'admin/ads/:id', redirectTo: 'ads/:id', pathMatch: 'full' }
];

export const adminRoutes: Routes = [
  ...adminCompatibilityRedirects,
  {
    path: 'login',
    loadComponent: () => import('./auth/login-page.component').then(component => component.LoginPageComponent),
    title: 'Admin sign in | Swyftly'
  },
  {
    path: 'access-denied',
    loadComponent: () => import('./auth/access-denied-page.component').then(component => component.AccessDeniedPageComponent),
    title: 'Access denied | Swyftly'
  },
  ...adminConsoleRoutes,
  { path: '**', redirectTo: '' }
];
