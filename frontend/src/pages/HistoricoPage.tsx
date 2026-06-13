import { useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '../api/client';
import type { MonthSummaryDto } from '../api/types';
import { BudgetTable } from '../components/BudgetTable';
import { MonthlySummaryCards } from '../components/MonthlySummaryCards';
import { formatCurrency } from '../utils/format';
import { PrivacyMask } from '../contexts/PrivacyContext';
import { applyDirection, compareNumbers, compareStrings, sortIndicator, toggleSort, type SortState } from '../utils/sorting';

interface HistoricoPageProps {
  months: MonthSummaryDto[];
}

type HistorySortKey = 'referenceMonth' | 'salary' | 'plannedTotal' | 'spentTotal';

const initialSortState: SortState<HistorySortKey> = {
  key: 'referenceMonth',
  direction: 'desc'
};

export function HistoricoPage({ months }: HistoricoPageProps) {
  const [sortState, setSortState] = useState<SortState<HistorySortKey>>(initialSortState);

  const closedMonths = useMemo(() => months.filter((month) => month.status === 'CLOSED'), [months]);

  const sortedClosedMonths = useMemo(() => {
    const items = [...closedMonths];

    items.sort((left, right) => {
      let comparison = 0;

      switch (sortState.key) {
        case 'referenceMonth':
          comparison = compareStrings(left.referenceMonth, right.referenceMonth);
          break;
        case 'salary':
          comparison = compareNumbers(left.salary, right.salary);
          break;
        case 'plannedTotal':
          comparison = compareNumbers(left.plannedTotal, right.plannedTotal);
          break;
        case 'spentTotal':
          comparison = compareNumbers(left.spentTotal, right.spentTotal);
          break;
      }

      if (comparison === 0) {
        return compareStrings(left.referenceMonth, right.referenceMonth);
      }

      return applyDirection(comparison, sortState.direction);
    });

    return items;
  }, [closedMonths, sortState]);

  const [selectedMonthId, setSelectedMonthId] = useState<string | null>(sortedClosedMonths[0]?.id ?? null);

  useEffect(() => {
    setSelectedMonthId((current) => {
      if (current && sortedClosedMonths.some((month) => month.id === current)) {
        return current;
      }

      return sortedClosedMonths[0]?.id ?? null;
    });
  }, [sortedClosedMonths]);

  const budgetQuery = useQuery({
    queryKey: ['history-budget', selectedMonthId],
    queryFn: () => api.getBudget(selectedMonthId as string),
    enabled: Boolean(selectedMonthId)
  });

  const dashboardQuery = useQuery({
    queryKey: ['history-dashboard', selectedMonthId],
    queryFn: () => api.getDashboard(selectedMonthId as string),
    enabled: Boolean(selectedMonthId)
  });

  function sortBy(key: HistorySortKey) {
    setSortState((current) => toggleSort(current, key));
  }

  if (sortedClosedMonths.length === 0) {
    return <p>Sem meses fechados no histórico ainda.</p>;
  }

  return (
    <div className="page-stack">
      <section className="panel">
        <header className="panel-header">
          <h2>Histórico de meses fechados</h2>
        </header>
        <div className="table-scroll">
          <table className="data-table history-table">
            <thead>
              <tr>
                <th>
                  <button type="button" className="sort-header" onClick={() => sortBy('referenceMonth')}>
                    Mês <span>{sortIndicator(sortState, 'referenceMonth')}</span>
                  </button>
                </th>
                <th>
                  <button type="button" className="sort-header" onClick={() => sortBy('salary')}>
                    Salário <span>{sortIndicator(sortState, 'salary')}</span>
                  </button>
                </th>
                <th>
                  <button type="button" className="sort-header" onClick={() => sortBy('plannedTotal')}>
                    Previsto <span>{sortIndicator(sortState, 'plannedTotal')}</span>
                  </button>
                </th>
                <th>
                  <button type="button" className="sort-header" onClick={() => sortBy('spentTotal')}>
                    Gasto <span>{sortIndicator(sortState, 'spentTotal')}</span>
                  </button>
                </th>
                <th>Ação</th>
              </tr>
            </thead>
            <tbody>
              {sortedClosedMonths.map((month) => (
                <tr key={month.id}>
                  <td>{month.referenceMonth}</td>
                  <td><PrivacyMask value={formatCurrency(month.salary)} /></td>
                  <td><PrivacyMask value={formatCurrency(month.plannedTotal)} /></td>
                  <td><PrivacyMask value={formatCurrency(month.spentTotal)} /></td>
                  <td>
                    <button type="button" onClick={() => setSelectedMonthId(month.id)}>
                      Ver detalhes
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      {dashboardQuery.data && <MonthlySummaryCards dashboard={dashboardQuery.data} />}

      {budgetQuery.data && (
        <BudgetTable
          budget={budgetQuery.data}
          readOnly
          onUpdateSalary={async () => {}}
          onCreateCategory={async () => {}}
          onUpdateCategory={async () => {}}
          onDeleteCategory={async () => {}}
        />
      )}
    </div>
  );
}
