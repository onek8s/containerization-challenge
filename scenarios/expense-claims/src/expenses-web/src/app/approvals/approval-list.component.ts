import { Component, OnInit } from '@angular/core';
import { ApiService } from '../core/api.service';
import { ClaimSummary } from '../core/models';
import { problemMessage } from '../shared/problem';

@Component({
  selector: 'app-approval-list',
  template: `
    <h1 class="h3 mb-3">Waiting for my approval</h1>
    <div class="alert alert-danger" *ngIf="error">{{ error }}</div>
    <table class="table table-hover bg-white" *ngIf="claims.length; else none">
      <thead><tr><th>Number</th><th>Employee</th><th>Purpose</th><th>Sent</th><th class="amount">Total EUR</th></tr></thead>
      <tbody>
        <tr *ngFor="let c of claims" [routerLink]="['/claims', c.id]" role="button">
          <td>{{ c.number }}</td>
          <td>{{ c.employee }}</td>
          <td>{{ c.purpose }}</td>
          <td>{{ c.submittedAt | date:'d.M.yyyy' }}</td>
          <td class="amount">{{ c.total | number:'1.2-2' }}</td>
        </tr>
      </tbody>
    </table>
    <ng-template #none><p class="text-muted" *ngIf="loaded">Nothing is waiting for your approval.</p></ng-template>`
})
export class ApprovalListComponent implements OnInit {
  claims: ClaimSummary[] = [];
  loaded = false;
  error = '';

  constructor(private api: ApiService) {}

  ngOnInit(): void {
    this.api.pendingApprovals().subscribe({
      next: c => { this.claims = c; this.loaded = true; },
      error: e => this.error = problemMessage(e)
    });
  }
}
