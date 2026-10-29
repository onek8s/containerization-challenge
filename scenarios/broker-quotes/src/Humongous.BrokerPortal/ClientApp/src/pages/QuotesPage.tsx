import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import type { QuoteSummary } from '../api/types';
import { formatDate, formatMoney, statusText } from '../format';
import { StatusBadge } from '../components/StatusBadge';

export function QuotesPage() {
  const [quotes, setQuotes] = useState<QuoteSummary[]>([]);
  const [status, setStatus] = useState('');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  useEffect(() => {
    const query = new URLSearchParams({ page: String(page) });
    if (status) query.set('status', status);
    if (search) query.set('search', search);
    const timer = setTimeout(() => api.get<QuoteSummary[]>(`/api/quotes?${query}`).then(setQuotes), 250);
    return () => clearTimeout(timer);
  }, [status, search, page]);

  return (
    <>
      <h1>Quotes</h1>
      <div className="filters">
        <input placeholder="Reference, insured or postcode" value={search} onChange={e => { setSearch(e.target.value); setPage(1); }} />
        <select value={status} onChange={e => { setStatus(e.target.value); setPage(1); }}>
          <option value="">All statuses</option>
          {Object.entries(statusText).filter(([k]) => k !== 'Draft').map(([k, v]) => <option key={k} value={k}>{v}</option>)}
        </select>
      </div>
      <table className="list">
        <thead><tr><th>Reference</th><th>Insured</th><th>Postcode</th><th>Inception</th><th>Valid until</th><th>Status</th><th className="num">Total</th></tr></thead>
        <tbody>
          {quotes.map(q => (
            <tr key={q.id}>
              <td><Link to={`/quotes/${q.id}`}>{q.reference}</Link>{q.isRenewal && <span className="note"> renewal</span>}</td>
              <td>{q.insuredName}</td>
              <td>{q.postcode}</td>
              <td>{formatDate(q.inceptionDate)}</td>
              <td>{formatDate(q.validUntil)}</td>
              <td><StatusBadge status={q.status} /></td>
              <td className="num">{formatMoney(q.total)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <div className="pager">
        <button disabled={page === 1} onClick={() => setPage(p => p - 1)}>Previous</button>
        <span>Page {page}</span>
        <button disabled={quotes.length < 25} onClick={() => setPage(p => p + 1)}>Next</button>
      </div>
    </>
  );
}
