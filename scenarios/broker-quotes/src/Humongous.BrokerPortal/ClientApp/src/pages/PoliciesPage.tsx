import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import type { PolicySummary } from '../api/types';
import { formatDate, formatMoney } from '../format';

export function PoliciesPage() {
  const [policies, setPolicies] = useState<PolicySummary[]>([]);
  const [search, setSearch] = useState('');

  useEffect(() => {
    const timer = setTimeout(() => api.get<PolicySummary[]>(`/api/policies?search=${encodeURIComponent(search)}`).then(setPolicies), 250);
    return () => clearTimeout(timer);
  }, [search]);

  return (
    <>
      <h1>Policies</h1>
      <div className="filters"><input placeholder="Policy number, insured or postcode" value={search} onChange={e => setSearch(e.target.value)} /></div>
      <table className="list">
        <thead><tr><th>Policy</th><th>Insured</th><th>Inception</th><th>Expiry</th><th>Status</th><th className="num">Annual gross</th></tr></thead>
        <tbody>
          {policies.map(p => (
            <tr key={p.id}>
              <td><Link to={`/policies/${p.id}`}>{p.policyNumber}</Link></td>
              <td>{p.insuredName}</td>
              <td>{formatDate(p.inceptionDate)}</td>
              <td>{formatDate(p.expiryDate)}</td>
              <td>{p.status}</td>
              <td className="num">{formatMoney(p.annualGrossPremium)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </>
  );
}
