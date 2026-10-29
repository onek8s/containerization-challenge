import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { api } from "../api";
import { useLoad } from "../components/Layout";

export function CasebookPage() {
  const { subjectId } = useParams();
  const [info, error] = useLoad(() => api.get<{ pages: number }>(`/api/subjects/${subjectId}/casebook`), [subjectId]);
  const [page, setPage] = useState(1);
  const [archive, setArchive] = useState<string>();

  const requestArchive = async () => {
    const r = await api.post<{ id: number; state: string }>(`/api/subjects/${subjectId}/casebook/archive`);
    setArchive(`PDF requested (#${r.id}). The jobs service writes it to the casebook archive.`);
  };

  return (
    <section>
      <Link to={`/subjects/${subjectId}`}>← Subject</Link>
      <h1>Casebook</h1>
      {error && <p className="error">{error}</p>}
      {!info && !error && <p className="muted">Rendering casebook…</p>}
      {info && (
        <>
          <div className="row">
            <button disabled={page <= 1} onClick={() => setPage(p => p - 1)}>‹</button>
            <span>Page {page} of {info.pages}</span>
            <button disabled={page >= info.pages} onClick={() => setPage(p => p + 1)}>›</button>
            <button onClick={requestArchive}>Archive as PDF</button>
            {archive && <span className="muted">{archive}</span>}
          </div>
          <div className="page">
            <img src={`/api/subjects/${subjectId}/casebook/pages/${page}`} alt={`Casebook page ${page}`} />
          </div>
        </>
      )}
    </section>
  );
}
