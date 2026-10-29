import { Link, useParams } from "react-router-dom";
import { api, SubjectRow } from "../api";
import { Pill, useLoad } from "../components/Layout";

export function SitePage() {
  const { siteId } = useParams();
  const [subjects, error] = useLoad(() => api.get<SubjectRow[]>(`/api/sites/${siteId}/subjects`), [siteId]);
  return (
    <section>
      <Link to="/">← Studies</Link>
      <h1>Site subjects</h1>
      {error && <p className="error">{error}</p>}
      <table>
        <thead><tr><th>Subject</th><th>Status</th><th>Consent</th><th>Randomisation</th><th /></tr></thead>
        <tbody>
          {subjects?.map(s => (
            <tr key={s.id}>
              <td><Link to={`/subjects/${s.id}`}>{s.number}</Link></td>
              <td><Pill text={s.status} /></td>
              <td>{s.consentDate}</td>
              <td>{s.randomisationNumber}</td>
              <td><Link to={`/subjects/${s.id}/casebook`}>Casebook</Link></td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}
