import { NavLink } from 'react-router-dom';
import type { ReactNode } from 'react';
import type { MonthSummaryDto } from '../api/types';
import { MonthSelector } from './MonthSelector';
import { NewMonthButton } from './NewMonthButton';
import { formatCurrency, formatPercent } from '../utils/format';

interface LayoutProps {
  months: MonthSummaryDto[];
  selectedMonthId: string | null;
  selectedMonth: MonthSummaryDto | null;
  onSelectMonth: (monthId: string) => void;
  onCreateMonth: () => void;
  creatingMonth: boolean;
  children: ReactNode;
}

export function Layout({
  months,
  selectedMonthId,
  selectedMonth,
  onSelectMonth,
  onCreateMonth,
  creatingMonth,
  children
}: LayoutProps) {
  const plannedRatio = selectedMonth && selectedMonth.salary > 0
    ? selectedMonth.plannedTotal / selectedMonth.salary
    : 0;
  const spentRatio = selectedMonth && selectedMonth.salary > 0
    ? selectedMonth.spentTotal / selectedMonth.salary
    : 0;
  const monthBalance = selectedMonth ? selectedMonth.salary - selectedMonth.spentTotal : 0;
  const monthStatusLabel = selectedMonth?.status === 'OPEN' ? 'Mes aberto' : 'Mes fechado';

  return (
    <div className="app-shell">
      <header className="topbar">
        <div className="topbar-main">
          <div>
            <h1>Painel Financeiro Pessoal</h1>
            <p>Controle de caixa mensal com visao consolidada de orcamento e gastos.</p>
          </div>
          {selectedMonth && (
            <div className="topbar-meta">
              <span
                className={`status-badge ${
                  selectedMonth.status === 'OPEN' ? 'status-badge-open' : 'status-badge-closed'
                }`}
              >
                {monthStatusLabel}
              </span>
              <span className="meta-pill">Referencia {selectedMonth.referenceMonth}</span>
            </div>
          )}
        </div>
        <div className="topbar-actions">
          <MonthSelector
            months={months}
            selectedMonthId={selectedMonthId}
            onChange={onSelectMonth}
          />
          <NewMonthButton
            disabled={months.length === 0}
            loading={creatingMonth}
            onClick={onCreateMonth}
          />
        </div>
      </header>

      {selectedMonth && (
        <section className="month-kpis">
          <article className="kpi-card">
            <p className="kpi-label">Salario base</p>
            <p className="kpi-value">{formatCurrency(selectedMonth.salary)}</p>
            <p className="kpi-note">Base de comparacao para o ciclo atual.</p>
          </article>

          <article className="kpi-card">
            <p className="kpi-label">Comprometido (previsto)</p>
            <p className="kpi-value">{formatCurrency(selectedMonth.plannedTotal)}</p>
            <p className="kpi-note">{formatPercent(plannedRatio)} do salario planejado.</p>
          </article>

          <article className="kpi-card">
            <p className="kpi-label">Executado (gasto)</p>
            <p className="kpi-value">{formatCurrency(selectedMonth.spentTotal)}</p>
            <p className="kpi-note">{formatPercent(spentRatio)} do salario ja utilizado.</p>
          </article>

          <article className={`kpi-card ${monthBalance < 0 ? 'kpi-card-danger' : 'kpi-card-positive'}`}>
            <p className="kpi-label">Saldo do mes</p>
            <p className="kpi-value">{formatCurrency(monthBalance)}</p>
            <p className="kpi-note">
              {monthBalance >= 0 ? 'Fluxo no azul.' : 'Fluxo no vermelho.'}
            </p>
          </article>
        </section>
      )}

      <nav className="main-nav">
        <NavLink to="/">Dashboard</NavLink>
        <NavLink to="/orcamento">Orçamento</NavLink>
        <NavLink to="/lancamentos">Lançamentos</NavLink>
        <NavLink to="/metas">Metas</NavLink>
        <NavLink to="/historico">Histórico</NavLink>
      </nav>

      <main>{children}</main>
    </div>
  );
}
