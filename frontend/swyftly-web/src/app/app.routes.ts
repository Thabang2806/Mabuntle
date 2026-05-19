import { Routes } from '@angular/router';
import { requireRoleGuard } from './auth/auth.guard';
import { AccountPageComponent } from './pages/account-page.component';
import { HomePageComponent } from './pages/home-page.component';

export const routes: Routes = [
  { path: '', component: HomePageComponent, title: 'Swyftly' },
  {
    path: 'shop',
    loadComponent: () => import('./pages/shop-page.component').then(component => component.ShopPageComponent),
    title: 'Shop | Swyftly'
  },
  {
    path: 'seller/products',
    loadComponent: () => import('./pages/seller-products-page.component').then(component => component.SellerProductsPageComponent),
    title: 'Seller products | Swyftly',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'seller/products/new',
    loadComponent: () => import('./pages/seller-product-form-page.component').then(component => component.SellerProductFormPageComponent),
    title: 'New product | Swyftly',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'seller/products/:id/edit',
    loadComponent: () => import('./pages/seller-product-form-page.component').then(component => component.SellerProductFormPageComponent),
    title: 'Edit product | Swyftly',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'seller/ads',
    loadComponent: () => import('./pages/seller-ad-campaigns-page.component').then(component => component.SellerAdCampaignsPageComponent),
    title: 'Seller ads | Swyftly',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'seller/ads/new',
    loadComponent: () => import('./pages/seller-ad-campaign-form-page.component').then(component => component.SellerAdCampaignFormPageComponent),
    title: 'New ad campaign | Swyftly',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'seller/ads/:id',
    loadComponent: () => import('./pages/seller-ad-campaign-detail-page.component').then(component => component.SellerAdCampaignDetailPageComponent),
    title: 'Ad campaign | Swyftly',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'seller/analytics',
    loadComponent: () => import('./pages/seller-analytics-page.component').then(component => component.SellerAnalyticsPageComponent),
    title: 'Seller analytics | Swyftly',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'seller',
    loadComponent: () => import('./pages/seller-page.component').then(component => component.SellerPageComponent),
    title: 'Seller | Swyftly',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'admin',
    loadComponent: () => import('./pages/admin-page.component').then(component => component.AdminPageComponent),
    title: 'Admin | Swyftly',
    canActivate: [requireRoleGuard(['Admin', 'SuperAdmin'])]
  },
  {
    path: 'admin/sellers',
    loadComponent: () => import('./pages/admin-sellers-page.component').then(component => component.AdminSellersPageComponent),
    title: 'Seller approvals | Swyftly',
    canActivate: [requireRoleGuard(['Admin', 'SuperAdmin'])]
  },
  {
    path: 'admin/sellers/:sellerId',
    loadComponent: () => import('./pages/admin-seller-detail-page.component').then(component => component.AdminSellerDetailPageComponent),
    title: 'Seller review | Swyftly',
    canActivate: [requireRoleGuard(['Admin', 'SuperAdmin'])]
  },
  {
    path: 'admin/products',
    loadComponent: () => import('./pages/admin-products-page.component').then(component => component.AdminProductsPageComponent),
    title: 'Product review queue | Swyftly',
    canActivate: [requireRoleGuard(['Admin', 'SuperAdmin'])]
  },
  {
    path: 'admin/products/:productId',
    loadComponent: () => import('./pages/admin-product-detail-page.component').then(component => component.AdminProductDetailPageComponent),
    title: 'Product review | Swyftly',
    canActivate: [requireRoleGuard(['Admin', 'SuperAdmin'])]
  },
  {
    path: 'admin/audit-logs',
    loadComponent: () => import('./pages/admin-audit-logs-page.component').then(component => component.AdminAuditLogsPageComponent),
    title: 'Audit logs | Swyftly',
    canActivate: [requireRoleGuard(['Admin', 'SuperAdmin'])]
  },
  {
    path: 'admin/reports',
    loadComponent: () => import('./pages/admin-marketplace-reports-page.component').then(component => component.AdminMarketplaceReportsPageComponent),
    title: 'Marketplace reports | Swyftly',
    canActivate: [requireRoleGuard(['Admin', 'SuperAdmin'])]
  },
  {
    path: 'admin/ai-usage',
    loadComponent: () => import('./pages/admin-ai-usage-analytics-page.component').then(component => component.AdminAiUsageAnalyticsPageComponent),
    title: 'AI usage analytics | Swyftly',
    canActivate: [requireRoleGuard(['Admin', 'SuperAdmin'])]
  },
  {
    path: 'admin/orders',
    loadComponent: () => import('./pages/admin-placeholder-page.component').then(component => component.AdminPlaceholderPageComponent),
    title: 'Admin orders | Swyftly',
    canActivate: [requireRoleGuard(['Admin', 'SuperAdmin'])],
    data: { title: 'Orders', description: 'Order operations summary and queue navigation.' }
  },
  {
    path: 'admin/payments',
    loadComponent: () => import('./pages/admin-placeholder-page.component').then(component => component.AdminPlaceholderPageComponent),
    title: 'Admin payments | Swyftly',
    canActivate: [requireRoleGuard(['Admin', 'SuperAdmin'])],
    data: { title: 'Payments', description: 'Payment operations summary and queue navigation.' }
  },
  {
    path: 'admin/refunds',
    loadComponent: () => import('./pages/admin-placeholder-page.component').then(component => component.AdminPlaceholderPageComponent),
    title: 'Admin refunds | Swyftly',
    canActivate: [requireRoleGuard(['Admin', 'SuperAdmin'])],
    data: { title: 'Refunds', description: 'Refund operations summary and queue navigation.' }
  },
  {
    path: 'admin/disputes',
    loadComponent: () => import('./pages/admin-placeholder-page.component').then(component => component.AdminPlaceholderPageComponent),
    title: 'Admin disputes | Swyftly',
    canActivate: [requireRoleGuard(['Admin', 'SuperAdmin'])],
    data: { title: 'Disputes', description: 'Dispute operations summary and queue navigation.' }
  },
  {
    path: 'admin/payouts',
    loadComponent: () => import('./pages/admin-placeholder-page.component').then(component => component.AdminPlaceholderPageComponent),
    title: 'Admin payouts | Swyftly',
    canActivate: [requireRoleGuard(['Admin', 'SuperAdmin'])],
    data: { title: 'Payouts', description: 'Payout operations summary and queue navigation.' }
  },
  {
    path: 'admin/ads',
    loadComponent: () => import('./pages/admin-ad-campaigns-page.component').then(component => component.AdminAdCampaignsPageComponent),
    title: 'Ad campaign review queue | Swyftly',
    canActivate: [requireRoleGuard(['Admin', 'SuperAdmin'])],
  },
  {
    path: 'admin/ads/:id',
    loadComponent: () => import('./pages/admin-ad-campaign-detail-page.component').then(component => component.AdminAdCampaignDetailPageComponent),
    title: 'Ad campaign review | Swyftly',
    canActivate: [requireRoleGuard(['Admin', 'SuperAdmin'])]
  },
  {
    path: 'account',
    component: AccountPageComponent,
    title: 'Account | Swyftly',
    canActivate: [requireRoleGuard(['Buyer'])]
  },
  {
    path: 'cart',
    loadComponent: () => import('./pages/cart-page.component').then(component => component.CartPageComponent),
    title: 'Cart | Swyftly',
    canActivate: [requireRoleGuard(['Buyer'])]
  },
  {
    path: 'assistant',
    loadComponent: () => import('./pages/buyer-ai-assistant-page.component').then(component => component.BuyerAiAssistantPageComponent),
    title: 'Shopping assistant | Swyftly',
    canActivate: [requireRoleGuard(['Buyer'])]
  },
  {
    path: 'visual-search',
    loadComponent: () => import('./pages/buyer-visual-search-page.component').then(component => component.BuyerVisualSearchPageComponent),
    title: 'Visual search | Swyftly',
    canActivate: [requireRoleGuard(['Buyer'])]
  },
  {
    path: 'checkout',
    loadComponent: () => import('./pages/checkout-page.component').then(component => component.CheckoutPageComponent),
    title: 'Checkout | Swyftly',
    canActivate: [requireRoleGuard(['Buyer'])]
  },
  {
    path: 'checkout/success',
    loadComponent: () => import('./pages/checkout-success-page.component').then(component => component.CheckoutSuccessPageComponent),
    title: 'Checkout started | Swyftly',
    canActivate: [requireRoleGuard(['Buyer'])]
  },
  {
    path: 'checkout/failed',
    loadComponent: () => import('./pages/checkout-failed-page.component').then(component => component.CheckoutFailedPageComponent),
    title: 'Checkout issue | Swyftly',
    canActivate: [requireRoleGuard(['Buyer'])]
  },
  {
    path: 'login',
    loadComponent: () => import('./auth/login-page.component').then(component => component.LoginPageComponent),
    title: 'Sign in | Swyftly'
  },
  {
    path: 'register/buyer',
    loadComponent: () => import('./auth/register-page.component').then(component => component.RegisterPageComponent),
    title: 'Create buyer account | Swyftly',
    data: { role: 'Buyer' }
  },
  {
    path: 'register/seller',
    loadComponent: () => import('./auth/register-page.component').then(component => component.RegisterPageComponent),
    title: 'Create seller account | Swyftly',
    data: { role: 'Seller' }
  },
  {
    path: 'access-denied',
    loadComponent: () => import('./auth/access-denied-page.component').then(component => component.AccessDeniedPageComponent),
    title: 'Access denied | Swyftly'
  },
  {
    path: 'category/:slug',
    loadComponent: () => import('./pages/category-page.component').then(component => component.CategoryPageComponent),
    title: 'Category | Swyftly'
  },
  {
    path: 'product/:slug',
    loadComponent: () => import('./pages/product-detail-page.component').then(component => component.ProductDetailPageComponent),
    title: 'Product | Swyftly'
  },
  {
    path: 'seller/:storeSlug',
    loadComponent: () => import('./pages/seller-storefront-page.component').then(component => component.SellerStorefrontPageComponent),
    title: 'Seller storefront | Swyftly'
  },
  { path: '**', redirectTo: '' }
];
