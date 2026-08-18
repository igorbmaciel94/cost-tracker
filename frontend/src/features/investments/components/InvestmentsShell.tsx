import { useMutation, useQueryClient } from '@tanstack/react-query';
import { NavLink } from 'react-router-dom';
import type { ReactNode } from 'react';
import { investmentErrorMessage, investmentsApi } from '../api';
import { investmentQueryKeys } from '../queryKeys';

export function InvestmentsShell({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const refreshMutation = useMutation({
    mutationFn: investmentsApi.refreshMarketData,
    onSuccess: async (status) => {
      await queryClient.invalidateQueries({ queryKey: investmentQueryKeys.all });
      queryClient.setQueryData(investmentQueryKeys.marketDataStatus(), status);
    }
  });

  return (
    <div className="investments-shell">
      <header className="investments-heading">
        <div>
          <span className="investments-eyebrow">Carteira longitudinal · EUR</span>
          <h1>Investimentos</h1>
          <p>Acompanhe posições, metas e aportes sem misturar a carteira com o orçamento mensal.</p>
        </div>
        <div className="investments-heading-actions">
          <div className="investments-heading-action-row">
            <button
              className="investment-secondary-link"
              type="button"
              disabled={refreshMutation.isPending}
              onClick={() => refreshMutation.mutate()}
            >
              {refreshMutation.isPending ? 'A atualizar…' : 'Atualizar cotações agora'}
            </button>
            <NavLink className="investment-primary-link" to="/investimentos/aporte">Planejar aporte</NavLink>
          </div>
          {refreshMutation.isError && (
            <small className="investments-heading-feedback" data-tone="danger" role="alert">
              {investmentErrorMessage(refreshMutation.error, 'Não foi possível atualizar as cotações.')}
            </small>
          )}
          {refreshMutation.isSuccess && (
            <small className="investments-heading-feedback" data-tone={refreshMutation.data.freshness === 'FRESH' ? undefined : 'warning'} role="status">
              {refreshMutation.data.freshness === 'FRESH' ? 'Cotações atualizadas.' : refreshMutation.data.message || 'Atualização executada; alguns dados continuam pendentes.'}
            </small>
          )}
        </div>
      </header>

      <nav className="investments-tabs" aria-label="Navegação de investimentos">
        <NavLink end to="/investimentos">Carteira</NavLink>
        <NavLink to="/investimentos/alocacao">Alocação</NavLink>
        <NavLink to="/investimentos/aporte">Novo aporte</NavLink>
        <NavLink to="/investimentos/ativos/novo">Cadastrar ativo</NavLink>
        <NavLink to="/investimentos/dividendos">Caixa de dividendos</NavLink>
        <NavLink to="/investimentos/cambio">Câmbio utilizado</NavLink>
      </nav>

      {children}
    </div>
  );
}
