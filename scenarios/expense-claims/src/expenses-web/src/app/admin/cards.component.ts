import { Component, OnInit } from '@angular/core';
import { ApiService } from '../core/api.service';
import { CompanyCard } from '../core/models';
import { problemMessage } from '../shared/problem';

@Component({
  selector: 'app-cards',
  template: `
    <h1 class="h3 mb-3">Company cards</h1>
    <div class="alert alert-danger" *ngIf="error">{{ error }}</div>
    <form class="row g-2 mb-3" (ngSubmit)="link()" #f="ngForm">
      <div class="col-md-4">
        <input class="form-control" name="masked" [(ngModel)]="maskedNumber" required placeholder="Card number as in the card file, 552235******4410"
               pattern="^[0-9]{6}\\*{6}[0-9]{4}$">
      </div>
      <div class="col-md-3">
        <input class="form-control" name="employee" [(ngModel)]="employeeNumber" required placeholder="Personnel number">
      </div>
      <div class="col-md-2"><button class="btn btn-primary" [disabled]="!f.valid">Link card</button></div>
    </form>
    <table class="table bg-white">
      <thead><tr><th>Card</th><th>Employee</th><th>Status</th><th></th></tr></thead>
      <tbody>
        <tr *ngFor="let c of cards">
          <td>{{ c.maskedNumber }}</td>
          <td>{{ c.employee }} ({{ c.employeeNumber }})</td>
          <td>{{ c.active ? 'Active' : 'Closed' }}</td>
          <td><button class="btn btn-sm btn-link text-danger" *ngIf="c.active" (click)="deactivate(c)">Close</button></td>
        </tr>
      </tbody>
    </table>`
})
export class CardsComponent implements OnInit {
  cards: CompanyCard[] = [];
  maskedNumber = '';
  employeeNumber = '';
  error = '';

  constructor(private api: ApiService) {}

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.api.cards().subscribe({ next: c => this.cards = c, error: e => this.error = problemMessage(e) });
  }

  link(): void {
    this.api.linkCard(this.maskedNumber, this.employeeNumber).subscribe({
      next: () => { this.maskedNumber = ''; this.employeeNumber = ''; this.load(); },
      error: e => this.error = problemMessage(e)
    });
  }

  deactivate(card: CompanyCard): void {
    this.api.deactivateCard(card.id).subscribe({ next: () => this.load(), error: e => this.error = problemMessage(e) });
  }
}
