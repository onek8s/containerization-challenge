import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from '../core/api.service';
import { AuthService, DevUser } from '../core/auth.service';
import { problemMessage } from '../shared/problem';

/** Test user picker shown only when the configuration uses development sign-in. */
@Component({
  selector: 'app-login',
  template: `
    <div class="row justify-content-center mt-5">
      <div class="col-md-6">
        <div class="card">
          <div class="card-body">
            <h1 class="h4">Sign in</h1>
            <p class="text-muted">Development sign-in. Select a test user.</p>
            <div class="alert alert-danger" *ngIf="error">{{ error }}</div>
            <div class="list-group">
              <button type="button" class="list-group-item list-group-item-action" *ngFor="let u of users" (click)="signIn(u)">
                <strong>{{ u.displayName }}</strong> <span class="text-muted">{{ u.upn }}</span>
                <span class="badge bg-secondary ms-1" *ngFor="let r of u.roles">{{ r }}</span>
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>`
})
export class LoginComponent implements OnInit {
  users: DevUser[] = [];
  error = '';

  constructor(private auth: AuthService, private api: ApiService, private router: Router) {}

  ngOnInit(): void {
    this.auth.devUsers().subscribe({ next: u => this.users = u, error: e => this.error = problemMessage(e) });
  }

  async signIn(user: DevUser): Promise<void> {
    try {
      await this.auth.devLogin(user.upn);
      this.api.resetMe();
      this.router.navigate(['/claims']);
    } catch (e) {
      this.error = problemMessage(e);
    }
  }
}
