import { Routes } from '@angular/router';
import { AccountPageComponent } from './pages/account-page.component';
import { AdminPageComponent } from './pages/admin-page.component';
import { HomePageComponent } from './pages/home-page.component';
import { SellerPageComponent } from './pages/seller-page.component';
import { ShopPageComponent } from './pages/shop-page.component';

export const routes: Routes = [
  { path: '', component: HomePageComponent, title: 'Swyftly' },
  { path: 'shop', component: ShopPageComponent, title: 'Shop | Swyftly' },
  { path: 'seller', component: SellerPageComponent, title: 'Seller | Swyftly' },
  { path: 'admin', component: AdminPageComponent, title: 'Admin | Swyftly' },
  { path: 'account', component: AccountPageComponent, title: 'Account | Swyftly' },
  { path: '**', redirectTo: '' }
];
