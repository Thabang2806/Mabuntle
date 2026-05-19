import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { authTokenInterceptor } from './auth.interceptor';

const AUTH_SESSION_KEY = 'swyftly.auth.session';

describe('authTokenInterceptor', () => {
  let httpClient: HttpClient;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    sessionStorage.clear();
    sessionStorage.setItem(AUTH_SESSION_KEY, JSON.stringify({
      currentUser: {
        userId: '8df688c9-4bdd-40cc-b6f6-7bd3b7fba019',
        email: 'buyer@example.test',
        roles: ['Buyer']
      },
      tokens: {
        accessToken: 'access-token',
        accessTokenExpiresAtUtc: '2026-05-18T12:30:00+00:00',
        refreshToken: 'refresh-token',
        refreshTokenExpiresAtUtc: '2026-06-01T12:00:00+00:00'
      }
    }));

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authTokenInterceptor])),
        provideHttpClientTesting()
      ]
    });

    httpClient = TestBed.inject(HttpClient);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
    sessionStorage.clear();
  });

  it('adds the bearer token header when an access token exists', () => {
    httpClient.get('/api/example').subscribe();

    const request = httpTestingController.expectOne('/api/example');
    expect(request.request.headers.get('Authorization')).toBe('Bearer access-token');
    request.flush({});
  });
});
