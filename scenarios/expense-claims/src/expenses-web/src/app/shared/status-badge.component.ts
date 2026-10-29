import { Component, Input } from '@angular/core';
import { ClaimStatus } from '../core/models';

const CLASSES: Record<ClaimStatus, string> = {
  Draft: 'bg-secondary',
  Submitted: 'bg-primary',
  Returned: 'bg-warning text-dark',
  Approved: 'bg-info text-dark',
  Rejected: 'bg-danger',
  Checked: 'bg-info text-dark',
  InPayment: 'bg-success',
  Paid: 'bg-success'
};

const NAMES: Record<ClaimStatus, string> = {
  Draft: 'Draft',
  Submitted: 'Waiting for approval',
  Returned: 'Returned',
  Approved: 'Approved',
  Rejected: 'Rejected',
  Checked: 'Checked by finance',
  InPayment: 'In payment',
  Paid: 'Paid'
};

@Component({
  selector: 'app-status-badge',
  template: `<span class="badge status" [ngClass]="cssClass">{{ name }}</span>`
})
export class StatusBadgeComponent {
  @Input() status!: ClaimStatus;

  get cssClass(): string {
    return CLASSES[this.status] ?? 'bg-secondary';
  }

  get name(): string {
    return NAMES[this.status] ?? this.status;
  }
}
