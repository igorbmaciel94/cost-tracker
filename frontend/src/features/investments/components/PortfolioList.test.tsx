import { render, screen } from '@testing-library/react';
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
});
