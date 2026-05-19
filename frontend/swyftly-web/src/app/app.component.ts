import { Component, OnInit, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatToolbarModule } from '@angular/material/toolbar';
import { AuthService } from './auth/auth.service';

@Component({
  selector: 'app-root',
  imports: [MatButtonModule, MatToolbarModule, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit {
  protected readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly navigationItems = computed(() => {
    const items = [{ label: 'Shop', route: '/shop' }];

    if (!this.authService.isAuthenticated()) {
      return [
        ...items,
        { label: 'Sign in', route: '/login' },
        { label: 'Join', route: '/register/buyer' },
        { label: 'Sell', route: '/register/seller' }
      ];
    }

    if (this.authService.hasAnyRole(['Buyer'])) {
      items.push({ label: 'Assistant', route: '/assistant' });
      items.push({ label: 'Visual search', route: '/visual-search' });
      items.push({ label: 'Cart', route: '/cart' });
      items.push({ label: 'Account', route: '/account' });
    }

    if (this.authService.hasAnyRole(['Seller'])) {
      items.push({ label: 'Seller', route: '/seller' });
    }

    if (this.authService.hasAnyRole(['Admin', 'SuperAdmin'])) {
      items.push({ label: 'Admin', route: '/admin' });
    }

    return items;
  });

  ngOnInit(): void {
    void this.authService.initialize();
  }

  protected async logout(): Promise<void> {
    await this.authService.logout();
    await this.router.navigateByUrl('/login');
  }
}
