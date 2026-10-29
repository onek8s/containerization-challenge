import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { api, QueryRow } from "../api";
import { Pill, useLoad } from "../components/Layout";

const states = ["Open", "Answered", "Closed", "Cancelled"];

export function QueriesPage() {
  const { studyId } = useParams();
  const [state, setState] = useState("Answered");
  const [rows, error] = useLoad(() => api.get<QueryRow[]>(`/api/studies/${studyId}/queries?state=${state}`), [studyId, state]);

  const csv = () => {
    const lines = ["id;subject;site;visit;form;field;state;origin;opened;age;text"].concat(
      (rows ?? []).map(r => [r.id, r.subject, r.site, r.visit, r.form, r.fieldCode, r.state, r.origin, r.openedAt, r.ageDays, `"${r.text.replace(/"/g, '""')}"`].join(";"))
    );
    const url = URL.createObjectURL(new Blob(["﻿" + lines.join("\r\n")], { type: "text/csv" }));
    const a = document.createElement("a");
    a.href = url;
    a.download = `queries-${state.toLowerCase()}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  };

  return (
    <section>
      <Link to={`/studies/${studyId}`}>← Study</Link>
      <h1>Queries</h1>
      <div className="row">
        {states.map(s => (
          <button key={s} className={s === state ? "" : "secondary"} onClick={() => setState(s)}>{s}</button>
        ))}
        <button className="link" onClick={csv}>Export CSV</button>
      </div>
      {error && <p className="error">{error}</p>}
      <table>
        <thead><tr><th>#</th><th>Subject</th><th>Site</th><th>Visit / form</th><th>Field</th><th>Query</th><th>Origin</th><th>Age</th></tr></thead>
        <tbody>
          {rows?.map(r => (
            <tr key={r.id} className={r.ageDays > 14 ? "late" : ""}>
              <td>{r.id}</td>
              <td><Link to={`/subjects/${r.subjectId}`}>{r.subject}</Link></td>
              <td>{r.site}</td>
              <td>{r.visit} / {r.form}{r.row > 1 ? ` #${r.row}` : ""}</td>
              <td><code>{r.fieldCode}</code></td>
              <td>{r.text}</td>
              <td><Pill text={r.origin} tone="" /></td>
              <td>{r.ageDays} d</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}
