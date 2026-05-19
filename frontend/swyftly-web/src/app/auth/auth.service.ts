import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AuthResponse,
  AuthRole,
  AuthSession,
  AuthState,
  AuthTokens,
  AuthUser,
  CurrentUserResponse,
  LoginRequest,
  LogoutRequest,
  RefreshTokenRequest,
  RegisterRequest,
  RegisterResponse
} from './auth.models';
import { AuthStorageService } from './auth-storage.service';

const INITIAL_AUTH_STATE: AuthState = {
  currentUser: null,
  tokens: null,
  initialized: false,
  loading: false
};

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly storage = inject(AuthStorageService);
  private readonly state = signal<AuthState>(INITIAL_AUTH_STATE);
  private initializePromise: Promise<void> | null = null;

  readonly currentUser = computed(() => this.state().currentUser);
  readonly isAuthenticated = computed(() => this.currentUser() !== null && this.state().tokens !== null);
  readonly isInitialized = computed(() => this.state().initialized);
  readonly isLoading = computed(() => this.state().loading);

  get accessToken(): string | null {
    return this.state().tokens?.accessToken ?? this.storage.load()?.tokens.accessToken ?? null;
  }

  async initialize(): Promise<void> {
    if (this.state().initialized) {
      return;
    }

    this.initializePromise ??= this.loadStoredSession();
    await this.initializePromise;
    this.initializePromise = null;
  }

  async register(request: RegisterRequest): Promise<RegisterResponse> {
    return await firstValueFrom(
      this.http.post<RegisterResponse>(this.authUrl('/register'), request)
    );
  }

  async login(request: LoginRequest): Promise<AuthResponse> {
    const response = await firstValueFrom(
      this.http.post<AuthResponse>(this.authUrl('/login'), request)
    );

    this.setAuthenticatedSession(response);
    return response;
  }

  async logout(): Promise<void> {
    const refreshToken = this.state().tokens?.refreshToken ?? this.storage.load()?.tokens.refreshToken;
    this.clearSession();

    if (!refreshToken) {
      return;
    }

    try {
      await firstValueFrom(
        this.http.post<void>(this.authUrl('/logout'), { refreshToken } satisfies LogoutRequest)
      );
    } catch {
      // Local logout should not fail because the server-side token was already invalid.
    }
  }

  hasAnyRole(allowedRoles: readonly AuthRole[]): boolean {
    const roles = this.currentUser()?.roles ?? [];
    return allowedRoles.some(role => roles.includes(role));
  }

  private async loadStoredSession(): Promise<void> {
    const storedSession = this.storage.load();
    if (!storedSession) {
      this.state.set({ ...INITIAL_AUTH_STATE, initialized: true });
      return;
    }

    this.state.set({
      currentUser: storedSession.currentUser,
      tokens: storedSession.tokens,
      initialized: false,
      loading: true
    });

    try {
      const currentUser = await firstValueFrom(
        this.http.get<CurrentUserResponse>(this.authUrl('/me'))
      );
      this.setSession(storedSession.tokens, currentUser, true);
    } catch (error) {
      if (this.isUnauthorized(error)) {
        await this.tryRefresh(storedSession.tokens.refreshToken);
        return;
      }

      this.clearSession(true);
    }
  }

  private async tryRefresh(refreshToken: string): Promise<void> {
    try {
      const response = await firstValueFrom(
        this.http.post<AuthResponse>(
          this.authUrl('/refresh'),
          { refreshToken } satisfies RefreshTokenRequest
        )
      );
      this.setAuthenticatedSession(response);
    } catch {
      this.clearSession(true);
    }
  }

  private setAuthenticatedSession(response: AuthResponse): void {
    const tokens: AuthTokens = {
      accessToken: response.accessToken,
      accessTokenExpiresAtUtc: response.accessTokenExpiresAtUtc,
      refreshToken: response.refreshToken,
      refreshTokenExpiresAtUtc: response.refreshTokenExpiresAtUtc
    };

    const currentUser: AuthUser = {
      userId: response.userId,
      email: response.email,
      roles: response.roles
    };

    this.setSession(tokens, currentUser, true);
  }

  private setSession(tokens: AuthTokens, currentUser: AuthUser, initialized: boolean): void {
    const session: AuthSession = { currentUser, tokens };
    this.storage.save(session);
    this.state.set({
      currentUser,
      tokens,
      initialized,
      loading: false
    });
  }

  private clearSession(initialized = this.state().initialized): void {
    this.storage.clear();
    this.state.set({
      ...INITIAL_AUTH_STATE,
      initialized
    });
  }

  private authUrl(path: string): string {
    return `${environment.apiBaseUrl}/api/auth${path}`;
  }

  private isUnauthorized(error: unknown): boolean {
    return error instanceof HttpErrorResponse && error.status === 401;
  }
}
