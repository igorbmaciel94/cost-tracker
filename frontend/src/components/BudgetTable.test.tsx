import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { BudgetResponseDto } from '../api/types';
import { PrivacyProvider } from '../contexts/PrivacyContext';
import { BudgetTable } from './BudgetTable';

const budget: BudgetResponseDto = {
  monthId: 'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11',
  referenceMonth: '2026-02',
  salary: 2000,
  plannedTotal: 1200,
  spentTotal: 800,
  differenceTotal: 400,
  lines: [
    {
      id: 'c0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11',
      name: 'Mercado',
      groupName: 'Custos Fixos',
      planned: 600,
      spent: 500,
      difference: 100,
      displayOrder: 1
    }
  ]
};

describe('BudgetTable', () => {
  beforeEach(() => {
    const store: Record<string, string> = {};
    vi.stubGlobal('localStorage', {
      getItem: (key: string) => store[key] ?? null,
      setItem: (key: string, value: string) => {
        store[key] = value;
      },
      removeItem: (key: string) => {
        delete store[key];
      },
      clear: () => {
        Object.keys(store).forEach((key) => {
          delete store[key];
        });
      }
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('renders sortable headers', () => {
    render(
      <BudgetTable
        budget={budget}
        readOnly={false}
        onUpdateSalary={async () => {}}
        onCreateCategory={async () => {}}
        onUpdateCategory={async () => {}}
        onDeleteCategory={async () => {}}
      />
    );

    expect(screen.getByRole('button', { name: /categoria/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /grupo/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /previsto/i })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Conforto' })).toBeInTheDocument();
  });

  it('edits a category inline and saves', async () => {
    const user = userEvent.setup();
    const onUpdateCategory = vi.fn(async () => {});

    render(
      <BudgetTable
        budget={budget}
        readOnly={false}
        onUpdateSalary={async () => {}}
        onCreateCategory={async () => {}}
        onUpdateCategory={onUpdateCategory}
        onDeleteCategory={async () => {}}
      />
    );

    await user.click(screen.getByRole('button', { name: /^editar$/i }));

    const nameInput = screen.getByDisplayValue('Mercado');
    await user.clear(nameInput);
    await user.type(nameInput, 'Mercado Premium');

    await user.click(screen.getByRole('button', { name: /^salvar$/i }));

    await waitFor(() => {
      expect(onUpdateCategory).toHaveBeenCalledWith('c0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11', {
        name: 'Mercado Premium',
        groupName: 'Custos Fixos',
        plannedAmount: 600,
        displayOrder: 1
      });
    });
  });

  it('masks planned line amounts when privacy mode is enabled', () => {
    localStorage.setItem('privacy-mode', '1');

    render(
      <PrivacyProvider>
        <BudgetTable
          budget={budget}
          readOnly={false}
          onUpdateSalary={async () => {}}
          onCreateCategory={async () => {}}
          onUpdateCategory={async () => {}}
          onDeleteCategory={async () => {}}
        />
      </PrivacyProvider>
    );

    expect(screen.queryByText(/600,00/)).not.toBeInTheDocument();
    expect(screen.getAllByText('***').length).toBeGreaterThan(0);

    localStorage.removeItem('privacy-mode');
  });
});
