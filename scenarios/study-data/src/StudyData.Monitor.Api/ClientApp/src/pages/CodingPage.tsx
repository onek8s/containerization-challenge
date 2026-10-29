import { useState } from "react";
import { Link } from "react-router-dom";
import { api, Candidate, CodingRow } from "../api";
import { useLoad } from "../components/Layout";

export function CodingPage() {
  const [state, setState] = useState("NeedsReview");
  const [rows, error, reload] = useLoad(() => api.get<CodingRow[]>(`/api/coding?state=${state}`), [state]);
  const [selected, setSelected] = useState<CodingRow>();
  const [candidates, setCandidates] = useState<Candidate[]>([]);
  const [message, setMessage] = useState<string>();

  const pick = async (row: CodingRow) => {
    setSelected(row);
    setCandidates(await api.get<Candidate[]>(`/api/coding/${row.id}/candidates`));
  };

  const assign = async (code: string) => {
    if (!selected) return;
    await api.post(`/api/coding/${selected.id}/assign`, { code });
    setSelected(undefined);
    reload();
  };

  const run = async () => {
    const r = await api.post<{ auto: number; review: number }>("/api/coding/run");
    setMessage(`${r.auto} autocoded, ${r.review} need review.`);
    reload();
  };

  return (
    <div className="grid-main">
      <section>
        <h1>Medical coding</h1>
        <div className="row">
          {["Pending", "NeedsReview", "AutoCoded", "Coded", "QueryRaised"].map(s => (
            <button key={s} className={s === state ? "" : "secondary"} onClick={() => setState(s)}>{s}</button>
          ))}
          <button className="link" onClick={run}>Run autocoder</button>
        </div>
        {message && <div className="notice">{message}</div>}
        {error && <p className="error">{error}</p>}
        <table>
          <thead><tr><th>Subject</th><th>Dictionary</th><th>Verbatim</th><th>Code</th><th>Score</th></tr></thead>
          <tbody>
            {rows?.map(r => (
              <tr key={r.id} className={selected?.id === r.id ? "selected" : ""} onClick={() => pick(r)}>
                <td><Link to={`/subjects/${r.subjectId}`}>{r.subject}</Link></td>
                <td>{r.dictionary}</td>
                <td>{r.verbatim}</td>
                <td>{r.code} {r.term}</td>
                <td>{r.score}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
      <aside>
        {selected && (
          <>
            <h2>“{selected.verbatim}”</h2>
            <ul className="candidates">
              {candidates.map(c => (
                <li key={c.code}>
                  <button className="link" onClick={() => assign(c.code)}>{c.code} {c.term}</button>
                  <div className="small muted">{c.preferredTerm} · {c.hierarchy} · score {c.score}</div>
                </li>
              ))}
            </ul>
            <button
              className="secondary"
              onClick={async () => {
                await api.post(`/api/coding/${selected.id}/query`, { text: `Please clarify the term "${selected.verbatim}".` });
                setSelected(undefined);
                reload();
              }}
            >
              Ask the site to clarify
            </button>
          </>
        )}
      </aside>
    </div>
  );
}
