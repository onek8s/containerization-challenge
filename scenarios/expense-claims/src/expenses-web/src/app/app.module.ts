import { HTTP_INTERCEPTORS, HttpClientModule } from '@angular/common/http';
import { APP_INITIALIZER, LOCALE_ID, NgModule } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BrowserModule } from '@angular/platform-browser';
import { AuthModule, StsConfigLoader } from 'angular-auth-oidc-client';
import { registerLocaleData } from '@angular/common';
import localeFi from '@angular/common/locales/fi';

import { CardsComponent } from './admin/cards.component';
import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { ApprovalListComponent } from './approvals/approval-list.component';
import { ClaimDetailComponent } from './claims/claim-detail.component';
import { ClaimListComponent } from './claims/claim-list.component';
import { AuthInterceptor } from './core/auth.interceptor';
import { oidcConfigLoaderFactory } from './core/auth-config';
import { AuthService } from './core/auth.service';
import { FinanceComponent } from './finance/finance.component';
import { LoginComponent } from './login/login.component';
import { StatusBadgeComponent } from './shared/status-badge.component';

registerLocaleData(localeFi);

export function initializeAuth(auth: AuthService): () => Promise<void> {
  return () => auth.initialize();
}

@NgModule({
  declarations: [
    AppComponent,
    LoginComponent,
    ClaimListComponent,
    ClaimDetailComponent,
    ApprovalListComponent,
    FinanceComponent,
    CardsComponent,
    StatusBadgeComponent
  ],
  imports: [
    BrowserModule,
    HttpClientModule,
    FormsModule,
    AppRoutingModule,
    AuthModule.forRoot({ loader: { provide: StsConfigLoader, useFactory: oidcConfigLoaderFactory } })
  ],
  providers: [
    { provide: APP_INITIALIZER, useFactory: initializeAuth, deps: [AuthService], multi: true },
    { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true },
    { provide: LOCALE_ID, useValue: 'fi' }
  ],
  bootstrap: [AppComponent]
})
export class AppModule {}
