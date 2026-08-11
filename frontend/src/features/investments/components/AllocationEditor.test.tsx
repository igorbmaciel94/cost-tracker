import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { AllocationEditor } from './AllocationEditor';

vi.mock('./AllocationDonut', () => ({
  AllocationDonut: () => <div data-testid="allocation-donut" />
}));

describe('AllocationEditor', () => {
  it('only submits when all four classes total exactly 100%', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();
    render(<AllocationEditor targets={[]} onSubmit={onSubmit} />);

    const submit = screen.getByRole('button', { name: /guardar alocação/i });
    expect(submit).toBeDisabled();

    for (const input of screen.getAllByRole('spinbutton')) {
      await user.clear(input);
      await user.type(input, '25');
    }

    expect(screen.getByText('100,00%')).toBeInTheDocument();
    expect(submit).toBeEnabled();
    await user.click(submit);

    expect(onSubmit).toHaveBeenCalledWith({
      STOCKS: 2500,
      REITS: 2500,
      BRAZIL_FIXED_INCOME: 2500,
      INTERNATIONAL_FIXED_INCOME: 2500
    });
  });
});
