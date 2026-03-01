import type { MonthSummaryDto } from '../api/types';

interface MonthSelectorProps {
  months: MonthSummaryDto[];
  selectedMonthId: string | null;
  onChange: (monthId: string) => void;
}

export function MonthSelector({ months, selectedMonthId, onChange }: MonthSelectorProps) {
  return (
    <label className="month-selector" htmlFor="month-selector">
      Mês:
      <select
        id="month-selector"
        value={selectedMonthId ?? ''}
        onChange={(event) => onChange(event.target.value)}
      >
        {months.map((month) => (
          <option key={month.id} value={month.id}>
            {month.referenceMonth} ({month.status === 'OPEN' ? 'Aberto' : 'Fechado'})
          </option>
        ))}
      </select>
    </label>
  );
}
