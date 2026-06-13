import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { OverBudgetCategoriesCard } from './OverBudgetCategoriesCard';

describe('OverBudgetCategoriesCard', () => {
  it('renders exceeded categories and total', () => {
    render(
      <OverBudgetCategoriesCard
        items={[
          {
            category: 'Credito',
            groupName: 'Conforto',
            planned: 75,
            spent: 384,
            exceededBy: 309
          },
          {
            category: 'Compras online',
            groupName: 'Prazeres',
            planned: 172,
            spent: 305,
            exceededBy: 133
          }
        ]}
      />
    );

    expect(screen.getByRole('heading', { name: /excessos por categoria/i })).toBeInTheDocument();
    expect(screen.getByText('Credito')).toBeInTheDocument();
    expect(screen.getByText('Compras online')).toBeInTheDocument();
    expect(screen.getByText(/442,00/)).toBeInTheDocument();
    expect(screen.getByText(/309,00/)).toBeInTheDocument();
    expect(screen.getByText(/133,00/)).toBeInTheDocument();
  });

  it('renders nothing when there are no exceeded categories', () => {
    const { container } = render(<OverBudgetCategoriesCard items={[]} />);

    expect(container).toBeEmptyDOMElement();
  });
});
