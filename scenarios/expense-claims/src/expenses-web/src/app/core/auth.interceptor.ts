import { HttpErrorResponse, HttpEvent, HttpHandler, HttpInterceptor, HttpRequest } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, catchError, switchMap, take, throwError } from 'rxjs';
import { AppConfigHolder } from './app-config';
import { AuthService } from './auth.service';

/** Adds the access token to calls to the Expense Claims API and starts a new sign-in when it is rejected. */
@Injectable()
export class AuthInterceptor implements HttpInterceptor {
  constructor(private auth: AuthService) {}

  intercept(request: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    const api = AppConfigHolder.config.apiUrl;
    if (!request.url.startsWith(api) || request.url.startsWith(`${api}/dev/`)) {
      return next.handle(request);
    }
    return this.auth.accessToken().pipe(
      take(1),
      switchMap(token => next.handle(token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request)),
      catchError((error: HttpErrorResponse) => {
        if (error.status === 401) {
          this.auth.login();
        }
        return throwError(() => error);
      }));
  }
}
