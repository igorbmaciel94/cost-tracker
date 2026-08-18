import { useQuery } from '@tanstack/react-query';
import { formatDateIsoToPt } from '../../../utils/format';
import { investmentsApi } from '../api';
import { investmentQueryKeys } from '../queryKeys';
import { InvestmentMoney } from './InvestmentMoney';

export function DividendCashCard() {
  const cashQuery = useQuery({
    queryKey: investmentQueryKeys.dividendCash(),
    queryFn: investmentsApi.getDividendCash
  });

  return (
    <section className="investment-panel dividend-cash-card">
      <header className="investment-panel-header">
        <div>
          <h2>Caixa de dividendos</h2>
          <p>Valores líquidos já pagos, separados das posições e agrupados por moeda.</p>
        </div>
        {cashQuery.data?.totalEur != null && (
          <div className="dividend-cash-total">
            <span>Equivalente atual</span>
            <strong><InvestmentMoney value={cashQuery.data.totalEur} /></strong>
          </div>
        )}
      </header>
      {cashQuery.isLoading && <p className="investment-empty-copy">A carregar o caixa…</p>}
      {cashQuery.isError && <p className="investment-alert" data-tone="danger">Não foi possível carregar o caixa de dividendos.</p>}
      {cashQuery.data?.balances.length === 0 && <p className="investment-empty-copy">Nenhum dividendo foi creditado ainda.</p>}
      {(cashQuery.data?.balances.length ?? 0) > 0 && (
        <div className="dividend-cash-balances">
          {cashQuery.data?.balances.map((balance) => (
            <article key={balance.currency}>
              <span>Saldo em {balance.currency}</span>
              <strong><InvestmentMoney value={balance.amount} currency={balance.currency} /></strong>
              <small>
                {balance.amountEur == null
                  ? 'Conversão para EUR indisponível'
                  : <>≈ <InvestmentMoney value={balance.amountEur} />{balance.fxData?.source ? ` · ${balance.fxData.source}` : ''}{balance.fxData?.asOf ? ` · câmbio de ${formatDateIsoToPt(balance.fxData.asOf)}` : ''}</>}
              </small>
              {balance.lastPaymentDate && <small>Último crédito: {formatDateIsoToPt(balance.lastPaymentDate)}</small>}
            </article>
          ))}
        </div>
      )}
      {cashQuery.data?.isPartial && <p className="investment-alert" data-tone="warning">Algum saldo ainda não tem câmbio disponível para calcular o total em EUR.</p>}
    </section>
  );
}
