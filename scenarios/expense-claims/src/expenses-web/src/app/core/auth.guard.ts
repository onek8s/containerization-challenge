import { Injectable } from '@angular/core';
import { CanActivate, Router, UrlTree } from '@angular/router';
import { Observable, map, take } from 'rxjs';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class AuthGuard implements CanActivate {
  constructor(private auth: AuthService, private router: Router) {}

  canActivate(): Observable<boolean | UrlTree> {
    return this.auth.isAuthenticated$.pipe(
      take(1),
      map(authenticated => {
        if (authenticated) {
          return true;
        }
        if (this.auth.devMode) {
          return this.router.createUrlTree(['/login']);
        }
        this.auth.login();
        return false;
      }));
  }
}
