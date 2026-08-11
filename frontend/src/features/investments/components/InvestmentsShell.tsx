import { NavLink } from 'react-router-dom';
import type { ReactNode } from 'react';

export function InvestmentsShell({ children }: { children: ReactNode }) {
  return (
    <div className="investments-shell">
      <header className="investments-heading">
        <div>
          <span className="investments-eyebrow">Carteira longitudinal · EUR</span>
          <h1>Investimentos</h1>
          <p>Acompanhe posições, metas e aportes sem misturar a carteira com o orçamento mensal.</p>
        </div>
        <NavLink className="investment-primary-link" to="/investimentos/aporte">Planejar aporte</NavLink>
      </header>

      <nav className="investments-tabs" aria-label="Navegação de investimentos">
        <NavLink end to="/investimentos">Carteira</NavLink>
        <NavLink to="/investimentos/alocacao">Alocação</NavLink>
        <NavLink to="/investimentos/aporte">Novo aporte</NavLink>
        <NavLink to="/investimentos/ativos/novo">Cadastrar ativo</NavLink>
      </nav>

      {children}
    </div>
  );
}
