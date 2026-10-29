import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { CardsComponent } from './admin/cards.component';
import { ApprovalListComponent } from './approvals/approval-list.component';
import { ClaimDetailComponent } from './claims/claim-detail.component';
import { ClaimListComponent } from './claims/claim-list.component';
import { AuthGuard } from './core/auth.guard';
import { FinanceComponent } from './finance/finance.component';
import { LoginComponent } from './login/login.component';

const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'claims', component: ClaimListComponent, canActivate: [AuthGuard] },
  { path: 'claims/:id', component: ClaimDetailComponent, canActivate: [AuthGuard] },
  { path: 'approvals', component: ApprovalListComponent, canActivate: [AuthGuard] },
  { path: 'finance', component: FinanceComponent, canActivate: [AuthGuard] },
  { path: 'admin/cards', component: CardsComponent, canActivate: [AuthGuard] },
  { path: '', pathMatch: 'full', redirectTo: 'claims' },
  { path: '**', redirectTo: 'claims' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule {}
