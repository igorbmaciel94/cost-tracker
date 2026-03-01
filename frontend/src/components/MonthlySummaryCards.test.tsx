import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { DashboardDto } from '../api/types';
import { MonthlySummaryCards } from './MonthlySummaryCards';

const dashboard: DashboardDto = {
  monthId: 'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11',
  referenceMonth: '2026-03',
  salary: 2000,
  plannedTotal: 1800,
  spentTotal: 500,
  isOverPlanned: false,
  isOverSpent: false,
  categoryChart: [],
  groupPie: []
};

describe('MonthlySummaryCards', () => {
  it('shows saldo atual using spent total', () => {
    render(<MonthlySummaryCards dashboard={dashboard} />);

    expect(screen.getByText(/Saldo atual/i)).toBeInTheDocument();
    expect(screen.getByText(/1500,00/)).toBeInTheDocument();
  });
});
