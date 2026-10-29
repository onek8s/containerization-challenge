import { useEffect, useState } from 'react';
import { api } from '../api';

export default function Collections({ store }) {
  const [list, setList] = useState(null);
  const [orderNo, setOrderNo] = useState('');
  const [found, setFound] = useState(null);
  const [error, setError] = useState(null);

  const load = () => api(`/storeops/collections?store=${store}`).then(setList).catch((e) => setError(e.message));

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [store]);

  const find = async (e) => {
    e.preventDefault();
    setError(null);
    setFound(null);
    try {
      setFound(await api(`/storeops/collections/find/${encodeURIComponent(orderNo.trim())}?store=${store}`));
    } catch (err) {
      setError(err.message);
    }
  };

  const handover = async (c) => {
    if (!window.confirm(`Hand over ${c.orderNo}? Check the customer's ID.`)) return;
    try {
      await api(`/storeops/collections/${c.id}/handover`, { method: 'POST' });
      setFound(null);
      setOrderNo('');
      await load();
    } catch (err) {
      setError(err.message);
    }
  };

  return (
    <div>
      <form onSubmit={find} className="row">
        <input placeholder="Order number" value={orderNo} onChange={(e) => setOrderNo(e.target.value)} autoFocus />
        <button type="submit">Find</button>
      </form>
      {error && <p className="error">{error}</p>}
      {found && (
        <section className="card">
          <h3>{found.orderNo} <small>{found.status}</small></h3>
          <p>{found.customerEmail} {found.customerPhone}</p>
          {found.status === 'Waiting' && <button onClick={() => handover(found)}>Hand over</button>}
        </section>
      )}
      <h2>On the shelf</h2>
      {list && (
        <table>
          <thead><tr><th>Order</th><th>Ready</th><th>Collect by</th></tr></thead>
          <tbody>
            {list.map((c) => (
              <tr key={c.id} className={new Date(c.collectBy) < new Date() ? 'late' : ''}>
                <td>{c.orderNo}</td>
                <td>{new Date(c.readyUtc).toLocaleDateString('fi-FI')}</td>
                <td>{new Date(c.collectBy).toLocaleDateString('fi-FI')}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
