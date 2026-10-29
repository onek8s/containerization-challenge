import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { AppConfigHolder } from './app-config';
import {
  CardTransaction, Claim, ClaimLineRequest, ClaimStatus, ClaimSummary, CompanyCard, Me, PaymentRun
} from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private me$?: Observable<Me>;

  constructor(private http: HttpClient) {}

  private get base(): string {
    return AppConfigHolder.config.apiUrl + '/api';
  }

  me(): Observable<Me> {
    this.me$ ??= this.http.get<Me>(`${this.base}/me`).pipe(shareReplay(1));
    return this.me$;
  }

  resetMe(): void {
    this.me$ = undefined;
  }

  myClaims(): Observable<ClaimSummary[]> {
    return this.http.get<ClaimSummary[]>(`${this.base}/claims`);
  }

  claim(id: number): Observable<Claim> {
    return this.http.get<Claim>(`${this.base}/claims/${id}`);
  }

  createClaim(purpose: string, costCenter: string | null, projectCode: string | null): Observable<Claim> {
    return this.http.post<Claim>(`${this.base}/claims`, { purpose, costCenter, projectCode });
  }

  updateClaim(id: number, purpose: string, costCenter: string | null, projectCode: string | null): Observable<Claim> {
    return this.http.put<Claim>(`${this.base}/claims/${id}`, { purpose, costCenter, projectCode });
  }

  deleteClaim(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/claims/${id}`);
  }

  addLine(id: number, line: ClaimLineRequest): Observable<Claim> {
    return this.http.post<Claim>(`${this.base}/claims/${id}/lines`, line);
  }

  updateLine(id: number, lineId: number, line: ClaimLineRequest): Observable<Claim> {
    return this.http.put<Claim>(`${this.base}/claims/${id}/lines/${lineId}`, line);
  }

  deleteLine(id: number, lineId: number): Observable<Claim> {
    return this.http.delete<Claim>(`${this.base}/claims/${id}/lines/${lineId}`);
  }

  addCardLine(id: number, transactionId: number): Observable<Claim> {
    return this.http.post<Claim>(`${this.base}/claims/${id}/lines/from-card/${transactionId}`, null);
  }

  uploadReceipt(id: number, lineId: number, file: File): Observable<Claim> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<Claim>(`${this.base}/claims/${id}/lines/${lineId}/receipt`, form);
  }

  downloadReceipt(receiptId: number): Observable<Blob> {
    return this.http.get(`${this.base}/receipts/${receiptId}`, { responseType: 'blob' });
  }

  submit(id: number): Observable<Claim> {
    return this.http.post<Claim>(`${this.base}/claims/${id}/submit`, null);
  }

  openCardTransactions(): Observable<CardTransaction[]> {
    return this.http.get<CardTransaction[]>(`${this.base}/card-transactions`);
  }

  pendingApprovals(): Observable<ClaimSummary[]> {
    return this.http.get<ClaimSummary[]>(`${this.base}/approvals`);
  }

  decide(id: number, decision: 'approve' | 'reject' | 'return', comment: string | null): Observable<Claim> {
    return this.http.post<Claim>(`${this.base}/approvals/${id}/${decision}`, { comment });
  }

  financeClaims(status: ClaimStatus): Observable<ClaimSummary[]> {
    return this.http.get<ClaimSummary[]>(`${this.base}/finance/claims`, { params: new HttpParams().set('status', status) });
  }

  financeCheck(id: number, accept: boolean, comment: string | null): Observable<Claim> {
    return this.http.post<Claim>(`${this.base}/finance/claims/${id}/${accept ? 'check' : 'return'}`, { comment });
  }

  paymentRuns(): Observable<PaymentRun[]> {
    return this.http.get<PaymentRun[]>(`${this.base}/finance/payment-runs`);
  }

  currencies(): Observable<string[]> {
    return this.http.get<string[]>(`${this.base}/reference/currencies`).pipe(shareReplay(1));
  }

  cards(): Observable<CompanyCard[]> {
    return this.http.get<CompanyCard[]>(`${this.base}/admin/cards`);
  }

  linkCard(maskedNumber: string, employeeNumber: string): Observable<CompanyCard> {
    return this.http.post<CompanyCard>(`${this.base}/admin/cards`, { maskedNumber, employeeNumber });
  }

  deactivateCard(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/admin/cards/${id}`);
  }
}
