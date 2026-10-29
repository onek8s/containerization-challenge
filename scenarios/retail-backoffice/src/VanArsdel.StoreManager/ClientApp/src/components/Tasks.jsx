import { useEffect, useState } from 'react';
import { api } from '../api';

export default function Tasks({ store }) {
  const [tasks, setTasks] = useState(null);
  const [error, setError] = useState(null);
  const [parcels, setParcels] = useState({});

  const load = () =>
    api(`/storeops/tasks?store=${store}`)
      .then(setTasks)
      .catch((e) => setError(e.message));

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [store]);

  const act = async (fn) => {
    setError(null);
    try {
      await fn();
      await load();
    } catch (e) {
      setError(e.message);
    }
  };

  if (!tasks) return <p className="loading">Loading…</p>;
  return (
    <div>
      {error && <p className="error">{error}</p>}
      {tasks.length === 0 && <p>Nothing to pick. 🎉</p>}
      {tasks.map((t) => (
        <section key={t.id} className={'card ' + (new Date(t.dueUtc) < new Date() ? 'late' : '')}>
          <h3>{t.orderNo} <small>{t.deliveryMethod === 'ClickAndCollect' ? 'Click & collect' : 'Ship'} · {t.customerName}</small></h3>
          <p className="muted">Due {new Date(t.dueUtc).toLocaleString('fi-FI')} · {t.status}{t.assignedTo ? ' · ' + t.assignedTo : ''}</p>
          <ul>
            {t.lines.map((l) => (
              <li key={l.sku}>{l.quantity} × {l.sku}</li>
            ))}
          </ul>
          {t.status === 'Open' && <button onClick={() => act(() => api(`/storeops/tasks/${t.id}/start`, { method: 'POST' }))}>Start picking</button>}
          {t.status === 'Picking' && (
            <div className="row">
              <label>Parcels <input type="number" min="1" value={parcels[t.id] || 1} onChange={(e) => setParcels({ ...parcels, [t.id]: +e.target.value })} /></label>
              <button onClick={() => act(() => api(`/storeops/tasks/${t.id}/packed`, { method: 'POST', body: { parcels: parcels[t.id] || 1, lines: [] } }))}>Packed</button>
            </div>
          )}
        </section>
      ))}
    </div>
  );
}
