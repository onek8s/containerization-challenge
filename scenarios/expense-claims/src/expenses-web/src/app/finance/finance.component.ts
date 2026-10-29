import { Component, OnInit } from '@angular/core';
import { ApiService } from '../core/api.service';
import { ClaimStatus, ClaimSummary, PaymentRun } from '../core/models';
import { problemMessage } from '../shared/problem';

@Component({
  selector: 'app-finance',
  template: `
    <h1 class="h3 mb-3">Finance</h1>
    <div class="alert alert-danger" *ngIf="error">{{ error }}</div>

    <ul class="nav nav-tabs mb-3">
      <li class="nav-item" *ngFor="let s of tabs">
        <a class="nav-link" href="" [class.active]="status === s.status" (click)="select(s.status); $event.preventDefault()">{{ s.name }}</a>
      </li>
      <li class="nav-item">
        <a class="nav-link" href="" [class.active]="status === null" (click)="showRuns(); $event.preventDefault()">Payment runs</a>
      </li>
    </ul>

    <table class="table table-hover bg-white" *ngIf="status !== null">
      <thead><tr><th>Number</th><th>Employee</th><th>Purpose</th><th>Sent</th><th class="amount">Total EUR</th></tr></thead>
      <tbody>
        <tr *ngFor="let c of claims" [routerLink]="['/claims', c.id]" role="button">
          <td>{{ c.number }}</td>
          <td>{{ c.employee }}</td>
          <td>{{ c.purpose }}</td>
          <td>{{ c.submittedAt | date:'d.M.yyyy' }}</td>
          <td class="amount">{{ c.total | number:'1.2-2' }}</td>
        </tr>
        <tr *ngIf="!claims.length"><td colspan="5" class="text-muted">No claims.</td></tr>
      </tbody>
    </table>

    <table class="table bg-white" *ngIf="status === null">
      <thead><tr><th>File</th><th>Created</th><th>Execution date</th><th class="amount">Payments</th><th class="amount">Total EUR</th><th>Status</th><th>Bank reference</th></tr></thead>
      <tbody>
        <tr *ngFor="let r of runs">
          <td>{{ r.messageId }}</td>
          <td>{{ r.createdAt | date:'d.M.yyyy HH:mm' }}</td>
          <td>{{ r.executionDate | date:'d.M.yyyy' }}</td>
          <td class="amount">{{ r.paymentCount }}</td>
          <td class="amount">{{ r.total | number:'1.2-2' }}</td>
          <td>{{ r.status }} <div class="small text-danger" *ngIf="r.error">{{ r.error }}</div></td>
          <td>{{ r.bankReference }}</td>
        </tr>
      </tbody>
    </table>`
})
export class FinanceComponent implements OnInit {
  readonly tabs: { status: ClaimStatus; name: string }[] = [
    { status: 'Approved', name: 'To check' },
    { status: 'Checked', name: 'Waiting for payment run' },
    { status: 'InPayment', name: 'In payment' },
    { status: 'Paid', name: 'Paid' }
  ];

  status: ClaimStatus | null = 'Approved';
  claims: ClaimSummary[] = [];
  runs: PaymentRun[] = [];
  error = '';

  constructor(private api: ApiService) {}

  ngOnInit(): void {
    this.select('Approved');
  }

  select(status: ClaimStatus): void {
    this.status = status;
    this.api.financeClaims(status).subscribe({ next: c => this.claims = c, error: e => this.error = problemMessage(e) });
  }

  showRuns(): void {
    this.status = null;
    this.api.paymentRuns().subscribe({ next: r => this.runs = r, error: e => this.error = problemMessage(e) });
  }
}
