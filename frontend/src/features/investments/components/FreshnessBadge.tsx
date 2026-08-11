import type { FreshnessStatus } from '../types';

const labels: Record<FreshnessStatus, string> = {
  FRESH: 'Atualizado',
  STALE: 'Desatualizado',
  BLOCKED: 'Expirado',
  MISSING: 'Sem cotação'
};

export function FreshnessBadge({ status }: { status: FreshnessStatus }) {
  return (
    <span className="freshness-badge" data-status={status}>
      <span aria-hidden="true" />
      {labels[status]}
    </span>
  );
}
