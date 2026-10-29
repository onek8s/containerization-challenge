import { Link, useNavigate, useParams } from "react-router-dom";
import { api, SiteMetrics, Study } from "../api";
import { useLoad } from "../components/Layout";
import { useNotifications } from "../notifications";

export function Dashboard() {
  const params = useParams();
  const navigate = useNavigate();
  const [studies] = useLoad(() => api.get<Study[]>("/api/studies"), []);
  const studyId = params.studyId ? Number(params.studyId) : studies?.[0]?.id;
  const study = studies?.find(s => s.id === studyId);
  const [sites, error] = useLoad(() => (studyId ? api.get<SiteMetrics[]>(`/api/studies/${studyId}/sites`) : Promise.resolve([])), [studyId]);
  const feed = useNotifications(studyId);

  if (!studies) return <p className="muted">Loading…</p>;
  const total = (k: keyof SiteMetrics) => (sites ?? []).reduce((n, s) => n + (s[k] as number), 0);

  return (
    <div className="grid-main">
      <section>
        <div className="row">
          <select value={studyId} onChange={e => navigate(`/studies/${e.target.value}`)}>
            {studies.map(s => <option key={s.id} value={s.id}>{s.code}</option>)}
          </select>
          {study && <Link to={`/studies/${study.id}/queries`}>Query listing</Link>}
        </div>
        {study && (
          <>
            <h1>{study.code}</h1>
            <p className="muted">{study.title}</p>
            <div className="kpis">
              <div><b>{total("randomised")}</b> / {study.targetEnrolment} randomised</div>
              <div><b>{total("subjects")}</b> subjects</div>
              <div><b>{total("valuesToVerify")}</b> values to verify</div>
              <div><b>{total("openQueries")}</b> open queries</div>
              <div><b>{total("answeredQueries")}</b> answers to review</div>
            </div>
          </>
        )}
        {error && <p className="error">{error}</p>}
        <table>
          <thead>
            <tr><th>Site</th><th>Investigator</th><th>Subjects</th><th>Randomised</th><th>To verify</th><th>Open</th><th>Answered</th></tr>
          </thead>
          <tbody>
            {sites?.map(s => (
              <tr key={s.id}>
                <td><Link to={`/sites/${s.id}`}>{s.number}</Link> {s.name}</td>
                <td>{s.investigator}</td>
                <td>{s.subjects}</td>
                <td>{s.randomised}</td>
                <td>{s.valuesToVerify}</td>
                <td className={s.openQueries ? "warn" : ""}>{s.openQueries}</td>
                <td className={s.answeredQueries ? "warn" : ""}>{s.answeredQueries}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
      <aside>
        <h2>Activity</h2>
        <ul className="feed">
          {feed.map(n => (
            <li key={n.id}>
              <span className="muted small">{new Date(n.at).toLocaleString()}</span>
              <div>{n.subjectId ? <Link to={`/subjects/${n.subjectId}`}>{n.text}</Link> : n.text}</div>
            </li>
          ))}
        </ul>
      </aside>
    </div>
  );
}
