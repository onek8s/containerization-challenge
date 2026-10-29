import { useEffect, useState } from 'react';
import { api, money } from '../api';

const today = () => new Date().toISOString().substring(0, 10);

export default function CashUp({ store }) {
  const [date, setDate] = useState(today());
  const [counted, setCounted] = useState('');
  const [notes, setNotes] = useState('');
  const [history, setHistory] = useState([]);
  const [message, setMessage] = useState(null);
  const [error, setError] = useState(null);

  const load = () => api(`/storeops/stores/${store}/cash-ups`).then(setHistory).catch((e) => setError(e.message));

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [store]);

  const submit = async (e) => {
    e.preventDefault();
    setError(null);
    try {
      // Tills print amounts with a decimal comma.
      const result = await api(`/storeops/stores/${store}/cash-up`, {
        method: 'POST',
        body: { businessDate: date, countedCash: parseFloat(counted.replace(',', '.')), notes },
      });
      setMessage(`Saved. Difference ${money(result.difference)} €`);
      setCounted('');
      setNotes('');
      await load();
    } catch (err) {
      setError(err.message);
    }
  };

  return (
    <div>
      <form onSubmit={submit} className="form">
        <label>Business day <input type="date" value={date} onChange={(e) => setDate(e.target.value)} /></label>
        <label>Counted cash € <input inputMode="decimal" value={counted} onChange={(e) => setCounted(e.target.value)} required /></label>
        <label>Notes <input value={notes} onChange={(e) => setNotes(e.target.value)} maxLength={500} /></label>
        <button type="submit">Sign off</button>
      </form>
      {message && <p className="ok">{message}</p>}
      {error && <p className="error">{error}</p>}
      <h2>Last two weeks</h2>
      <table>
        <thead><tr><th>Day</th><th className="num">Expected</th><th className="num">Counted</th><th className="num">Difference</th><th>By</th></tr></thead>
        <tbody>
          {history.map((c) => (
            <tr key={c.id} className={Math.abs(c.difference) > 5 ? 'late' : ''}>
              <td>{new Date(c.businessDate).toLocaleDateString('fi-FI')}</td>
              <td className="num">{money(c.expectedCash)}</td>
              <td className="num">{money(c.countedCash)}</td>
              <td className="num">{money(c.difference)}</td>
              <td>{c.countedBy}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
