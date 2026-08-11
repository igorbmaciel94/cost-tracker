import type { PortfolioSummaryDto } from '../types';
import { formatDateIsoToPt } from '../../../utils/format';
import { FreshnessBadge } from './FreshnessBadge';
import { InvestmentMoney } from './InvestmentMoney';

export function PortfolioKpis({ summary }: { summary: PortfolioSummaryDto }) {
  const gainKnown = summary.gainLossEur !== null && summary.gainLossEur !== undefined;
  return (
    <section className="investment-kpis" aria-label="Resumo da carteira">
      <article>
        <span>{summary.isPartial ? 'Patrimônio conhecido' : 'Patrimônio estimado'}</span>
        <strong><InvestmentMoney value={summary.totalValueEur} /></strong>
        <small>{summary.isPartial ? 'Subtotal: existem posições sem cotação ou câmbio' : 'Consolidado na moeda base EUR'}</small>
      </article>
      <article>
        <span>Aportes líquidos conhecidos</span>
        <strong><InvestmentMoney value={summary.knownCostEur} unavailableLabel="Histórico parcial" /></strong>
        <small>Entradas menos saídas com histórico suficiente</small>
      </article>
      <article data-tone={gainKnown && (summary.gainLossEur ?? 0) < 0 ? 'negative' : gainKnown ? 'positive' : undefined}>
        <span>Variação vs. aportes</span>
        <strong><InvestmentMoney value={summary.gainLossEur} /></strong>
        <small>Patrimônio menos aportes líquidos conhecidos</small>
      </article>
      <article>
        <span>Dados usados</span>
        <FreshnessBadge status={summary.freshness} />
        <small>{summary.asOf ? `Mais antigo: ${formatDateIsoToPt(summary.asOf)}` : 'Sem data consolidada'}</small>
      </article>
    </section>
  );
}
