import { NavLink } from 'react-router-dom';
import type { ReactNode } from 'react';
import type { MonthSummaryDto } from '../api/types';
import { MonthSelector } from './MonthSelector';
import { NewMonthButton } from './NewMonthButton';
import { formatCurrency, formatPercent } from '../utils/format';
import { usePrivacy, PrivacyMask } from '../contexts/PrivacyContext';

interface LayoutProps {
  months: MonthSummaryDto[];
  selectedMonthId: string | null;
  selectedMonth: MonthSummaryDto | null;
  onSelectMonth: (monthId: string) => void;
  onCreateMonth: () => void;
  creatingMonth: boolean;
  username: string | null;
  onLogout: () => Promise<void>;
  loggingOut: boolean;
  children: ReactNode;
}

export function Layout({
  months,
  selectedMonthId,
  selectedMonth,
  onSelectMonth,
  onCreateMonth,
  creatingMonth,
  username,
  onLogout,
  loggingOut,
  children
}: LayoutProps) {
  const { hidden, toggle } = usePrivacy();
  const plannedRatio = selectedMonth && selectedMonth.salary > 0
    ? selectedMonth.plannedTotal / selectedMonth.salary
    : 0;
  const spentRatio = selectedMonth && selectedMonth.salary > 0
    ? selectedMonth.spentTotal / selectedMonth.salary
    : 0;
  const monthBalance = selectedMonth ? selectedMonth.salary - selectedMonth.spentTotal : 0;
  const monthStatusLabel = selectedMonth?.status === 'OPEN' ? 'Mes aberto' : 'Mes fechado';
  const showTopKpis = Boolean(selectedMonth);

  return (
    <div className="app-shell">
      <header className="topbar">
        <div className="topbar-main">
          <div className="topbar-title-row">
            <div>
              <h1>Painel Financeiro</h1>
              <p className="topbar-subtitle">Controle de caixa mensal</p>
            </div>
            <button
              type="button"
              className="privacy-toggle"
              onClick={toggle}
              title={hidden ? 'Mostrar valores' : 'Ocultar valores'}
              aria-label={hidden ? 'Mostrar valores financeiros' : 'Ocultar valores financeiros'}
            >
              {hidden ? (
                <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94" />
                  <path d="M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19" />
                  <line x1="1" y1="1" x2="23" y2="23" />
                </svg>
              ) : (
                <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                  <circle cx="12" cy="12" r="3" />
                </svg>
              )}
            </button>
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
              <span className="meta-pill">{selectedMonth.referenceMonth}</span>
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
          <button
            type="button"
            className="button-secondary"
            onClick={() => {
              void onLogout();
            }}
            disabled={loggingOut}
          >
            {loggingOut ? 'Saindo...' : `Sair${username ? ` (${username})` : ''}`}
          </button>
        </div>
      </header>

      {showTopKpis && selectedMonth && (
        <section className="month-kpis">
          <article className="kpi-card">
            <p className="kpi-label">Salario base</p>
            <p className="kpi-value"><PrivacyMask value={formatCurrency(selectedMonth.salary)} /></p>
            <p className="kpi-note">Base do ciclo atual.</p>
          </article>

          <article className="kpi-card">
            <p className="kpi-label">Comprometido</p>
            <p className="kpi-value"><PrivacyMask value={formatCurrency(selectedMonth.plannedTotal)} /></p>
            <p className="kpi-note">{formatPercent(plannedRatio)} do salario.</p>
          </article>

          <article className="kpi-card">
            <p className="kpi-label">Executado</p>
            <p className="kpi-value"><PrivacyMask value={formatCurrency(selectedMonth.spentTotal)} /></p>
            <p className="kpi-note">{formatPercent(spentRatio)} utilizado.</p>
          </article>

          <article className={`kpi-card ${monthBalance < 0 ? 'kpi-card-danger' : 'kpi-card-positive'}`}>
            <p className="kpi-label">Saldo do mes</p>
            <p className="kpi-value"><PrivacyMask value={formatCurrency(monthBalance)} /></p>
            <p className="kpi-note">
              {monthBalance >= 0 ? 'No azul.' : 'No vermelho.'}
            </p>
          </article>
        </section>
      )}

      <nav className="main-nav">
        <NavLink to="/">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="14" y="14" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/></svg>
          <span>Dashboard</span>
        </NavLink>
        <NavLink to="/orcamento">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z"/><path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z"/></svg>
          <span>Orcamento</span>
        </NavLink>
        <NavLink to="/lancamentos">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><line x1="12" y1="1" x2="12" y2="23"/><path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/></svg>
          <span>Lancamentos</span>
        </NavLink>
        <NavLink to="/metas">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="10"/><circle cx="12" cy="12" r="6"/><circle cx="12" cy="12" r="2"/></svg>
          <span>Metas</span>
        </NavLink>
        <NavLink to="/historico">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>
          <span>Historico</span>
        </NavLink>
      </nav>

      <main>{children}</main>
    </div>
  );
}
