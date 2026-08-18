import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { InvestmentApiError, investmentErrorMessage, investmentsApi } from '../api';
import { AllocationDonut } from '../components/AllocationDonut';
import { InvestmentMoney } from '../components/InvestmentMoney';
import { PortfolioKpis } from '../components/PortfolioKpis';
import { PortfolioList } from '../components/PortfolioList';
import { StatePanel } from '../components/StatePanel';
import { ASSET_CLASSES, ASSET_CLASS_META } from '../constants';
import { investmentQueryKeys } from '../queryKeys';
import type { AssetClass, InvestableAssetClass, PortfolioSummaryDto } from '../types';
import { worstFreshness } from '../utils';

export function PortfolioPage() {
  const queryClient = useQueryClient();
  const [selectedClass, setSelectedClass] = useState<InvestableAssetClass | 'ALL'>('ALL');
  const portfolioQuery = useQuery({
    queryKey: investmentQueryKeys.portfolio(),
    queryFn: investmentsApi.getPortfolio,
    retry: (failureCount, error) => !(error instanceof InvestmentApiError && error.status === 404) && failureCount < 2
  });
  const marketStatusQuery = useQuery({
    queryKey: investmentQueryKeys.marketDataStatus(),
    queryFn: investmentsApi.getMarketDataStatus,
    enabled: portfolioQuery.isSuccess && portfolioQuery.data.configured,
    retry: false
  });
  const refreshMutation = useMutation({
    mutationFn: investmentsApi.refreshMarketData,
    onSuccess: async (status) => {
      queryClient.setQueryData(investmentQueryKeys.marketDataStatus(), status);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: investmentQueryKeys.portfolio() }),
        queryClient.invalidateQueries({ queryKey: investmentQueryKeys.instruments() })
      ]);
    }
  });

  const needsOnboarding = (portfolioQuery.error instanceof InvestmentApiError && portfolioQuery.error.status === 404)
    || portfolioQuery.data?.configured === false;
  const portfolio = portfolioQuery.data;
  const positions = portfolio?.positions ?? [];
  const computedSummary = useMemo<PortfolioSummaryDto>(() => {
    if (portfolio?.summary) return portfolio.summary;
    const totalValueEur = positions.reduce((sum, position) => sum + (position.valueEur ?? 0), 0);
    const knownCosts = positions.filter((position) => position.knownCostEur !== null && position.knownCostEur !== undefined);
    const statuses = positions.map((position) => position.marketData?.freshness ?? 'FRESH');
    const dates = positions
      .map((position) => position.marketData?.asOf ?? position.lastValuationAsOf)
      .filter((value): value is string => Boolean(value))
      .sort();
    return {
      totalValueEur,
      knownCostEur: knownCosts.length > 0 ? knownCosts.reduce((sum, position) => sum + (position.knownCostEur ?? 0), 0) : null,
      gainLossEur: knownCosts.length > 0 ? knownCosts.reduce((sum, position) => sum + (position.gainLossEur ?? 0), 0) : null,
      freshness: statuses.length > 0 ? worstFreshness(statuses) : 'MISSING',
      asOf: dates[0] ?? null
    };
  }, [portfolio?.summary, positions]);

  if (portfolioQuery.isLoading) {
    return <StatePanel title="A carregar a carteira…"><p>A reunir posições, câmbio e últimos fechamentos disponíveis.</p></StatePanel>;
  }

  if (needsOnboarding) {
    return (
      <StatePanel title="A sua carteira ainda não está configurada" tone="warning" action={<Link className="investment-primary-link" to="/investimentos/alocacao">Definir alocação</Link>}>
        <p>Comece pelas metas das cinco categorias. Criptomoedas aparece apenas como meta percentual; as outras quatro aceitam posições e aportes.</p>
      </StatePanel>
    );
  }

  if (portfolioQuery.isError && !needsOnboarding) {
    return (
      <StatePanel title="Não foi possível carregar a carteira" tone="danger" action={<button type="button" onClick={() => void portfolioQuery.refetch()}>Tentar novamente</button>}>
        <p>{investmentErrorMessage(portfolioQuery.error)}</p>
      </StatePanel>
    );
  }

  const total = computedSummary.totalValueEur;
  const distribution = Object.fromEntries(
    ASSET_CLASSES.map((assetClass) => [
      assetClass,
      total > 0
        ? positions.filter((position) => position.assetClass === assetClass).reduce((sum, position) => sum + (position.valueEur ?? 0), 0) / total
        : portfolio?.targets.find((target) => target.assetClass === assetClass)?.currentWeight ?? 0
    ])
  ) as Record<AssetClass, number>;

  return (
    <div className="investment-page-stack">
      <section className="investment-panel market-refresh-panel">
        <div>
          <strong>Atualização de cotações</strong>
          <small>Executada automaticamente às 06:00. Você também pode buscar os valores mais recentes a qualquer momento.</small>
        </div>
        <button type="button" disabled={refreshMutation.isPending} onClick={() => refreshMutation.mutate()}>
          {refreshMutation.isPending ? 'A atualizar…' : 'Atualizar cotações agora'}
        </button>
        {refreshMutation.isError && <p className="investment-alert" data-tone="danger" role="alert">{investmentErrorMessage(refreshMutation.error, 'Não foi possível atualizar as cotações.')}</p>}
        {refreshMutation.isSuccess && (
          <p className="investment-alert" data-tone={refreshMutation.data.freshness === 'FRESH' ? undefined : 'warning'} role="status">
            {refreshMutation.data.freshness === 'FRESH' ? 'Cotações atualizadas.' : refreshMutation.data.message || 'Atualização executada; alguns dados continuam pendentes.'}
          </p>
        )}
      </section>
      {marketStatusQuery.data && marketStatusQuery.data.freshness !== 'FRESH' && (
        <StatePanel
          title={marketStatusQuery.data.freshness === 'STALE' ? 'Cotações desatualizadas' : 'Dados de mercado incompletos'}
          tone={marketStatusQuery.data.freshness === 'STALE' ? 'warning' : 'danger'}
        >
          <p>{marketStatusQuery.data.message || 'Os valores exibidos usam o último snapshot disponível. Um novo aporte pode ficar bloqueado até a atualização.'}</p>
          {(marketStatusQuery.data.failures?.length ?? 0) > 0 && (
            <details className="market-provider-failures">
              <summary>Detalhes da última atualização</summary>
              <ul>{marketStatusQuery.data.failures?.slice(0, 5).map((failure, index) => <li key={`${failure.provider}-${failure.subject}-${index}`}><strong>{failure.provider}</strong>: {failure.subject} — {failure.message}</li>)}</ul>
            </details>
          )}
        </StatePanel>
      )}
      <PortfolioKpis summary={computedSummary} />
      <div className="portfolio-overview-grid">
        <section className="investment-panel allocation-overview">
          <AllocationDonut
            values={distribution}
            centerLabel=""
            centerValue={<InvestmentMoney value={computedSummary.totalValueEur} />}
            title={computedSummary.isPartial ? 'Posições conhecidas por classe' : 'Carteira por classe'}
          />
        </section>
        <section className="investment-panel allocation-comparison">
          <header className="investment-panel-header">
            <div><h2>Atual × meta</h2><p>{computedSummary.isPartial ? 'Comparação suspensa enquanto faltarem cotações ou câmbio.' : 'O novo aporte tentará reduzir estes desvios sem recomendar vendas.'}</p></div>
            <Link to="/investimentos/alocacao">Editar metas</Link>
          </header>
          {computedSummary.isPartial ? (
            <p className="investment-alert" data-tone="warning">Os percentuais conhecidos não representam a carteira inteira. Atualize os dados antes de avaliar os desvios.</p>
          ) : <div className="allocation-comparison-list">
            {ASSET_CLASSES.map((assetClass) => {
              const target = portfolio?.targets.find((item) => item.assetClass === assetClass)?.weight ?? 0;
              const current = distribution[assetClass] ?? 0;
              return (
                <div key={assetClass}>
                  <span>{ASSET_CLASS_META[assetClass].label}</span>
                  <strong>{(current * 100).toFixed(2)}%</strong>
                  <small>Meta {(target * 100).toFixed(2)}% · desvio {((current - target) * 100).toFixed(2)} p.p.</small>
                  <div className="allocation-progress"><span style={{ width: `${Math.min(100, current * 100)}%` }} /></div>
                </div>
              );
            })}
          </div>}
        </section>
      </div>
      <PortfolioList positions={positions} selectedClass={selectedClass} onSelectClass={setSelectedClass} />
    </div>
  );
}
