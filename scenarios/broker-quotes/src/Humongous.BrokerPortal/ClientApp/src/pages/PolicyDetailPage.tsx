import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, ApiError } from '../api/client';
import { useQuoteHub } from '../api/hub';
import type { PolicyDetail, Risk } from '../api/types';
import { formatDate, formatMoney, today } from '../format';
import { PremiumTable } from '../components/PremiumTable';
import { Documents } from '../components/Documents';

const transactionText: Record<string, string> = {
  NewBusiness: 'New business',
  MidTermAdjustment: 'Mid-term change',
  Renewal: 'Renewal',
  Cancellation: 'Cancellation'
};

export function PolicyDetailPage() {
  const { id } = useParams();
  const [policy, setPolicy] = useState<PolicyDetail>();
  const [message, setMessage] = useState<string>();
  const [mode, setMode] = useState<'none' | 'change' | 'cancel'>('none');
  const [effective, setEffective] = useState(today());
  const [reason, setReason] = useState('');
  const [draft, setDraft] = useState<Risk>();

  const load = useCallback(() => api.get<PolicyDetail>(`/api/policies/${id}`).then(p => { setPolicy(p); setDraft(p.risk); }), [id]);
  useEffect(() => { load(); }, [load]);
  useQuoteHub({
    policyChanged: n => { if (n.policyId === id) { if (n.message) setMessage(n.message); load(); } },
    documentReady: n => { if (n.policyId === id) load(); }
  });

  if (!policy || !draft) return <p>Loading...</p>;

  const submit = async () => {
    try {
      if (mode === 'change') {
        await api.post(`/api/policies/${policy.id}/adjustments`, { effectiveDate: effective, reason, risk: draft });
        setMessage('Change submitted. The endorsement follows shortly.');
      } else {
        await api.post(`/api/policies/${policy.id}/cancellation`, { effectiveDate: effective, reason });
        setMessage('Cancellation submitted.');
      }
      setMode('none');
    } catch (e) {
      setMessage(e instanceof ApiError ? e.message : String(e));
    }
  };

  const md = draft.materialDamage;
  const el = draft.employersLiability;

  return (
    <>
      <h1>{policy.policyNumber} <span className="badge">{policy.status}</span></h1>
      {message && <div className="info">{message}</div>}
      <div className="columns">
        <section>
          <dl>
            <dt>Insured</dt><dd>{policy.risk.insured.name}</dd>
            <dt>Period</dt><dd>{formatDate(policy.inceptionDate)} to {formatDate(policy.expiryDate)}</dd>
            <dt>Product</dt><dd>Business Combined {policy.productVersion}</dd>
            <dt>Original quote</dt><dd><Link to={`/quotes/${policy.quoteId}`}>View</Link></dd>
            {policy.renewalQuoteId && <><dt>Renewal</dt><dd><Link to={`/quotes/${policy.renewalQuoteId}`}>Renewal terms</Link></dd></>}
          </dl>
          <h2>Transactions</h2>
          <table className="list">
            <thead><tr><th>Type</th><th>Effective</th><th>Reason</th><th className="num">Gross</th><th className="num">IPT</th></tr></thead>
            <tbody>
              {policy.transactions.map(t => (
                <tr key={t.id}><td>{transactionText[t.type]}</td><td>{formatDate(t.effectiveDate)}</td><td>{t.reason}</td>
                  <td className="num">{formatMoney(t.grossPremium)}</td><td className="num">{formatMoney(t.ipt)}</td></tr>
              ))}
            </tbody>
          </table>
          {policy.status === 'Live' && mode === 'none' && (
            <p>
              <button onClick={() => setMode('change')}>Change cover</button>
              <button onClick={() => setMode('cancel')}>Cancel policy</button>
            </p>
          )}
          {mode !== 'none' && (
            <div className="panel">
              <h3>{mode === 'change' ? 'Mid-term change' : 'Cancel the policy'}</h3>
              <label>Effective date<input type="date" value={effective} onChange={e => setEffective(e.target.value)} /></label>
              <label>Reason<input value={reason} onChange={e => setReason(e.target.value)} /></label>
              {mode === 'change' && md && (
                <>
                  <label>Contents<input type="number" value={md.contents} onChange={e => setDraft({ ...draft, materialDamage: { ...md, contents: Number(e.target.value) } })} /></label>
                  <label>Stock<input type="number" value={md.stock} onChange={e => setDraft({ ...draft, materialDamage: { ...md, stock: Number(e.target.value) } })} /></label>
                </>
              )}
              {mode === 'change' && el && (
                <>
                  <label>Employees<input type="number" value={el.employees} onChange={e => setDraft({ ...draft, employersLiability: { ...el, employees: Number(e.target.value) } })} /></label>
                  <label>Wageroll<input type="number" value={el.wageroll} onChange={e => setDraft({ ...draft, employersLiability: { ...el, wageroll: Number(e.target.value) } })} /></label>
                </>
              )}
              <button className="primary" onClick={submit}>Submit</button>
              <button onClick={() => setMode('none')}>Back</button>
            </div>
          )}
        </section>
        <section>
          <h2>Annual premium</h2>
          <PremiumTable premium={policy.premium} />
          <h2>Documents</h2>
          <Documents documents={policy.documents} />
        </section>
      </div>
    </>
  );
}
