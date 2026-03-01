import { NavLink } from 'react-router-dom';
import type { ReactNode } from 'react';
import type { MonthSummaryDto } from '../api/types';
import { MonthSelector } from './MonthSelector';
import { NewMonthButton } from './NewMonthButton';

interface LayoutProps {
  months: MonthSummaryDto[];
  selectedMonthId: string | null;
  onSelectMonth: (monthId: string) => void;
  onCreateMonth: () => void;
  creatingMonth: boolean;
  children: ReactNode;
}

export function Layout({
  months,
  selectedMonthId,
  onSelectMonth,
  onCreateMonth,
  creatingMonth,
  children
}: LayoutProps) {
  return (
    <div className="app-shell">
      <header className="topbar">
        <div>
          <h1>Controle Mensal de Custos</h1>
          <p>Orçamento, lançamentos, metas e histórico.</p>
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
