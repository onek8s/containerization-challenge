import type { DocumentInfo } from '../api/types';
import { formatDateTime } from '../format';

export function Documents({ documents }: { documents: DocumentInfo[] }) {
  if (documents.length === 0) return <p className="muted">No documents yet.</p>;
  return (
    <ul className="documents">
      {documents.map(d => (
        <li key={d.id}>
          <a href={`/api/documents/${d.id}`} target="_blank" rel="noreferrer">{d.fileName}</a>
          <span className="muted"> {formatDateTime(d.createdUtc)}</span>
        </li>
      ))}
    </ul>
  );
}
