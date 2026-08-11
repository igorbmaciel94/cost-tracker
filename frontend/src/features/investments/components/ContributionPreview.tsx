import { ASSET_CLASS_META, INVESTABLE_ASSET_CLASSES } from '../constants';
import type { ContributionPlanDto, InvestableAssetClass } from '../types';
import { formatDateIsoToPt, formatPercent } from '../../../utils/format';
import { FreshnessBadge } from './FreshnessBadge';
import { InvestmentMoney } from './InvestmentMoney';
import { PrivacyMask } from '../../../contexts/PrivacyContext';

export function ContributionPreview({ plan }: { plan: ContributionPlanDto }) {
  const byClass = Object.fromEntries(
    INVESTABLE_ASSET_CLASSES.map((assetClass) => [
      assetClass,
      plan.classRecommendations.find((line) => line.assetClass === assetClass)?.recommendedAmountEur
        ?? plan.lines.filter((line) => line.assetClass === assetClass).reduce((sum, line) => sum + line.recommendedAmountEur, 0)
    ])
  ) as Record<InvestableAssetClass, number>;

  return (
    <div className="contribution-preview">
      <section className="contribution-summary" aria-label="Resumo do preview">
        <article><span>Aporte disponível</span><strong><InvestmentMoney value={plan.contributionAmountEur} /></strong></article>
        <article><span>Total sugerido</span><strong><InvestmentMoney value={plan.totalSuggestedEur} /></strong></article>
        <article><span>Residual</span><strong><InvestmentMoney value={plan.residualAmountEur} /></strong></article>
        <article><span>Validade</span><strong>{new Date(plan.expiresAt).toLocaleString('pt-PT', { dateStyle: 'short', timeStyle: 'short' })}</strong><small>Estratégia {plan.strategyVersion}</small></article>
      </section>

      <section className="investment-panel contribution-macro">
        <header className="investment-panel-header"><div><h2>Distribuição entre classes</h2><p>O cálculo usa dinheiro novo e não sugere vendas.</p></div></header>
        <div className="contribution-class-grid">
          {INVESTABLE_ASSET_CLASSES.map((assetClass) => (
            <article key={assetClass} style={{ '--asset-color': ASSET_CLASS_META[assetClass].color } as React.CSSProperties}>
              <span>{ASSET_CLASS_META[assetClass].label}</span>
              <strong><InvestmentMoney value={byClass[assetClass]} /></strong>
              <small>{formatPercent(plan.contributionAmountEur > 0 ? byClass[assetClass] / plan.contributionAmountEur : 0)} do aporte</small>
            </article>
          ))}
        </div>
      </section>

      <section className="investment-panel">
        <header className="investment-panel-header"><div><h2>Sugestões detalhadas</h2><p>Frações já respeitam o passo mínimo. Preço nominal menor não significa ativo mais barato.</p></div></header>
        <div className="contribution-line-list">
          {plan.lines.map((line) => (
            <article key={line.id} className="contribution-line">
              <header>
                <div><span className="asset-class-label">{ASSET_CLASS_META[line.assetClass].label}</span><h3>{line.ticker || line.instrumentName || 'Escolher destino na confirmação'}</h3></div>
                <FreshnessBadge status={line.freshness} />
              </header>
              <dl>
                <div><dt>Sugestão</dt><dd><InvestmentMoney value={line.recommendedAmountEur} /></dd></div>
                <div><dt>Moeda nativa</dt><dd><InvestmentMoney value={line.recommendedNativeAmount} currency={line.nativeCurrency ?? 'EUR'} /></dd></div>
                <div><dt>Quantidade</dt><dd><PrivacyMask value={line.suggestedQuantity == null ? 'Definir' : String(line.suggestedQuantity)} /></dd></div>
                <div><dt>Preço usado</dt><dd><InvestmentMoney value={line.unitPrice} currency={line.nativeCurrency ?? 'EUR'} /></dd></div>
                <div><dt>Meta da classe</dt><dd>{formatPercent(line.targetWeight)}</dd></div>
                <div><dt>Nota</dt><dd>{line.allocationScore ?? '—'}</dd></div>
              </dl>
              <p>{line.explanation}</p>
              <small className="snapshot-note">
                {line.quoteAsOf ? `Cotação: ${formatDateIsoToPt(line.quoteAsOf)}` : 'Sem cotação de mercado'}
                {line.fxAsOf ? ` · câmbio: ${formatDateIsoToPt(line.fxAsOf)}` : ''}
              </small>
            </article>
          ))}
          {plan.lines.length === 0 && <p className="investment-empty-copy">Nenhum destino elegível. Todo o valor permanece como residual.</p>}
        </div>
      </section>
    </div>
  );
}
