import type { DashboardDto } from '../api/types';
import { formatCurrency } from '../utils/format';

interface MonthlySummaryCardsProps {
  dashboard: DashboardDto;
}

export function MonthlySummaryCards({ dashboard }: MonthlySummaryCardsProps) {
  return (
    <section className="summary-grid">
      <article className="summary-card">
        <h3>Salário</h3>
        <p>{formatCurrency(dashboard.salary)}</p>
      </article>
      <article className="summary-card">
        <h3>Total previsto</h3>
        <p>{formatCurrency(dashboard.plannedTotal)}</p>
      </article>
      <article className="summary-card">
        <h3>Total gasto</h3>
        <p>{formatCurrency(dashboard.spentTotal)}</p>
      </article>
      <article className="summary-card">
        <h3>Saldo atual</h3>
        <p>{formatCurrency(dashboard.salary - dashboard.spentTotal)}</p>
      </article>
      {dashboard.isOverPlanned && (
        <article className="summary-card warning">
          <h3>Alerta previsto</h3>
          <p>Previsto acima do salário.</p>
        </article>
      )}
      {dashboard.isOverSpent && (
        <article className="summary-card warning">
          <h3>Alerta gasto</h3>
          <p>Gasto acima do salário.</p>
        </article>
      )}
    </section>
  );
}
