import { useQuery } from '@tanstack/react-query';
import { investmentErrorMessage, investmentsApi } from '../api';
import { FxRatesPanel } from '../components/FxRatesPanel';
import { StatePanel } from '../components/StatePanel';
import { investmentQueryKeys } from '../queryKeys';

export function FxRatesPage() {
  const portfolioQuery = useQuery({
    queryKey: investmentQueryKeys.portfolio(),
    queryFn: investmentsApi.getPortfolio,
    retry: false
  });

  if (portfolioQuery.isLoading) {
    return <StatePanel title="A carregar o câmbio…"><p>A reunir as taxas utilizadas nas posições da carteira.</p></StatePanel>;
  }

  if (portfolioQuery.isError) {
    return (
      <StatePanel title="Não foi possível carregar o câmbio" tone="danger" action={<button type="button" onClick={() => void portfolioQuery.refetch()}>Tentar novamente</button>}>
        <p>{investmentErrorMessage(portfolioQuery.error)}</p>
      </StatePanel>
    );
  }

  return (
    <div className="investment-page-stack">
      <FxRatesPanel positions={portfolioQuery.data?.positions ?? []} />
    </div>
  );
}
