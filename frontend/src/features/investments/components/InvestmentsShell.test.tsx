import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { InvestmentsShell } from './InvestmentsShell';

describe('InvestmentsShell', () => {
  it('links dividend cash and used exchange rates to dedicated pages', () => {
    render(
      <MemoryRouter initialEntries={['/investimentos/dividendos']}>
        <InvestmentsShell><p>Conteúdo da página</p></InvestmentsShell>
      </MemoryRouter>
    );

    expect(screen.getByRole('link', { name: 'Cadastrar ativo' })).toHaveAttribute('href', '/investimentos/ativos/novo');
    expect(screen.getByRole('link', { name: 'Caixa de dividendos' })).toHaveAttribute('href', '/investimentos/dividendos');
    expect(screen.getByRole('link', { name: 'Caixa de dividendos' })).toHaveClass('active');
    expect(screen.getByRole('link', { name: 'Câmbio utilizado' })).toHaveAttribute('href', '/investimentos/cambio');
  });
});
