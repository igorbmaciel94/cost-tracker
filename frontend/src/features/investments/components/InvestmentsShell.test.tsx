import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { InvestmentsShell } from './InvestmentsShell';

function renderShell() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/investimentos/dividendos']}>
        <InvestmentsShell><p>Conteúdo da página</p></InvestmentsShell>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('InvestmentsShell', () => {
  it('links dividend cash and used exchange rates to dedicated pages', () => {
    renderShell();

    expect(screen.getByRole('link', { name: 'Cadastrar ativo' })).toHaveAttribute('href', '/investimentos/ativos/novo');
    expect(screen.getByRole('link', { name: 'Caixa de dividendos' })).toHaveAttribute('href', '/investimentos/dividendos');
    expect(screen.getByRole('link', { name: 'Caixa de dividendos' })).toHaveClass('active');
    expect(screen.getByRole('link', { name: 'Câmbio utilizado' })).toHaveAttribute('href', '/investimentos/cambio');
  });

  it('refreshes market data from the heading action', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn((_input: RequestInfo | URL, init?: RequestInit) => {
      expect(init?.method).toBe('POST');
      return Promise.resolve(new Response(JSON.stringify({ freshness: 'FRESH', message: 'Atualizado.' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' }
      }));
    });
    vi.stubGlobal('fetch', fetchMock);
    renderShell();

    const refreshButton = screen.getByRole('button', { name: 'Atualizar cotações agora' });
    expect(refreshButton).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Planejar aporte' })).toBeInTheDocument();
    await user.click(refreshButton);

    expect(await screen.findByRole('status')).toHaveTextContent('Cotações atualizadas.');
    await waitFor(() => expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/investments/market-data/refresh'),
      expect.objectContaining({ method: 'POST' })
    ));
  });
});
