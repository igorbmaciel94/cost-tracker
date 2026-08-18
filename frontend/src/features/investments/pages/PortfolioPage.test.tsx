import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { PrivacyProvider } from '../../../contexts/PrivacyContext';
import { PortfolioPage } from './PortfolioPage';

vi.mock('../components/AllocationDonut', () => ({
  AllocationDonut: ({ centerValue, centerLabel }: { centerValue?: ReactNode; centerLabel?: string }) => (
    <div data-testid="allocation-donut">{centerValue}{centerLabel}</div>
  )
}));

beforeEach(() => {
  const values = new Map<string, string>();
  vi.stubGlobal('localStorage', {
    getItem: vi.fn((key: string) => values.get(key) ?? null),
    setItem: vi.fn((key: string, value: string) => values.set(key, value)),
    removeItem: vi.fn((key: string) => values.delete(key)),
    clear: vi.fn(() => values.clear())
  });
});

afterEach(() => {
  vi.unstubAllGlobals();
});

function apiResponse(value: unknown) {
  return Promise.resolve(new Response(JSON.stringify(value), {
    status: 200,
    headers: { 'Content-Type': 'application/json' }
  }));
}

function renderPage(fetchMock: ReturnType<typeof vi.fn>) {
  vi.stubGlobal('fetch', fetchMock);
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <PrivacyProvider><PortfolioPage /></PrivacyProvider>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('PortfolioPage', () => {
  it('does not render the market refresh control inside the portfolio', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = String(input);
      if (url.endsWith('/investments/portfolio/valuation')) {
        return apiResponse({
          id: 'portfolio-1',
          isConfigured: true,
          summary: { totalValueEur: 1250, freshness: 'FRESH', isPartial: false },
          positions: [],
          allocationTargets: []
        });
      }
      if (url.endsWith('/investments/dividends/cash')) return apiResponse({ totalEur: 0, isPartial: false, balances: [] });
      return apiResponse({ freshness: 'FRESH', message: 'Dados atualizados.' });
    });
    renderPage(fetchMock);

    await screen.findByTestId('allocation-donut');
    expect(screen.queryByRole('button', { name: 'Atualizar cotações agora' })).not.toBeInTheDocument();
    expect(screen.queryByText('Atualização de cotações')).not.toBeInTheDocument();
    expect(screen.queryByText('Cotações desatualizadas')).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Caixa de dividendos' })).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Câmbio utilizado' })).not.toBeInTheDocument();
  });

  it('hides the portfolio value in the donut when privacy mode is enabled', async () => {
    localStorage.setItem('privacy-mode', '1');
    renderPage(vi.fn((input: RequestInfo | URL) => {
      const url = String(input);
      if (url.endsWith('/investments/portfolio/valuation')) {
        return apiResponse({
          id: 'portfolio-1',
          isConfigured: true,
          summary: { totalValueEur: 1250, freshness: 'FRESH', isPartial: false },
          positions: [],
          allocationTargets: []
        });
      }
      if (url.endsWith('/investments/dividends/cash')) return apiResponse({ totalEur: 0, isPartial: false, balances: [] });
      return apiResponse({ freshness: 'FRESH' });
    }));

    await screen.findByTestId('allocation-donut');
    expect(within(screen.getByTestId('allocation-donut')).getByText('***')).toBeInTheDocument();
    expect(screen.queryByText('Patrimônio')).not.toBeInTheDocument();
  });
});
