import { Link } from 'react-router-dom';
import { ASSET_CLASS_META, ASSET_CLASSES } from '../constants';
import type { AssetClass, InstrumentPositionDto } from '../types';
import { formatDateIsoToPt, formatPercent } from '../../../utils/format';
import { PrivacyMask } from '../../../contexts/PrivacyContext';
import { FreshnessBadge } from './FreshnessBadge';
import { InvestmentMoney } from './InvestmentMoney';

interface PortfolioListProps {
  positions: InstrumentPositionDto[];
  selectedClass: AssetClass | 'ALL';
  onSelectClass: (assetClass: AssetClass | 'ALL') => void;
}

function positionFreshness(position: InstrumentPositionDto) {
  if (position.freshness) return position.freshness;
  if (position.valuationMode === 'MANUAL') {
    const hasValuation = position.manualBalance !== null && position.manualBalance !== undefined;
    return hasValuation ? (position.marketData?.freshness ?? 'FRESH') : 'MISSING';
  }
  return position.marketData?.freshness ?? (position.currentPrice === null || position.currentPrice === undefined ? 'MISSING' : 'FRESH');
}

function oldestPositionDate(position: InstrumentPositionDto) {
  return [position.marketData?.asOf, position.lastValuationAsOf, position.fxData?.asOf]
    .filter((value): value is string => Boolean(value))
    .sort()[0];
}

function InstrumentName({ position }: { position: InstrumentPositionDto }) {
  return (
    <div className="instrument-name">
      <strong>{position.ticker || position.name}</strong>
      <span>{position.ticker ? position.name : ASSET_CLASS_META[position.assetClass].label}</span>
      {position.mic && <small>{position.mic}</small>}
    </div>
  );
}

export function PortfolioList({ positions, selectedClass, onSelectClass }: PortfolioListProps) {
  const filtered = selectedClass === 'ALL'
    ? positions
    : positions.filter((position) => position.assetClass === selectedClass);

  return (
    <section className="investment-panel portfolio-list-panel">
      <header className="investment-panel-header">
        <div>
          <h2>Ativos da carteira</h2>
          <p>{positions.length} {positions.length === 1 ? 'posição cadastrada' : 'posições cadastradas'}</p>
        </div>
        <Link className="investment-button-link" to="/investimentos/ativos/novo">+ Novo ativo</Link>
      </header>

      <div className="investment-filters" aria-label="Filtrar por classe">
        <button type="button" className="filter-chip" aria-pressed={selectedClass === 'ALL'} onClick={() => onSelectClass('ALL')}>Todos</button>
        {ASSET_CLASSES.map((assetClass) => (
          <button
            type="button"
            className="filter-chip"
            key={assetClass}
            aria-pressed={selectedClass === assetClass}
            onClick={() => onSelectClass(assetClass)}
          >
            <span className="allocation-dot" style={{ background: ASSET_CLASS_META[assetClass].color }} />
            {ASSET_CLASS_META[assetClass].shortLabel}
          </button>
        ))}
      </div>

      {filtered.length === 0 ? (
        <div className="investment-empty-inline">
          <p>Nenhum ativo neste filtro.</p>
          {positions.length === 0 && <Link to="/investimentos/ativos/novo">Cadastrar o primeiro ativo</Link>}
        </div>
      ) : (
        <>
          <div className="investment-table-wrap">
            <table className="investment-table">
              <caption>Posições atuais, valores e atualização dos dados</caption>
              <thead>
                <tr>
                  <th scope="col">Ativo</th>
                  <th scope="col">Classe</th>
                  <th scope="col">Quantidade / saldo</th>
                  <th scope="col">Valor nativo</th>
                  <th scope="col">Equivalente EUR</th>
                  <th scope="col">Peso</th>
                  <th scope="col">Dados</th>
                  <th scope="col"><span className="sr-only">Ações</span></th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((position) => (
                  <tr key={position.instrumentId}>
                    <td><InstrumentName position={position} /></td>
                    <td><span className="asset-class-pill" style={{ '--asset-color': ASSET_CLASS_META[position.assetClass].color } as React.CSSProperties}>{ASSET_CLASS_META[position.assetClass].shortLabel}</span></td>
                    <td>{position.valuationMode === 'MARKET_QUOTE' ? <PrivacyMask value={position.quantity == null ? '—' : String(position.quantity)} /> : <InvestmentMoney value={position.manualBalance} currency={position.nativeCurrency} />}</td>
                    <td><InvestmentMoney value={position.nativeValue} currency={position.nativeCurrency} /></td>
                    <td><strong><InvestmentMoney value={position.valueEur} /></strong></td>
                    <td>{position.portfolioWeight == null ? '—' : formatPercent(position.portfolioWeight)}</td>
                    <td>
                      <FreshnessBadge status={positionFreshness(position)} />
                      {oldestPositionDate(position) && <small className="as-of">{formatDateIsoToPt(oldestPositionDate(position) ?? '')}</small>}
                    </td>
                    <td><Link className="table-action-link" to={`/investimentos/ativos/${position.instrumentId}`}>Detalhes</Link></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="instrument-card-list">
            {filtered.map((position) => (
              <article className="instrument-card" key={position.instrumentId}>
                <header>
                  <InstrumentName position={position} />
                  <span className="asset-class-pill" style={{ '--asset-color': ASSET_CLASS_META[position.assetClass].color } as React.CSSProperties}>{ASSET_CLASS_META[position.assetClass].shortLabel}</span>
                </header>
                <dl>
                  <div><dt>Valor</dt><dd><InvestmentMoney value={position.valueEur} /></dd></div>
                  <div><dt>Na moeda nativa</dt><dd><InvestmentMoney value={position.nativeValue} currency={position.nativeCurrency} /></dd></div>
                  <div><dt>Peso</dt><dd>{position.portfolioWeight == null ? '—' : formatPercent(position.portfolioWeight)}</dd></div>
                  <div><dt>Dados</dt><dd><FreshnessBadge status={positionFreshness(position)} /></dd></div>
                </dl>
                <Link className="investment-secondary-link" to={`/investimentos/ativos/${position.instrumentId}`}>Ver detalhes</Link>
              </article>
            ))}
          </div>
        </>
      )}
    </section>
  );
}
