import type { DashboardOverBudgetCategoryDto } from '../api/types';
import { PrivacyMask } from '../contexts/PrivacyContext';
import { formatCurrency } from '../utils/format';

interface OverBudgetCategoriesCardProps {
  items: DashboardOverBudgetCategoryDto[];
}

export function OverBudgetCategoriesCard({ items }: OverBudgetCategoriesCardProps) {
  if (items.length === 0) {
    return null;
  }

  const totalExceeded = items.reduce((sum, item) => sum + item.exceededBy, 0);

  return (
    <section className="panel over-budget-panel">
      <header className="panel-header">
        <div>
          <h2>Excessos por categoria</h2>
          <p>{items.length} {items.length === 1 ? 'categoria excedida' : 'categorias excedidas'}</p>
        </div>
        <strong className="over-budget-total">
          <PrivacyMask value={formatCurrency(totalExceeded)} />
        </strong>
      </header>

      <div className="table-scroll">
        <table className="data-table over-budget-table">
          <thead>
            <tr>
              <th>Categoria</th>
              <th>Grupo</th>
              <th>Previsto</th>
              <th>Gasto</th>
              <th>Excesso</th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={`${item.groupName}-${item.category}`}>
                <td>{item.category}</td>
                <td>{item.groupName}</td>
                <td><PrivacyMask value={formatCurrency(item.planned)} /></td>
                <td><PrivacyMask value={formatCurrency(item.spent)} /></td>
                <td className="negative">
                  <PrivacyMask value={formatCurrency(item.exceededBy)} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
