import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { AllocationEditor } from './AllocationEditor';

vi.mock('./AllocationDonut', () => ({
  AllocationDonut: () => <div data-testid="allocation-donut" />
}));

describe('AllocationEditor', () => {
  it('uses only integer sliders and submits when all five categories total exactly 100%', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();
    render(<AllocationEditor targets={[]} onSubmit={onSubmit} />);

    const submit = screen.getByRole('button', { name: /guardar alocação/i });
    expect(submit).toBeDisabled();
    expect(screen.queryAllByRole('spinbutton')).toHaveLength(0);

    const sliders = screen.getAllByRole('slider');
    expect(sliders).toHaveLength(5);
    for (const slider of sliders) {
      expect(slider).toHaveAttribute('min', '0');
      expect(slider).toHaveAttribute('max', '100');
      expect(slider).toHaveAttribute('step', '1');
      fireEvent.change(slider, { target: { value: '20' } });
    }

    expect(screen.getAllByText('20%')).toHaveLength(5);
    expect(within(screen.getByText('Total definido').parentElement as HTMLElement).getByText('100%')).toBeInTheDocument();
    await waitFor(() => expect(submit).toBeEnabled());
    await user.click(submit);

    expect(onSubmit).toHaveBeenCalledWith({
      STOCKS: 20,
      REITS: 20,
      BRAZIL_FIXED_INCOME: 20,
      INTERNATIONAL_FIXED_INCOME: 20,
      CRYPTOCURRENCIES: 20
    });
  });

  it('allows an unconfigured legacy allocation to save its rounded integer targets unchanged', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();
    render(
      <AllocationEditor
        allowUnchangedSubmit
        targets={[
          { assetClass: 'STOCKS', weight: 0.154 },
          { assetClass: 'REITS', weight: 0.146 },
          { assetClass: 'BRAZIL_FIXED_INCOME', weight: 0.204 },
          { assetClass: 'INTERNATIONAL_FIXED_INCOME', weight: 0.296 },
          { assetClass: 'CRYPTOCURRENCIES', weight: 0.2 }
        ]}
        onSubmit={onSubmit}
      />
    );

    const submit = screen.getByRole('button', { name: /guardar alocação/i });
    expect(submit).toBeEnabled();
    await user.click(submit);

    expect(onSubmit).toHaveBeenCalledWith({
      STOCKS: 15,
      REITS: 15,
      BRAZIL_FIXED_INCOME: 20,
      INTERNATIONAL_FIXED_INCOME: 30,
      CRYPTOCURRENCIES: 20
    });
  });
});
