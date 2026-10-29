import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import { useQuoteHub } from '../api/hub';
import type { Dashboard } from '../api/types';
import { formatDate, formatMoney } from '../format';
import { StatusBadge } from '../components/StatusBadge';

export function DashboardPage() {
  const [data, setData] = useState<Dashboard>();
  const load = useCallback(() => api.get<Dashboard>('/api/dashboard').then(setData), []);
  useEffect(() => { load(); }, [load]);
  useQuoteHub({ quoteChanged: () => load(), policyChanged: () => load() });

  if (!data) return <p>Loading...</p>;
  const count = (s: string) => data.quotesByStatus.find(x => x.status === s)?.count ?? 0;

  return (
    <>
      <h1>Dashboard</h1>
      <div className="tiles">
        <div className="tile"><span>Open quotes</span><strong>{count('Quoted') + count('Accepted')}</strong></div>
        <div className="tile"><span>With underwriters</span><strong>{count('Referred')}</strong></div>
        <div className="tile"><span>Live policies</span><strong>{data.policiesLive}</strong></div>
        <div className="tile"><span>Renewals in 60 days</span><strong>{data.renewalsDueIn60Days}</strong></div>
        <div className="tile"><span>Written this month</span><strong>{formatMoney(data.boundGrossPremiumMonthToDate)}</strong></div>
      </div>
      <h2>Recent quotes</h2>
      <table className="list">
        <thead><tr><th>Reference</th><th>Insured</th><th>Inception</th><th>Status</th><th className="num">Total</th></tr></thead>
        <tbody>
          {data.recentQuotes.map(q => (
            <tr key={q.id}>
              <td><Link to={`/quotes/${q.id}`}>{q.reference}</Link>{q.isRenewal && <span className="note"> renewal</span>}</td>
              <td>{q.insuredName}</td>
              <td>{formatDate(q.inceptionDate)}</td>
              <td><StatusBadge status={q.status} /></td>
              <td className="num">{formatMoney(q.total)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </>
  );
}
