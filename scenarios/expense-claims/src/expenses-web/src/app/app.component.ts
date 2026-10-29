import { Component, OnInit } from '@angular/core';
import { Observable, of, switchMap } from 'rxjs';
import { ApiService } from './core/api.service';
import { AuthService } from './core/auth.service';
import { Me } from './core/models';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html'
})
export class AppComponent implements OnInit {
  me$: Observable<Me | null> = of(null);

  constructor(public auth: AuthService, private api: ApiService) {}

  ngOnInit(): void {
    this.me$ = this.auth.isAuthenticated$.pipe(switchMap(a => a ? this.api.me() : of(null)));
  }
}
