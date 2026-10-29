import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { api, Form, LabRow, SubjectDetail } from "../api";
import { Pill, useLoad } from "../components/Layout";

export function SubjectPage() {
  const { subjectId } = useParams();
  const [subject, error, reload] = useLoad(() => api.get<SubjectDetail>(`/api/subjects/${subjectId}`), [subjectId]);
  const [labs] = useLoad(() => api.get<LabRow[]>(`/api/subjects/${subjectId}/labs`), [subjectId]);
  const [openForm, setOpenForm] = useState<number>();
  const [message, setMessage] = useState<string>();

  if (error) return <p className="error">{error}</p>;
  if (!subject) return <p className="muted">Loading…</p>;

  const act = async (work: () => Promise<unknown>, done: string) => {
    try {
      await work();
      setMessage(done);
      reload();
    } catch (e) {
      setMessage((e as Error).message);
    }
  };

  return (
    <section>
      <Link to={`/sites/${subject.site.id}`}>← Site {subject.site.number}</Link>
      <h1>
        {subject.number} <Pill text={subject.status} />
      </h1>
      <p className="muted">
        {subject.study.code} · {subject.site.name} · {subject.randomisationNumber ?? "not randomised"} ·{" "}
        <Link to={`/subjects/${subject.id}/casebook`}>Casebook</Link>
      </p>
      {message && <div className="notice">{message}</div>}

      {subject.visits.map(v => (
        <div key={v.id} className={`visit ${v.outOfWindow ? "oow" : ""}`}>
          <h2>
            {v.name} <span className="muted small">{v.date ?? "no date"}</span> {v.outOfWindow && <Pill text="out of window" tone="warn" />}
          </h2>
          <div className="forms">
            {v.forms.filter(f => f.status !== "NotStarted").map(f => (
              <button key={f.id} className={`form-chip ${openForm === f.id ? "active" : ""}`} onClick={() => setOpenForm(openForm === f.id ? undefined : f.id)}>
                {f.name}{f.code === "AE" || f.code === "CM" ? ` #${f.row}` : ""} <Pill text={f.status} />
                {f.queries.some(q => q.state === "Open" || q.state === "Answered") && <Pill text="query" tone="warn" />}
              </button>
            ))}
          </div>
          {v.forms.filter(f => f.id === openForm).map(f => (
            <FormReview key={f.id} form={f} act={act} />
          ))}
        </div>
      ))}

      {labs && labs.length > 0 && (
        <>
          <h2>Central laboratory</h2>
          <table>
            <thead><tr><th>Visit</th><th>Collected</th><th>Test</th><th>Result</th><th>Range</th><th>Flag</th><th>Grade</th></tr></thead>
            <tbody>
              {labs.map(l => (
                <tr key={l.accession + l.testCode} className={l.grade && l.grade >= 3 ? "late" : ""}>
                  <td>{l.visitCode}</td>
                  <td>{new Date(l.collectedAt).toLocaleString()}</td>
                  <td>{l.testName}</td>
                  <td>{l.value} {l.unit}</td>
                  <td>{l.lowerLimit}–{l.upperLimit}</td>
                  <td>{l.flag}</td>
                  <td>{l.grade}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </section>
  );
}

function FormReview({ form, act }: { form: Form; act: (w: () => Promise<unknown>, done: string) => Promise<void> }) {
  const [queryField, setQueryField] = useState<string>();
  const [text, setText] = useState("");
  const [reply, setReply] = useState<Record<number, string>>({});

  return (
    <div className="review">
      <div className="muted small">
        Saved {form.lastSavedAt && new Date(form.lastSavedAt).toLocaleString()} by {form.lastSavedBy}
        {form.derivedWith && <> · calculated with {form.derivedWith}</>}
        {form.signedBy && <> · signed by {form.signedBy}</>}
      </div>
      <table>
        <thead><tr><th>Field</th><th>Value</th><th>SDV</th><th /></tr></thead>
        <tbody>
          {form.fields.map(f => (
            <tr key={f.code}>
              <td><code>{f.code}</code> {f.label}</td>
              <td>
                {f.value ?? "—"} {f.value && f.unit}
                {f.coding?.code && <div className="small muted">{f.coding.code} {f.coding.term}</div>}
              </td>
              <td>{f.sdv && f.type !== "Derived" ? (f.verified ? "✓" : f.raw ? "pending" : "") : ""}</td>
              <td><button className="link" onClick={() => setQueryField(f.code)}>Query</button></td>
            </tr>
          ))}
        </tbody>
      </table>
      <div className="row">
        <button onClick={() => act(() => api.post(`/api/forms/${form.id}/verify`, { fields: null }), "Form verified.")}>Verify all</button>
      </div>
      {queryField && (
        <div className="row">
          <span>Query on <code>{queryField}</code></span>
          <input className="wide" value={text} onChange={e => setText(e.target.value)} placeholder="Question to the site" />
          <button
            onClick={() =>
              act(() => api.post(`/api/forms/${form.id}/queries`, { field: queryField, text }), "Query sent.").then(() => {
                setQueryField(undefined);
                setText("");
              })
            }
          >
            Send
          </button>
        </div>
      )}
      {form.queries.map(q => (
        <div key={q.id} className={`query ${q.state.toLowerCase()}`}>
          <b>#{q.id} {q.fieldCode}</b> <span className="pill">{q.state}</span> <span className="muted small">{q.origin}</span>
          {q.messages.map((m, i) => (
            <div key={i} className="small"><span className="muted">{new Date(m.at).toLocaleString()} {m.user}:</span> {m.text}</div>
          ))}
          {(q.state === "Open" || q.state === "Answered") && (
            <div className="row">
              <input value={reply[q.id] ?? ""} onChange={e => setReply({ ...reply, [q.id]: e.target.value })} placeholder="Comment" />
              <button onClick={() => act(() => api.post(`/api/queries/${q.id}/close`, { text: reply[q.id] }), "Query closed.")}>Close</button>
              {q.state === "Answered" && (
                <button onClick={() => act(() => api.post(`/api/queries/${q.id}/reopen`, { text: reply[q.id] }), "Query re-opened.")}>Re-open</button>
              )}
              <button className="link" onClick={() => act(() => api.post(`/api/queries/${q.id}/cancel`, { text: reply[q.id] }), "Query cancelled.")}>Cancel</button>
            </div>
          )}
        </div>
      ))}
    </div>
  );
}
