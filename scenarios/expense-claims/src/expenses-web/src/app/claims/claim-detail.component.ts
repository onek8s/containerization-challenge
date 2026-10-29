import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable } from 'rxjs';
import { ApiService } from '../core/api.service';
import {
  CATEGORIES, CATEGORY_NAMES, CardTransaction, Claim, ClaimLine, ClaimLineRequest, ExpenseCategory, Me, PolicyFinding
} from '../core/models';
import { problemMessage } from '../shared/problem';

function today(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function emptyLine(): ClaimLineRequest {
  return {
    expenseDate: today(), category: 'Travel', description: '', amount: 0, currency: 'EUR',
    kilometres: null, route: null, perDiem: 'None', country: null, attendees: 0
  };
}

@Component({
  selector: 'app-claim-detail',
  templateUrl: './claim-detail.component.html'
})
export class ClaimDetailComponent implements OnInit {
  readonly categories = CATEGORIES;
  readonly categoryNames = CATEGORY_NAMES;

  claim?: Claim;
  me?: Me;
  currencies: string[] = ['EUR'];
  cardTransactions: CardTransaction[] = [];
  showCards = false;

  line: ClaimLineRequest = emptyLine();
  editingLineId: number | null = null;
  showLineForm = false;

  comment = '';
  error = '';
  busy = false;

  constructor(private api: ApiService, private route: ActivatedRoute, private router: Router) {}

  ngOnInit(): void {
    this.api.me().subscribe(me => this.me = me);
    this.api.currencies().subscribe(c => this.currencies = c);
    this.route.paramMap.subscribe(p => this.load(Number(p.get('id'))));
  }

  get canFinanceCheck(): boolean {
    return !!this.me?.isFinance && this.claim?.status === 'Approved' && this.claim.employeeNumber !== this.me.employeeNumber;
  }

  get hasErrors(): boolean {
    return !!this.claim?.findings.some(f => f.severity === 'Error');
  }

  findingsFor(line: ClaimLine): PolicyFinding[] {
    return this.claim?.findings.filter(f => f.lineNumber === line.lineNumber) ?? [];
  }

  get claimFindings(): PolicyFinding[] {
    return this.claim?.findings.filter(f => f.lineNumber === 0) ?? [];
  }

  private load(id: number): void {
    this.api.claim(id).subscribe({ next: c => this.claim = c, error: e => this.error = problemMessage(e) });
  }

  private run(call: Observable<Claim>, after?: () => void): void {
    this.busy = true;
    this.error = '';
    call.subscribe({
      next: c => { this.claim = c; this.busy = false; after?.(); },
      error: e => { this.error = problemMessage(e); this.busy = false; }
    });
  }

  newLine(): void {
    this.line = emptyLine();
    this.editingLineId = null;
    this.showLineForm = true;
  }

  editLine(l: ClaimLine): void {
    this.line = {
      expenseDate: l.expenseDate, category: l.category, description: l.description, amount: l.amount, currency: l.currency,
      kilometres: l.kilometres, route: l.route, perDiem: l.perDiem, country: l.country, attendees: l.attendees
    };
    this.editingLineId = l.id;
    this.showLineForm = true;
  }

  categoryChanged(category: ExpenseCategory): void {
    this.line.perDiem = category === 'PerDiem' ? 'Full' : 'None';
    if (category === 'Mileage' || category === 'PerDiem') {
      this.line.currency = 'EUR';
      this.line.amount = 0;
    }
  }

  saveLine(): void {
    const claim = this.claim!;
    const call = this.editingLineId
      ? this.api.updateLine(claim.id, this.editingLineId, this.line)
      : this.api.addLine(claim.id, this.line);
    this.run(call, () => this.showLineForm = false);
  }

  deleteLine(l: ClaimLine): void {
    if (confirm(`Delete line ${l.lineNumber}?`)) {
      this.run(this.api.deleteLine(this.claim!.id, l.id));
    }
  }

  uploadReceipt(l: ClaimLine, event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (file) {
      this.run(this.api.uploadReceipt(this.claim!.id, l.id, file));
    }
  }

  openReceipt(l: ClaimLine): void {
    this.api.downloadReceipt(l.receipt!.id).subscribe(blob => {
      const url = URL.createObjectURL(blob);
      window.open(url, '_blank');
      setTimeout(() => URL.revokeObjectURL(url), 60000);
    });
  }

  toggleCards(): void {
    this.showCards = !this.showCards;
    if (this.showCards) {
      this.api.openCardTransactions().subscribe(t => this.cardTransactions = t);
    }
  }

  addCardLine(t: CardTransaction): void {
    this.run(this.api.addCardLine(this.claim!.id, t.id), () => {
      this.cardTransactions = this.cardTransactions.filter(x => x.id !== t.id);
    });
  }

  submit(): void {
    this.run(this.api.submit(this.claim!.id));
  }

  deleteClaim(): void {
    if (confirm(`Delete draft ${this.claim!.number}?`)) {
      this.api.deleteClaim(this.claim!.id).subscribe({
        next: () => this.router.navigate(['/claims']),
        error: e => this.error = problemMessage(e)
      });
    }
  }

  decide(decision: 'approve' | 'reject' | 'return'): void {
    this.run(this.api.decide(this.claim!.id, decision, this.comment || null), () => this.router.navigate(['/approvals']));
  }

  financeCheck(accept: boolean): void {
    this.run(this.api.financeCheck(this.claim!.id, accept, this.comment || null), () => this.router.navigate(['/finance']));
  }
}
