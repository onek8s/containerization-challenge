import { useEffect, useState } from 'react';
import { api, money } from '../api';
import { connectLive } from '../live';

export default function Dashboard({ store }) {
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);
  const [live, setLive] = useState('connecting');

  const load = () =>
    api(`/storeops/stores/${store}/dashboard`)
      .then(setData)
      .catch((e) => setError(e.message));

  useEffect(() => {
    load();
    connectLive(store, {
      salesUpdated: () => load(),
      taskCreated: () => load(),
      collectionReady: () => load(),
    })
      .then(() => setLive('live'))
      .catch(() => setLive('offline'));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [store]);

  if (error) return <p className="error">{error}</p>;
  if (!data) return <p className="loading">Loading…</p>;

  const vsLastWeek = data.netSalesLastWeek ? ((data.netSales - data.netSalesLastWeek) / data.netSalesLastWeek) * 100 : null;
  return (
    <div>
      <p className={'badge ' + live}>{live}</p>
      <div className="tiles">
        <div className="tile"><span>Net sales</span><b>{money(data.netSales)} €</b>
          {vsLastWeek !== null && <small className={vsLastWeek >= 0 ? 'up' : 'down'}>{vsLastWeek.toFixed(1)} % vs last week</small>}
        </div>
        <div className="tile"><span>Receipts</span><b>{data.receipts}</b><small>avg {money(data.averageBasket)} €</small></div>
        <div className="tile"><span>Visitors</span><b>{data.footfall}</b><small>conversion {data.conversion} %</small></div>
        <div className="tile"><span>Club share</span><b>{data.loyaltyShare} %</b></div>
        <div className="tile"><span>To pick</span><b>{data.openPickTasks}</b>{data.overduePickTasks > 0 && <small className="down">{data.overduePickTasks} late</small>}</div>
        <div className="tile"><span>On the shelf</span><b>{data.waitingCollections}</b></div>
      </div>
      <p className="muted">Last POS file: {data.lastPosFile || '–'}</p>
    </div>
  );
}
