export type QuoteStatus =
  | 'Draft' | 'Rating' | 'Quoted' | 'Referred' | 'Accepted' | 'Declined'
  | 'BindRequested' | 'Bound' | 'Expired' | 'NotTakenUp' | 'RatingFailed';

export type SectionCode = 'MaterialDamage' | 'BusinessInterruption' | 'EmployersLiability' | 'PublicLiability';

export interface Insured {
  name: string;
  addressLine1: string;
  addressLine2?: string | null;
  town: string;
  postcode: string;
  tradeCode: string;
  yearsTrading: number;
  turnover: number;
  contactEmail?: string | null;
}

export interface Premises {
  construction: 'Standard' | 'TimberFrame' | 'NonStandard';
  roof: 'Standard' | 'FlatFelt' | 'Thatch' | 'Other';
  yearBuilt: number;
  occupied: boolean;
  listedBuilding: boolean;
  floodInLast10Years: boolean;
  alarm: 'None' | 'Audible' | 'RemoteSignalling';
}

export interface MaterialDamage { buildings: number; contents: number; stock: number; computerEquipment: number; excess: number }
export interface BusinessInterruption { grossProfit: number; indemnityPeriodMonths: number }
export interface EmployersLiability { employees: number; wageroll: number; manualWageroll: number }
export interface PublicLiability { limitOfIndemnity: number; productsSupplied: boolean; workAwayFromPremises: boolean }
export interface Claim { date: string; section: SectionCode; amount: number; description: string; open: boolean }

export interface Risk {
  insured: Insured;
  premises: Premises;
  materialDamage?: MaterialDamage | null;
  businessInterruption?: BusinessInterruption | null;
  employersLiability?: EmployersLiability | null;
  publicLiability?: PublicLiability | null;
  claims: Claim[];
  previouslyDeclinedOrCancelled: boolean;
}

export interface SectionPremium { section: SectionCode; technical: number; minimumApplied: number; loadingPercent: number; net: number; gross: number }

export interface Premium {
  sections: SectionPremium[];
  netPremium: number;
  commissionRate: number;
  commission: number;
  grossPremium: number;
  iptRate: number;
  ipt: number;
  fee: number;
  total: number;
}

export interface QuoteSummary {
  id: string;
  reference: string;
  agencyCode: string;
  insuredName: string;
  postcode: string;
  tradeCode: string;
  status: QuoteStatus;
  total?: number | null;
  inceptionDate: string;
  createdUtc: string;
  validUntil?: string | null;
  isRenewal: boolean;
}

export interface Referral { code: string; text: string; source: string }
export interface DocumentInfo { id: string; documentType: string; fileName: string; createdUtc: string }
export interface HistoryEntry { atUtc: string; event: string; by?: string; detail?: string }

export interface QuoteDetail {
  id: string;
  reference: string;
  agencyCode: string;
  agencyName: string;
  brokerReference?: string | null;
  status: QuoteStatus;
  productVersion: string;
  inceptionDate: string;
  validUntil?: string | null;
  commissionRate: number;
  risk: Risk;
  premium?: Premium | null;
  referrals: Referral[];
  clauses: string[];
  ratingTables?: string | null;
  ratingError?: string | null;
  underwriterNote?: string | null;
  policyId?: string | null;
  renewalOfPolicyId?: string | null;
  documents: DocumentInfo[];
  history: HistoryEntry[];
}

export interface PolicySummary {
  id: string;
  policyNumber: string;
  agencyCode: string;
  insuredName: string;
  status: 'Live' | 'Cancelled' | 'Lapsed' | 'Renewed';
  inceptionDate: string;
  expiryDate: string;
  annualGrossPremium: number;
}

export interface PolicyTransaction {
  id: string;
  type: 'NewBusiness' | 'MidTermAdjustment' | 'Renewal' | 'Cancellation';
  effectiveDate: string;
  grossPremium: number;
  ipt: number;
  commission: number;
  reason?: string | null;
  createdUtc: string;
}

export interface PolicyDetail extends Omit<PolicySummary, 'insuredName' | 'annualGrossPremium'> {
  productVersion: string;
  risk: Risk;
  premium: Premium;
  clauses: string[];
  quoteId: string;
  renewalQuoteId?: string | null;
  transactions: PolicyTransaction[];
  documents: DocumentInfo[];
}

export interface Trade { code: string; description: string; group: string; acceptance: string }
export interface Catalogue { trades: Trade[]; publicLiabilityLimits: number[] }

export interface Dashboard {
  quotesByStatus: { status: string; count: number }[];
  boundGrossPremiumMonthToDate: number;
  policiesLive: number;
  renewalsDueIn60Days: number;
  recentQuotes: QuoteSummary[];
}

export interface User { name: string; email: string; agency: string; roles: string[] }

export interface QuoteNotice { quoteId: string; reference: string; agencyCode: string; status: QuoteStatus; total?: number | null; message?: string | null }
export interface PolicyNotice { policyId: string; policyNumber: string; agencyCode: string; event: string; message?: string | null }
export interface DocumentNotice { documentId: string; documentType: string; quoteId?: string | null; policyId?: string | null; fileName: string }

export interface ProblemDetails { title?: string; detail?: string; errors?: Record<string, string[]> }
