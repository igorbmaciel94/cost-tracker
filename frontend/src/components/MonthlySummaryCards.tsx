import type { DashboardDto } from '../api/types';
import { formatCurrency } from '../utils/format';

interface MonthlySummaryCardsProps {
  dashboard: DashboardDto;
}

export function MonthlySummaryCards({ dashboard }: MonthlySummaryCardsProps) {
  const currentBalance = dashboard.salary - dashboard.spentTotal;
  const plannedRatio = dashboard.salary > 0 ? dashboard.plannedTotal / dashboard.salary : 0;
  const spentRatio = dashboard.salary > 0 ? dashboard.spentTotal / dashboard.salary : 0;

  return (
    <section className="summary-grid">
      <article className="summary-card summary-card-income">
        <h3>Salário</h3>
        <p>{formatCurrency(dashboard.salary)}</p>
        <small>Base mensal de receita.</small>
      </article>
      <article className="summary-card">
        <h3>Total previsto</h3>
        <p>{formatCurrency(dashboard.plannedTotal)}</p>
        <small>{Math.round(plannedRatio * 100)}% da renda.</small>
      </article>
      <article className="summary-card">
        <h3>Total gasto</h3>
        <p>{formatCurrency(dashboard.spentTotal)}</p>
        <small>{Math.round(spentRatio * 100)}% executado.</small>
      </article>
      <article className={`summary-card ${currentBalance < 0 ? 'summary-card-negative' : 'summary-card-positive'}`}>
        <h3>Saldo atual</h3>
        <p>{formatCurrency(currentBalance)}</p>
        <small>{currentBalance >= 0 ? 'Resultado positivo no ciclo.' : 'Resultado negativo no ciclo.'}</small>
      </article>
      {dashboard.isOverPlanned && (
        <article className="summary-card warning">
          <h3>Alerta previsto</h3>
          <p>Previsto acima do salário.</p>
          <small>Revise categorias projetadas.</small>
        </article>
      )}
      {dashboard.isOverSpent && (
        <article className="summary-card warning">
          <h3>Alerta gasto</h3>
          <p>Gasto acima do salário.</p>
          <small>Reduza ritmo para fechar no azul.</small>
        </article>
      )}
    </section>
  );
}
