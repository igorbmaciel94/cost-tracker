import { NavLink } from 'react-router-dom';
import type { ReactNode } from 'react';
import type { MonthSummaryDto } from '../api/types';
import { MonthSelector } from './MonthSelector';
import { NewMonthButton } from './NewMonthButton';
import { formatCurrency, formatPercent } from '../utils/format';
import { usePrivacy, PrivacyMask } from '../contexts/PrivacyContext';
import { useTheme } from '../contexts/ThemeContext';

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

function IconDashboard() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/>
      <rect x="14" y="14" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/>
    </svg>
  );
}

function IconBudget() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z"/><path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z"/>
    </svg>
  );
}

function IconEntries() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <line x1="12" y1="1" x2="12" y2="23"/><path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/>
    </svg>
  );
}

function IconGoals() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10"/><circle cx="12" cy="12" r="6"/><circle cx="12" cy="12" r="2"/>
    </svg>
  );
}

function IconHistory() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/>
    </svg>
  );
}

function IconEye() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/>
    </svg>
  );
}

function IconEyeOff() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94"/>
      <path d="M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19"/>
      <line x1="1" y1="1" x2="23" y2="23"/>
    </svg>
  );
}

function IconMoon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/>
    </svg>
  );
}

function IconSun() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="5"/>
      <line x1="12" y1="1" x2="12" y2="3"/><line x1="12" y1="21" x2="12" y2="23"/>
      <line x1="4.22" y1="4.22" x2="5.64" y2="5.64"/><line x1="18.36" y1="18.36" x2="19.78" y2="19.78"/>
      <line x1="1" y1="12" x2="3" y2="12"/><line x1="21" y1="12" x2="23" y2="12"/>
      <line x1="4.22" y1="19.78" x2="5.64" y2="18.36"/><line x1="18.36" y1="5.64" x2="19.78" y2="4.22"/>
    </svg>
  );
}

function IconLogout() {
  return (
    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/>
      <polyline points="16 17 21 12 16 7"/>
      <line x1="21" y1="12" x2="9" y2="12"/>
    </svg>
  );
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
  const { theme, toggleTheme } = useTheme();

  const plannedRatio = selectedMonth && selectedMonth.salary > 0
    ? selectedMonth.plannedTotal / selectedMonth.salary
    : 0;
  const spentRatio = selectedMonth && selectedMonth.salary > 0
    ? selectedMonth.spentTotal / selectedMonth.salary
    : 0;
  const monthBalance = selectedMonth ? selectedMonth.salary - selectedMonth.spentTotal : 0;

  return (
    <div className="app-shell">
      <header className="topbar">
        <div className="topbar-main">
          <div className="topbar-brand">
            <div className="topbar-brand-icon">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <line x1="12" y1="1" x2="12" y2="23"/><path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/>
              </svg>
            </div>
            <span className="topbar-brand-name">Painel Financeiro</span>
          </div>

          {selectedMonth && (
            <>
              <div className="topbar-divider" />
              <div className="topbar-meta">
                <span className={`status-badge ${selectedMonth.status === 'OPEN' ? 'status-badge-open' : 'status-badge-closed'}`}>
                  {selectedMonth.status === 'OPEN' ? 'Aberto' : 'Fechado'}
                </span>
                <span className="meta-pill">{selectedMonth.referenceMonth}</span>
              </div>
            </>
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
            className="icon-btn"
            onClick={toggle}
            title={hidden ? 'Mostrar valores' : 'Ocultar valores'}
            aria-label={hidden ? 'Mostrar valores financeiros' : 'Ocultar valores financeiros'}
          >
            {hidden ? <IconEyeOff /> : <IconEye />}
          </button>
          <button
            type="button"
            className="icon-btn"
            onClick={toggleTheme}
            title={theme === 'dark' ? 'Modo claro' : 'Modo escuro'}
            aria-label={theme === 'dark' ? 'Ativar modo claro' : 'Ativar modo escuro'}
          >
            {theme === 'dark' ? <IconSun /> : <IconMoon />}
          </button>
          <button
            type="button"
            className="icon-btn"
            onClick={() => { void onLogout(); }}
            disabled={loggingOut}
            title={`Sair${username ? ` (${username})` : ''}`}
            aria-label="Sair da conta"
          >
            <IconLogout />
          </button>
        </div>
      </header>

      {selectedMonth && (
        <section className="month-kpis">
          <article className="kpi-card">
            <p className="kpi-label">Salário base</p>
            <p className="kpi-value"><PrivacyMask value={formatCurrency(selectedMonth.salary)} /></p>
            <p className="kpi-note">Base do ciclo atual</p>
          </article>

          <article className="kpi-card">
            <p className="kpi-label">Comprometido</p>
            <p className="kpi-value"><PrivacyMask value={formatCurrency(selectedMonth.plannedTotal)} /></p>
            <p className="kpi-note">{formatPercent(plannedRatio)} do salário</p>
          </article>

          <article className="kpi-card">
            <p className="kpi-label">Executado</p>
            <p className="kpi-value"><PrivacyMask value={formatCurrency(selectedMonth.spentTotal)} /></p>
            <p className="kpi-note">{formatPercent(spentRatio)} utilizado</p>
          </article>

          <article className={`kpi-card ${monthBalance < 0 ? 'kpi-card-danger' : 'kpi-card-positive'}`}>
            <p className="kpi-label">Saldo do mês</p>
            <p className="kpi-value"><PrivacyMask value={formatCurrency(monthBalance)} /></p>
            <p className="kpi-note">{monthBalance >= 0 ? 'No azul' : 'No vermelho'}</p>
          </article>
        </section>
      )}

      <nav className="main-nav">
        <NavLink to="/"><IconDashboard /><span>Dashboard</span></NavLink>
        <NavLink to="/orcamento"><IconBudget /><span>Orçamento</span></NavLink>
        <NavLink to="/lancamentos"><IconEntries /><span>Lançamentos</span></NavLink>
        <NavLink to="/metas"><IconGoals /><span>Metas</span></NavLink>
        <NavLink to="/historico"><IconHistory /><span>Histórico</span></NavLink>
      </nav>

      <main>{children}</main>
    </div>
  );
}
