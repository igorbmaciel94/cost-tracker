import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { InstrumentForm } from './InstrumentForm';

describe('InstrumentForm', () => {
  it('switches from market identity to manual valuation for fixed income', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();
    render(<InstrumentForm onSubmit={onSubmit} />);

    expect(screen.getByLabelText('Classe')).not.toHaveTextContent('Criptomoedas');
    expect(screen.getByLabelText('Ticker')).toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText('Classe'), 'BRAZIL_FIXED_INCOME');

    expect(screen.queryByLabelText('Ticker')).not.toBeInTheDocument();
    expect(screen.getByLabelText('Saldo resgatável')).toBeInTheDocument();

    await user.type(screen.getByLabelText('Nome'), 'CDB liquidez diária');
    await user.selectOptions(screen.getByLabelText('Moeda nativa'), 'BRL');
    await user.type(screen.getByLabelText('Saldo resgatável'), '10000');
    await user.click(screen.getByRole('button', { name: /cadastrar ativo/i }));

    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
    expect(onSubmit.mock.calls[0][0]).toMatchObject({
      assetClass: 'BRAZIL_FIXED_INCOME',
      valuationMode: 'MANUAL',
      nativeCurrency: 'BRL',
      allocationScore: 0,
      quantityStep: undefined,
      manualValuation: {
        amount: 10000,
        currency: 'BRL',
        idempotencyKey: expect.any(String)
      }
    });
  });

  it('accepts a dot-based unit cost as text and rejects a decimal comma', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();
    render(<InstrumentForm onSubmit={onSubmit} />);

    await user.type(screen.getByLabelText('Nome'), 'Apple');
    await user.type(screen.getByLabelText('Ticker'), 'AAPL');
    await user.type(screen.getByLabelText('Bolsa / MIC'), 'XNAS');
    await user.selectOptions(screen.getByLabelText('Moeda nativa'), 'USD');
    await user.click(screen.getByRole('checkbox'));
    await user.type(screen.getByLabelText('Quantidade fracionária'), '2');
    const unitCost = screen.getByLabelText('Custo unitário (opcional)');
    expect(unitCost).toHaveAttribute('type', 'text');

    await user.type(unitCost, '12,34');
    await user.click(screen.getByRole('button', { name: /cadastrar ativo/i }));
    expect(await screen.findByText('Use somente números e ponto como separador decimal.')).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();

    await user.clear(unitCost);
    await user.type(unitCost, '12.34');
    await user.click(screen.getByRole('button', { name: /cadastrar ativo/i }));

    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
    expect(onSubmit.mock.calls[0][0]).toMatchObject({
      openingTransaction: { quantity: 2, unitPrice: 12.34 }
    });
  });
});
