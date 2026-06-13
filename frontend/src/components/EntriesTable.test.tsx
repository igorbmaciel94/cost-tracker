import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { BudgetLineDto, EntriesResponseDto } from '../api/types';
import { EntriesTable } from './EntriesTable';

const categories: BudgetLineDto[] = [
  {
    id: 'c0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11',
    name: 'Mercado',
    groupName: 'Custos Fixos',
    planned: 600,
    spent: 500,
    difference: 100,
    displayOrder: 1
  }
];

const entries: EntriesResponseDto = {
  monthId: 'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11',
  referenceMonth: '2026-03',
  totalSpent: 20,
  items: [
    {
      id: 'e0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11',
      categoryBudgetId: categories[0].id,
      categoryName: 'Mercado',
      entryDate: '2026-03-01',
      description: 'Compra inicial',
      amount: 20
    }
  ]
};

describe('EntriesTable', () => {
  it('edits an entry inline and saves', async () => {
    const user = userEvent.setup();
    const onUpdateEntry = vi.fn(async () => {});

    render(
      <EntriesTable
        entries={entries}
        categories={categories}
        readOnly={false}
        onCreateEntry={async () => {}}
        onUpdateEntry={onUpdateEntry}
        onDeleteEntry={async () => {}}
      />
    );

    await user.click(screen.getByRole('button', { name: /^editar$/i }));

    const descriptionInput = screen.getByDisplayValue('Compra inicial');
    await user.clear(descriptionInput);
    await user.type(descriptionInput, 'Compra atualizada');

    await user.click(screen.getByRole('button', { name: /^salvar$/i }));

    await waitFor(() => {
      expect(onUpdateEntry).toHaveBeenCalledWith('e0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11', {
        categoryBudgetId: categories[0].id,
        entryDate: '2026-03-01',
        description: 'Compra atualizada',
        amount: 20
      });
    });
  });
});
