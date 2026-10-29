import { statusText } from '../format';

export function StatusBadge({ status }: { status: string }) {
  return <span className={`badge badge-${status.toLowerCase()}`}>{statusText[status] ?? status}</span>;
}
