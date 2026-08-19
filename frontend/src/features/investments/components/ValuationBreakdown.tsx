import { PrivacyMask } from '../../../contexts/PrivacyContext';
import { formatCurrency, formatDateIsoToPt } from '../../../utils/format';
import type { InstrumentPositionDto } from '../types';
import { InvestmentMoney } from './InvestmentMoney';

function sourceLabel(source?: string | null) {
  switch (source) {
    case 'TWELVE_DATA': return 'Twelve Data';
    case 'MARKETSTACK': return 'Marketstack';
    case 'ALPHA_VANTAGE': return 'Alpha Vantage';
    case 'EODHD': return 'EODHD';
    case 'YAHOO_TEST': return 'Yahoo (fallback)';
    case 'ECB': return 'Banco Central Europeu (ECB)';
    case 'BCB_PTAX': return 'Banco Central do Brasil (PTAX)';
    case 'MANUAL': return 'Informação manual';
    case 'IDENTITY': return 'Conversão direta';
    default: return source || 'Fonte indisponível';
  }
}

function referenceDate(value?: string | null) {
  return value ? formatDateIsoToPt(value) : 'data indisponível';
}

export function ValuationBreakdown({ position }: { position: InstrumentPositionDto }) {
  const quote = position.marketData;
  const fx = position.fxData;
  const quantity = position.quantity;
  const price = quote?.price ?? position.currentPrice;
  const nativeCalculation = quantity != null && price != null && position.nativeValue != null
    ? `${quantity.toLocaleString('pt-PT')} × ${formatCurrency(price, position.nativeCurrency)} = ${formatCurrency(position.nativeValue, position.nativeCurrency)}`
    : null;
  const fxCalculation = position.nativeValue != null && position.valueEur != null && fx && fx.rate > 0 && position.nativeCurrency !== 'EUR'
    ? `${formatCurrency(position.nativeValue, position.nativeCurrency)} ÷ ${fx.rate.toLocaleString('pt-PT', { maximumFractionDigits: 8 })} = ${formatCurrency(position.valueEur, 'EUR')}`
    : null;

  return (
    <dl className="valuation-breakdown">
      {position.valuationMode === 'MARKET_QUOTE' && (
        <div>
          <dt>Cotação utilizada</dt>
          <dd>
            <InvestmentMoney value={price} currency={position.nativeCurrency} />
            <small>{sourceLabel(quote?.source)} · {referenceDate(quote?.asOf)}{quote?.isFallback ? ' · fallback' : ''}</small>
          </dd>
        </div>
      )}
      {position.valuationMode === 'MANUAL' && (
        <div>
          <dt>Avaliação utilizada</dt>
          <dd><InvestmentMoney value={position.manualBalance} currency={position.nativeCurrency} /><small>Informação manual · {referenceDate(position.lastValuationAsOf)}</small></dd>
        </div>
      )}
      {nativeCalculation && <div><dt>Valor na moeda nativa</dt><dd><PrivacyMask value={nativeCalculation} /></dd></div>}
      {fx && (
        <div>
          <dt>Câmbio utilizado</dt>
          <dd>
            <span>1 {fx.baseCurrency} = {fx.rate.toLocaleString('pt-PT', { maximumFractionDigits: 8 })} {fx.quoteCurrency}</span>
            <small>{sourceLabel(fx.source)} · {referenceDate(fx.asOf)}{fx.isFallback ? ' · fallback' : ''}</small>
          </dd>
        </div>
      )}
      {fxCalculation && <div><dt>Conversão para EUR</dt><dd><PrivacyMask value={fxCalculation} /></dd></div>}
      <div><dt>Valor final</dt><dd><strong><InvestmentMoney value={position.valueEur} /></strong></dd></div>
    </dl>
  );
}
