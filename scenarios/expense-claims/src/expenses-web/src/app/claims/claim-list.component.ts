import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from '../core/api.service';
import { ClaimSummary, Me } from '../core/models';
import { problemMessage } from '../shared/problem';

@Component({
  selector: 'app-claim-list',
  template: `
    <div class="d-flex justify-content-between align-items-center mb-3">
      <h1 class="h3 mb-0">My expense claims</h1>
      <button class="btn btn-primary" (click)="showNew = !showNew">New claim</button>
    </div>

    <div class="alert alert-warning" *ngIf="me && !me.hasBankAccount">
      There is no bank account for you in the HR system. Claims cannot be paid until HR has added it.
    </div>
    <div class="alert alert-danger" *ngIf="error">{{ error }}</div>

    <form class="card card-body mb-3" *ngIf="showNew" (ngSubmit)="create()" #f="ngForm">
      <div class="row g-2">
        <div class="col-md-6">
          <label class="form-label" for="purpose">Purpose of the trip or expense</label>
          <input id="purpose" class="form-control" name="purpose" [(ngModel)]="purpose" required maxlength="500">
        </div>
        <div class="col-md-3">
          <label class="form-label" for="cc">Cost center</label>
          <input id="cc" class="form-control" name="costCenter" [(ngModel)]="costCenter" [placeholder]="me?.costCenter ?? ''">
        </div>
        <div class="col-md-3">
          <label class="form-label" for="project">Project</label>
          <input id="project" class="form-control" name="projectCode" [(ngModel)]="projectCode" placeholder="PR20114">
        </div>
      </div>
      <div class="mt-2">
        <button class="btn btn-primary" type="submit" [disabled]="!f.valid || busy">Create</button>
      </div>
    </form>

    <table class="table table-hover bg-white" *ngIf="claims.length; else empty">
      <thead>
        <tr><th>Number</th><th>Purpose</th><th>Created</th><th>Status</th><th class="amount">Total EUR</th></tr>
      </thead>
      <tbody>
        <tr *ngFor="let c of claims" [routerLink]="['/claims', c.id]" role="button">
          <td>{{ c.number }}</td>
          <td>{{ c.purpose }}</td>
          <td>{{ c.createdAt | date:'d.M.yyyy' }}</td>
          <td><app-status-badge [status]="c.status"></app-status-badge></td>
          <td class="amount">{{ c.total | number:'1.2-2' }}</td>
        </tr>
      </tbody>
    </table>
    <ng-template #empty><p class="text-muted" *ngIf="!loading">You have no expense claims yet.</p></ng-template>`
})
export class ClaimListComponent implements OnInit {
  claims: ClaimSummary[] = [];
  me?: Me;
  loading = true;
  busy = false;
  error = '';
  showNew = false;
  purpose = '';
  costCenter = '';
  projectCode = '';

  constructor(private api: ApiService, private router: Router) {}

  ngOnInit(): void {
    this.api.me().subscribe(me => this.me = me);
    this.api.myClaims().subscribe({
      next: c => { this.claims = c; this.loading = false; },
      error: e => { this.error = problemMessage(e); this.loading = false; }
    });
  }

  create(): void {
    this.busy = true;
    this.api.createClaim(this.purpose, this.costCenter || null, this.projectCode || null).subscribe({
      next: c => this.router.navigate(['/claims', c.id]),
      error: e => { this.error = problemMessage(e); this.busy = false; }
    });
  }
}
