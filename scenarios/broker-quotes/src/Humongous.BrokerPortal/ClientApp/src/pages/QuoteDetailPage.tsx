import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, ApiError } from '../api/client';
import { useQuoteHub } from '../api/hub';
import type { QuoteDetail } from '../api/types';
import { formatDate, formatDateTime, formatMoney } from '../format';
import { StatusBadge } from '../components/StatusBadge';
import { PremiumTable } from '../components/PremiumTable';
import { Documents } from '../components/Documents';

export function QuoteDetailPage() {
  const { id } = useParams();
  const [quote, setQuote] = useState<QuoteDetail>();
  const [message, setMessage] = useState<string>();
  const [error, setError] = useState<string>();
  const [insuredEmail, setInsuredEmail] = useState('');
  const [sendToInsured, setSendToInsured] = useState(false);

  const load = useCallback(() => api.get<QuoteDetail>(`/api/quotes/${id}`).then(setQuote), [id]);
  useEffect(() => { load(); }, [load]);

  useQuoteHub({
    quoteChanged: n => {
      if (n.quoteId === id) {
        if (n.message) setMessage(n.message);
        load();
      }
    },
    documentReady: n => { if (n.quoteId === id) load(); }
  }, id);

  if (!quote) return <p>Loading...</p>;

  const bindable = (quote.status === 'Quoted' || quote.status === 'Accepted');
  const editable = ['Quoted', 'Referred', 'RatingFailed'].includes(quote.status);

  const bind = async () => {
    setError(undefined);
    try {
      await api.post(`/api/quotes/${quote.id}/bind`, { insuredEmail: insuredEmail || null, sendDocumentsToInsured: sendToInsured });
      setMessage('Bind requested. The policy will be issued in a moment.');
      load();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : String(e));
    }
  };

  const notTakenUp = async () => {
    await api.post(`/api/quotes/${quote.id}/not-taken-up`);
    load();
  };

  return (
    <>
      <h1>{quote.reference} <StatusBadge status={quote.status} /></h1>
      {message && <div className="info">{message}</div>}
      {error && <div className="error">{error}</div>}

      <div className="columns">
        <section>
          <h2>{quote.risk.insured.name}</h2>
          <dl>
            <dt>Address</dt><dd>{quote.risk.insured.addressLine1}, {quote.risk.insured.town} {quote.risk.insured.postcode}</dd>
            <dt>Trade</dt><dd>{quote.risk.insured.tradeCode}</dd>
            <dt>Inception</dt><dd>{formatDate(quote.inceptionDate)}</dd>
            <dt>Valid until</dt><dd>{formatDate(quote.validUntil)}</dd>
            <dt>Product</dt><dd>Business Combined {quote.productVersion}</dd>
            <dt>Your reference</dt><dd>{quote.brokerReference}</dd>
          </dl>
          {quote.status === 'Rating' && <p className="muted">Rating in progress. This page updates by itself.</p>}
          {quote.ratingError && <p className="error">{quote.ratingError}</p>}
          {quote.referrals.length > 0 && (
            <>
              <h3>{quote.status === 'Declined' ? 'Decline reasons' : 'Referral reasons'}</h3>
              <ul>{quote.referrals.map(r => <li key={r.code}>{r.text}</li>)}</ul>
            </>
          )}
          {quote.underwriterNote && <><h3>Underwriter's note</h3><p>{quote.underwriterNote}</p></>}
          {quote.policyId && <p><Link to={`/policies/${quote.policyId}`}>Go to the policy</Link></p>}
        </section>

        <section>
          {quote.premium && <><h2>Premium</h2><PremiumTable premium={quote.premium} /></>}
          {bindable && (
            <div className="panel">
              <h3>Bind cover</h3>
              <label>Client email (optional)<input type="email" value={insuredEmail} onChange={e => setInsuredEmail(e.target.value)} /></label>
              <label className="check"><input type="checkbox" checked={sendToInsured} onChange={e => setSendToInsured(e.target.checked)} /> Send policy documents to the client</label>
              <button className="primary" onClick={bind}>Bind from {formatDate(quote.inceptionDate)} for {formatMoney(quote.premium?.total)}</button>
              <button onClick={notTakenUp}>Not taken up</button>
            </div>
          )}
          {editable && <p><Link className="button" to={`/quotes/${quote.id}/edit`}>Change and requote</Link></p>}
          <h2>Documents</h2>
          <Documents documents={quote.documents} />
          <h2>History</h2>
          <ul className="history">
            {quote.history.map((h, i) => <li key={i}>{formatDateTime(h.atUtc)} {h.event} {h.by && `by ${h.by}`} {h.detail && <span className="muted">{h.detail}</span>}</li>)}
          </ul>
        </section>
      </div>
    </>
  );
}
