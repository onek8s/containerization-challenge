import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { BehaviorSubject, Observable, firstValueFrom, map, of } from 'rxjs';
import { AppConfigHolder } from './app-config';

const DEV_TOKEN_KEY = 'expenses.devToken';

export interface DevUser {
  upn: string;
  displayName: string;
  employeeNumber: string;
  managerEmployeeNumber: string | null;
  roles: string[];
}

/** Sign-in for both modes: ADFS through OpenID Connect, or the API's test tokens in development mode. */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly authenticated = new BehaviorSubject<boolean>(false);
  readonly isAuthenticated$ = this.authenticated.asObservable();

  constructor(private oidc: OidcSecurityService, private http: HttpClient, private router: Router) {}

  get devMode(): boolean {
    return AppConfigHolder.config.auth.mode === 'development';
  }

  async initialize(): Promise<void> {
    if (this.devMode) {
      this.authenticated.next(!!this.readDevToken());
      return;
    }
    const result = await firstValueFrom(this.oidc.checkAuth());
    this.authenticated.next(result.isAuthenticated);
  }

  accessToken(): Observable<string | null> {
    if (this.devMode) {
      return of(this.readDevToken());
    }
    return this.oidc.getAccessToken().pipe(map(t => t || null));
  }

  login(): void {
    if (this.devMode) {
      this.router.navigate(['/login']);
    } else {
      this.oidc.authorize();
    }
  }

  logout(): void {
    if (this.devMode) {
      sessionStorage.removeItem(DEV_TOKEN_KEY);
      this.authenticated.next(false);
      this.router.navigate(['/login']);
    } else {
      this.oidc.logoffLocal();
      this.authenticated.next(false);
      window.location.href = AppConfigHolder.config.auth.authority.replace(/\/$/, '') + '/ls/?wa=wsignout1.0';
    }
  }

  devUsers(): Observable<DevUser[]> {
    return this.http.get<DevUser[]>(`${AppConfigHolder.config.apiUrl}/dev/users`);
  }

  async devLogin(upn: string): Promise<void> {
    const response = await firstValueFrom(
      this.http.post<{ access_token: string }>(`${AppConfigHolder.config.apiUrl}/dev/token`, { upn }));
    sessionStorage.setItem(DEV_TOKEN_KEY, response.access_token);
    this.authenticated.next(true);
  }

  private readDevToken(): string | null {
    const token = sessionStorage.getItem(DEV_TOKEN_KEY);
    if (!token) {
      return null;
    }
    try {
      const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
      if (payload.exp * 1000 < Date.now()) {
        sessionStorage.removeItem(DEV_TOKEN_KEY);
        return null;
      }
    } catch {
      return null;
    }
    return token;
  }
}
