import { Injectable, PLATFORM_ID, inject } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { AuthSession } from './auth.models';

const AUTH_SESSION_KEY = 'swyftly.auth.session';

@Injectable({ providedIn: 'root' })
export class AuthStorageService {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  load(): AuthSession | null {
    if (!this.isBrowser) {
      return null;
    }

    const rawSession = sessionStorage.getItem(AUTH_SESSION_KEY);
    if (!rawSession) {
      return null;
    }

    try {
      return JSON.parse(rawSession) as AuthSession;
    } catch {
      this.clear();
      return null;
    }
  }

  save(session: AuthSession): void {
    if (!this.isBrowser) {
      return;
    }

    sessionStorage.setItem(AUTH_SESSION_KEY, JSON.stringify(session));
  }

  clear(): void {
    if (!this.isBrowser) {
      return;
    }

    sessionStorage.removeItem(AUTH_SESSION_KEY);
  }
}
