import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '../../environments/environment';
import { AuthResponse } from './auth.models';
import { AuthService } from './auth.service';

const AUTH_SESSION_KEY = 'swyftly.auth.session';

describe('AuthService', () => {
  let service: AuthService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    sessionStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(AuthService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
    sessionStorage.clear();
  });

  it('stores auth state after login', async () => {
    const loginPromise = service.login({
      email: 'buyer@example.test',
      password: 'Password123'
    });

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/auth/login`);
    expect(request.request.method).toBe('POST');
    request.flush(createAuthResponse());

    await loginPromise;

    expect(service.isAuthenticated()).toBeTrue();
    expect(service.currentUser()?.email).toBe('buyer@example.test');
    expect(sessionStorage.getItem(AUTH_SESSION_KEY)).toContain('buyer@example.test');
  });

  it('clears auth state on logout', async () => {
    const loginPromise = service.login({
      email: 'buyer@example.test',
      password: 'Password123'
    });
    httpTestingController.expectOne(`${environment.apiBaseUrl}/api/auth/login`).flush(createAuthResponse());
    await loginPromise;

    const logoutPromise = service.logout();
    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/auth/logout`);
    expect(request.request.body).toEqual({ refreshToken: 'refresh-token' });
    request.flush(null);
    await logoutPromise;

    expect(service.isAuthenticated()).toBeFalse();
    expect(sessionStorage.getItem(AUTH_SESSION_KEY)).toBeNull();
  });
});

function createAuthResponse(): AuthResponse {
  return {
    userId: '8df688c9-4bdd-40cc-b6f6-7bd3b7fba019',
    email: 'buyer@example.test',
    roles: ['Buyer'],
    accessToken: 'access-token',
    accessTokenExpiresAtUtc: '2026-05-18T12:30:00+00:00',
    refreshToken: 'refresh-token',
    refreshTokenExpiresAtUtc: '2026-06-01T12:00:00+00:00'
  };
}
