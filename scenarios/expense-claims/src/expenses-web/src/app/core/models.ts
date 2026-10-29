export type ClaimStatus = 'Draft' | 'Submitted' | 'Returned' | 'Approved' | 'Rejected' | 'Checked' | 'InPayment' | 'Paid';
export type ExpenseCategory = 'Travel' | 'Accommodation' | 'Meals' | 'Mileage' | 'PerDiem' | 'Training' | 'Equipment' | 'Representation' | 'Other';
export type PerDiemType = 'None' | 'Partial' | 'Full' | 'Abroad';
export type ApprovalDecision = 'Pending' | 'Approved' | 'Rejected' | 'Returned';

export const CATEGORIES: ExpenseCategory[] = ['Travel', 'Accommodation', 'Meals', 'Mileage', 'PerDiem', 'Training', 'Equipment', 'Representation', 'Other'];

export const CATEGORY_NAMES: Record<ExpenseCategory, string> = {
  Travel: 'Travel tickets',
  Accommodation: 'Accommodation',
  Meals: 'Meals',
  Mileage: 'Mileage (own car)',
  PerDiem: 'Daily allowance',
  Training: 'Training and seminars',
  Equipment: 'Small equipment',
  Representation: 'Representation',
  Other: 'Other'
};

export interface Me {
  upn: string;
  employeeNumber: string | null;
  displayName: string;
  costCenter: string | null;
  hasBankAccount: boolean;
  isApprover: boolean;
  isFinance: boolean;
  isAdministrator: boolean;
}

export interface ClaimSummary {
  id: number;
  number: string;
  employee: string;
  purpose: string;
  status: ClaimStatus;
  total: number;
  createdAt: string;
  submittedAt: string | null;
  lineCount: number;
}

export interface Receipt {
  id: number;
  fileName: string;
  contentType: string;
  length: number;
}

export interface CardTransaction {
  id: number;
  card: string;
  transactionDate: string;
  merchant: string;
  city: string | null;
  country: string | null;
  merchantCategoryCode: string;
  suggestedCategory: ExpenseCategory;
  originalAmount: number;
  originalCurrency: string;
  billedAmountEur: number;
}

export interface ClaimLine {
  id: number;
  lineNumber: number;
  expenseDate: string;
  category: ExpenseCategory;
  description: string;
  amount: number;
  currency: string;
  exchangeRate: number;
  amountEur: number;
  kilometres: number | null;
  route: string | null;
  perDiem: PerDiemType;
  country: string | null;
  attendees: number;
  vatRate: number;
  vatAmountEur: number;
  paidByCompany: boolean;
  cardTransaction: CardTransaction | null;
  receipt: Receipt | null;
}

export interface PolicyFinding {
  lineNumber: number;
  severity: 'Warning' | 'Error';
  code: string;
  message: string;
}

export interface Approval {
  level: number;
  approver: string;
  decision: ApprovalDecision;
  comment: string | null;
  decidedAt: string | null;
}

export interface ClaimEvent {
  at: string;
  actor: string;
  action: string;
  details: string | null;
}

export interface Claim {
  id: number;
  number: string;
  employeeNumber: string;
  employee: string;
  purpose: string;
  costCenter: string;
  projectCode: string | null;
  status: ClaimStatus;
  total: number;
  reimbursable: number;
  createdAt: string;
  submittedAt: string | null;
  paidAt: string | null;
  rowVersion: string;
  canEdit: boolean;
  canDecide: boolean;
  lines: ClaimLine[];
  approvals: Approval[];
  findings: PolicyFinding[];
  events: ClaimEvent[];
}

export interface ClaimLineRequest {
  expenseDate: string;
  category: ExpenseCategory;
  description: string;
  amount: number;
  currency: string | null;
  kilometres: number | null;
  route: string | null;
  perDiem: PerDiemType;
  country: string | null;
  attendees: number;
}

export interface PaymentRun {
  id: number;
  messageId: string;
  createdAt: string;
  executionDate: string;
  paymentCount: number;
  total: number;
  status: 'Created' | 'Sent' | 'Accepted' | 'Failed';
  bankReference: string | null;
  error: string | null;
}

export interface CompanyCard {
  id: number;
  maskedNumber: string;
  employeeNumber: string;
  employee: string;
  active: boolean;
}

export interface ProblemDetails {
  title?: string;
  status?: number;
  errors?: Record<string, string[]>;
}
