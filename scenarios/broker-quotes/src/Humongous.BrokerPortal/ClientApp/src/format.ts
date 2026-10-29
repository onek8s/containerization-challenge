const money = new Intl.NumberFormat('en-GB', { style: 'currency', currency: 'GBP' });
const date = new Intl.DateTimeFormat('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
const dateTime = new Intl.DateTimeFormat('en-GB', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' });

export const formatMoney = (value?: number | null) => (value == null ? '' : money.format(value));
export const formatDate = (value?: string | null) => (value ? date.format(new Date(value)) : '');
export const formatDateTime = (value?: string | null) => (value ? dateTime.format(new Date(value)) : '');
export const formatPercent = (value: number) => `${(value * 100).toFixed(1)}%`;

export const sectionNames: Record<string, string> = {
  MaterialDamage: 'Material damage',
  BusinessInterruption: 'Business interruption',
  EmployersLiability: "Employers' liability",
  PublicLiability: 'Public and products liability'
};

export const statusText: Record<string, string> = {
  Rating: 'Rating...',
  Quoted: 'Quoted',
  Referred: 'Referred to underwriter',
  Accepted: 'Terms agreed',
  Declined: 'Declined',
  BindRequested: 'Binding...',
  Bound: 'On cover',
  Expired: 'Expired',
  NotTakenUp: 'Not taken up',
  RatingFailed: 'Rating failed',
  Draft: 'Draft'
};

export function today(offsetDays = 0): string {
  const d = new Date();
  d.setDate(d.getDate() + offsetDays);
  return d.toISOString().slice(0, 10);
}
