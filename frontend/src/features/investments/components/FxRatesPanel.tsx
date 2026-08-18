import { formatDateIsoToPt } from '../../../utils/format';
import type { InstrumentPositionDto } from '../types';

function sourceLabel(source?: string | null) {
  if (source === 'ECB') return 'ECB';
  if (source === 'BCB_PTAX') return 'BCB PTAX';
  if (source === 'IDENTITY') return 'Conversão direta';
  return source || 'Fonte indisponível';
}

export function FxRatesPanel({ positions }: { positions: InstrumentPositionDto[] }) {
  const rates = Array.from(new Map(
    positions
      .map((position) => position.fxData)
      .filter((rate) => rate && rate.quoteCurrency !== rate.baseCurrency && rate.rate > 0)
      .map((rate) => [`${rate!.baseCurrency}/${rate!.quoteCurrency}`, rate!] as const)
  ).values());

  if (rates.length === 0) return null;

  return (
    <section className="investment-panel fx-rates-panel">
      <header className="investment-panel-header">
        <div><h2>Câmbio utilizado</h2><p>Taxas efetivamente usadas para converter as posições para EUR.</p></div>
      </header>
      <div className="fx-rate-list">
        {rates.map((rate) => (
          <article key={`${rate.baseCurrency}/${rate.quoteCurrency}`}>
            <span>{rate.baseCurrency}/{rate.quoteCurrency}</span>
            <strong>1 {rate.baseCurrency} = {rate.rate.toLocaleString('pt-PT', { maximumFractionDigits: 8 })} {rate.quoteCurrency}</strong>
            <small>{sourceLabel(rate.source)} · {rate.asOf ? formatDateIsoToPt(rate.asOf) : 'data indisponível'}</small>
          </article>
        ))}
      </div>
    </section>
  );
}
