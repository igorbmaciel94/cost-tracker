import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import type { InstrumentPositionDto } from '../types';
import { PortfolioList } from './PortfolioList';

const position: InstrumentPositionDto = {
  instrumentId: 'barc-id',
  name: 'Barclays',
  ticker: 'BARC',
  mic: 'XLON',
  assetClass: 'STOCKS',
  kind: 'STOCK',
  valuationMode: 'MARKET_QUOTE',
  nativeCurrency: 'GBP',
  allocationScore: 10,
  quantity: 100,
  nativeValue: 503.6,
  valueEur: 580,
  portfolioWeight: 0.25,
  freshness: 'FRESH',
  marketData: {
    asOf: '2026-08-18',
    source: 'ALPHA_VANTAGE',
    freshness: 'FRESH',
    price: 5.036,
    currency: 'GBP',
    priceKind: 'EOD_CLOSE',
    providerSymbol: 'BARC.LON'
  }
};

describe('PortfolioList', () => {
  it('keeps calculation and source details out of the portfolio overview', () => {
    render(
      <MemoryRouter>
        <PortfolioList positions={[position]} selectedClass="ALL" onSelectClass={vi.fn()} />
      </MemoryRouter>
    );

    expect(screen.queryByText('Ver cálculo e fontes')).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Detalhes' })).toHaveAttribute('href', '/investimentos/ativos/barc-id');
    expect(screen.getByRole('link', { name: 'Ver detalhes' })).toHaveAttribute('href', '/investimentos/ativos/barc-id');
  });

  it('groups positions by the configured asset class order', () => {
    const positions: InstrumentPositionDto[] = [
      { ...position, instrumentId: 'br-fixed-id', ticker: 'CDB-BR', assetClass: 'BRAZIL_FIXED_INCOME' },
      { ...position, instrumentId: 'stock-a-id', ticker: 'STOCK-A', assetClass: 'STOCKS' },
      { ...position, instrumentId: 'intl-fixed-id', ticker: 'BOND-EUR', assetClass: 'INTERNATIONAL_FIXED_INCOME' },
      { ...position, instrumentId: 'reit-id', ticker: 'REIT-A', assetClass: 'REITS' },
      { ...position, instrumentId: 'stock-b-id', ticker: 'STOCK-B', assetClass: 'STOCKS' }
    ];

    render(
      <MemoryRouter>
        <PortfolioList positions={positions} selectedClass="ALL" onSelectClass={vi.fn()} />
      </MemoryRouter>
    );

    const rows = within(screen.getByRole('table')).getAllByRole('row').slice(1);
    expect(rows.map((row) => within(row).getAllByRole('cell')[0].textContent)).toEqual([
      'STOCK-ABarclaysXLON',
      'STOCK-BBarclaysXLON',
      'REIT-ABarclaysXLON',
      'BOND-EURBarclaysXLON',
      'CDB-BRBarclaysXLON'
    ]);
  });
});
