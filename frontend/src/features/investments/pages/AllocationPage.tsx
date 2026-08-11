import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { InvestmentApiError, investmentErrorMessage, investmentsApi } from '../api';
import { AllocationEditor } from '../components/AllocationEditor';
import { StatePanel } from '../components/StatePanel';
import { ASSET_CLASSES } from '../constants';
import { investmentQueryKeys } from '../queryKeys';
import type { AssetClass } from '../types';
import { basisPointsToWeight } from '../utils';

export function AllocationPage() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const [saveError, setSaveError] = useState<string | null>(null);
  const portfolioQuery = useQuery({
    queryKey: investmentQueryKeys.portfolio(),
    queryFn: investmentsApi.getPortfolio,
    retry: (failureCount, error) => !(error instanceof InvestmentApiError && error.status === 404) && failureCount < 2
  });

  const isMissing = portfolioQuery.error instanceof InvestmentApiError && portfolioQuery.error.status === 404;
  const isOnboarding = isMissing || portfolioQuery.data?.configured === false;
  const allocationMutation = useMutation({
    mutationFn: investmentsApi.updateAllocation,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: investmentQueryKeys.all });
      navigate('/investimentos');
    }
  });

  if (portfolioQuery.isLoading) {
    return <StatePanel title="A carregar a alocação…"><p>Estamos a consultar as metas da carteira.</p></StatePanel>;
  }

  if (portfolioQuery.isError && !isMissing) {
    return (
      <StatePanel
        title="Não foi possível carregar a alocação"
        tone="danger"
        action={<button type="button" onClick={() => void portfolioQuery.refetch()}>Tentar novamente</button>}
      >
        <p>{investmentErrorMessage(portfolioQuery.error)}</p>
      </StatePanel>
    );
  }

  const portfolio = portfolioQuery.data;
  const currentValues = Object.fromEntries(
    ASSET_CLASSES.map((assetClass) => [
      assetClass,
      portfolio?.targets.find((target) => target.assetClass === assetClass)?.currentWeight ?? 0
    ])
  ) as Record<AssetClass, number>;

  return (
    <div className="investment-page-stack">
      {isOnboarding && (
        <StatePanel title="Configure a sua carteira" tone="warning">
          <p>Antes de cadastrar ativos, defina quanto cada uma das quatro classes deve representar. Nenhum percentual é escolhido por você automaticamente.</p>
        </StatePanel>
      )}

      <section className="investment-panel">
        <AllocationEditor
          targets={portfolio?.targets ?? []}
          currentValues={portfolio?.configured ? currentValues : undefined}
          disabled={allocationMutation.isPending}
          submitLabel={isOnboarding ? 'Criar carteira' : 'Guardar novas metas'}
          onSubmit={async (basisPoints) => {
            setSaveError(null);
            try {
              await allocationMutation.mutateAsync({
                expectedVersion: portfolio?.version,
                items: ASSET_CLASSES.map((assetClass) => ({
                  assetClass,
                  weight: basisPointsToWeight(basisPoints[assetClass])
                }))
              });
            } catch (error) {
              setSaveError(investmentErrorMessage(error, 'Não foi possível guardar as metas.'));
            }
          }}
        />
        {saveError && <p className="investment-alert" data-tone="danger" role="alert">{saveError}</p>}
      </section>

      {!isOnboarding && <p className="investment-page-footnote"><Link to="/investimentos">Voltar à carteira</Link>. Alterar as metas expira previews de aporte ainda abertos.</p>}
    </div>
  );
}
