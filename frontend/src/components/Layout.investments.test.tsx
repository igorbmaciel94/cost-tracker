import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeAll, describe, expect, it, vi } from 'vitest';
import { PrivacyProvider } from '../contexts/PrivacyContext';
import { ThemeProvider } from '../contexts/ThemeContext';
import { Layout } from './Layout';

beforeAll(() => {
  const values = new Map<string, string>();
  vi.stubGlobal('localStorage', {
    getItem: vi.fn((key: string) => values.get(key) ?? null),
    setItem: vi.fn((key: string, value: string) => values.set(key, value)),
    removeItem: vi.fn((key: string) => values.delete(key)),
    clear: vi.fn(() => values.clear())
  });
  vi.stubGlobal('matchMedia', vi.fn().mockReturnValue({
    matches: false,
    addEventListener: vi.fn(),
    removeEventListener: vi.fn()
  }));
});

describe('Layout on investment routes', () => {
  it('keeps global controls but hides monthly context', () => {
    render(
      <MemoryRouter initialEntries={['/investimentos']}>
        <ThemeProvider>
          <PrivacyProvider>
            <Layout
              months={[{ id: 'month-1', referenceMonth: '2026-08', salary: 4000, currency: 'EUR', status: 'OPEN', plannedTotal: 2000, spentTotal: 1000, differenceTotal: 3000, isOverPlanned: false, isOverSpent: false }]}
              selectedMonthId="month-1"
              selectedMonth={{ id: 'month-1', referenceMonth: '2026-08', salary: 4000, currency: 'EUR', status: 'OPEN', plannedTotal: 2000, spentTotal: 1000, differenceTotal: 3000, isOverPlanned: false, isOverSpent: false }}
              onSelectMonth={vi.fn()}
              onCreateMonth={vi.fn()}
              creatingMonth={false}
              username="igor"
              onLogout={vi.fn()}
              loggingOut={false}
              onGenerateAnalysis={vi.fn()}
              generatingAnalysis={false}
              analysisError={false}
            >
              <p>Carteira independente</p>
            </Layout>
          </PrivacyProvider>
        </ThemeProvider>
      </MemoryRouter>
    );

    expect(screen.getByText('Carteira independente')).toBeInTheDocument();
    expect(screen.queryByLabelText(/mês/i)).not.toBeInTheDocument();
    expect(screen.queryByText('Salário base')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /ocultar valores financeiros/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /investimentos/i })).toHaveClass('active');
  });
});
