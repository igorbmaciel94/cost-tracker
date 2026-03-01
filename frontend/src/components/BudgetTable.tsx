import { useEffect, useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import type {
  BudgetLineDto,
  BudgetResponseDto,
  CreateCategoryRequest,
  UpdateCategoryRequest
} from '../api/types';
import { formatCurrency } from '../utils/format';
import { applyDirection, compareNumbers, compareStrings, sortIndicator, toggleSort, type SortState } from '../utils/sorting';
import { categorySchema, salarySchema } from '../utils/validators';

interface BudgetTableProps {
  budget: BudgetResponseDto;
  readOnly: boolean;
  onUpdateSalary: (salary: number) => Promise<void>;
  onCreateCategory: (payload: CreateCategoryRequest) => Promise<void>;
  onUpdateCategory: (categoryId: string, payload: UpdateCategoryRequest) => Promise<void>;
  onDeleteCategory: (categoryId: string) => Promise<void>;
}

type SalaryFormData = {
  salary: number;
};

type CategoryFormData = {
  name: string;
  groupName: string;
  plannedAmount: number;
};

type BudgetSortKey = 'displayOrder' | 'name' | 'groupName' | 'planned' | 'spent' | 'difference';

type BudgetEditDraft = {
  name: string;
  groupName: string;
  plannedAmount: string;
};

const initialSortState: SortState<BudgetSortKey> = {
  key: 'displayOrder',
  direction: 'asc'
};

export function BudgetTable({
  budget,
  readOnly,
  onUpdateSalary,
  onCreateCategory,
  onUpdateCategory,
  onDeleteCategory
}: BudgetTableProps) {
  const [sortState, setSortState] = useState<SortState<BudgetSortKey>>(initialSortState);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editingDraft, setEditingDraft] = useState<BudgetEditDraft | null>(null);
  const [editingError, setEditingError] = useState<string | null>(null);

  const salaryForm = useForm<SalaryFormData>({
    resolver: zodResolver(salarySchema),
    defaultValues: {
      salary: budget.salary
    }
  });

  const createCategoryForm = useForm<CategoryFormData>({
    resolver: zodResolver(categorySchema),
    defaultValues: {
      name: '',
      groupName: 'Essenciais',
      plannedAmount: 0
    }
  });

  useEffect(() => {
    salaryForm.reset({ salary: budget.salary });
  }, [budget.salary, salaryForm]);

  useEffect(() => {
    if (!editingId) {
      return;
    }

    const exists = budget.lines.some((line) => line.id === editingId);
    if (!exists) {
      setEditingId(null);
      setEditingDraft(null);
      setEditingError(null);
    }
  }, [budget.lines, editingId]);

  const sortedLines = useMemo(() => {
    const items = [...budget.lines];

    items.sort((left, right) => {
      let comparison = 0;

      switch (sortState.key) {
        case 'displayOrder':
          comparison = compareNumbers(left.displayOrder, right.displayOrder);
          break;
        case 'name':
          comparison = compareStrings(left.name, right.name);
          break;
        case 'groupName':
          comparison = compareStrings(left.groupName, right.groupName);
          break;
        case 'planned':
          comparison = compareNumbers(left.planned, right.planned);
          break;
        case 'spent':
          comparison = compareNumbers(left.spent, right.spent);
          break;
        case 'difference':
          comparison = compareNumbers(left.difference, right.difference);
          break;
      }

      if (comparison === 0) {
        return compareNumbers(left.displayOrder, right.displayOrder);
      }

      return applyDirection(comparison, sortState.direction);
    });

    return items;
  }, [budget.lines, sortState]);

  function startEdit(line: BudgetLineDto) {
    if (readOnly) {
      return;
    }

    setEditingId(line.id);
    setEditingDraft({
      name: line.name,
      groupName: line.groupName,
      plannedAmount: String(line.planned)
    });
    setEditingError(null);
  }

  function cancelEdit() {
    setEditingId(null);
    setEditingDraft(null);
    setEditingError(null);
  }

  async function saveEdit(line: BudgetLineDto) {
    if (!editingDraft) {
      return;
    }

    const plannedAmount = Number(editingDraft.plannedAmount.replace(',', '.'));

    const validation = categorySchema.safeParse({
      name: editingDraft.name,
      groupName: editingDraft.groupName,
      plannedAmount
    });

    if (!validation.success) {
      setEditingError(validation.error.issues[0]?.message ?? 'Dados inválidos.');
      return;
    }

    await onUpdateCategory(line.id, {
      name: validation.data.name,
      groupName: validation.data.groupName,
      plannedAmount: validation.data.plannedAmount,
      displayOrder: line.displayOrder
    });

    cancelEdit();
  }

  async function handleDelete(categoryId: string) {
    if (readOnly) {
      return;
    }

    if (window.confirm('Deseja remover esta categoria?')) {
      await onDeleteCategory(categoryId);
    }
  }

  function sortBy(key: BudgetSortKey) {
    setSortState((current) => toggleSort(current, key));
  }

  return (
    <section className="panel">
      <header className="panel-header">
        <h2>Orçamento do mês {budget.referenceMonth}</h2>
        <form
          className="inline-form"
          onSubmit={salaryForm.handleSubmit(async (data) => {
            await onUpdateSalary(data.salary);
          })}
        >
          <label htmlFor="salary-input">Salário</label>
          <input
            id="salary-input"
            type="number"
            step="0.01"
            disabled={readOnly}
            {...salaryForm.register('salary')}
          />
          <button type="submit" disabled={readOnly}>
            Salvar salário
          </button>
        </form>
      </header>

      <table className="data-table">
        <thead>
          <tr>
            <th>
              <button type="button" className="sort-header" onClick={() => sortBy('name')}>
                Categoria <span>{sortIndicator(sortState, 'name')}</span>
              </button>
            </th>
            <th>
              <button type="button" className="sort-header" onClick={() => sortBy('groupName')}>
                Grupo <span>{sortIndicator(sortState, 'groupName')}</span>
              </button>
            </th>
            <th>
              <button type="button" className="sort-header" onClick={() => sortBy('planned')}>
                Previsto <span>{sortIndicator(sortState, 'planned')}</span>
              </button>
            </th>
            <th>
              <button type="button" className="sort-header" onClick={() => sortBy('spent')}>
                Gasto <span>{sortIndicator(sortState, 'spent')}</span>
              </button>
            </th>
            <th>
              <button type="button" className="sort-header" onClick={() => sortBy('difference')}>
                Diferença <span>{sortIndicator(sortState, 'difference')}</span>
              </button>
            </th>
            <th>Ações</th>
          </tr>
        </thead>
        <tbody>
          {sortedLines.map((line) => {
            const isEditing = editingId === line.id && editingDraft;

            return (
              <tr key={line.id}>
                <td>
                  {isEditing ? (
                    <input
                      value={editingDraft.name}
                      onChange={(event) =>
                        setEditingDraft((current) =>
                          current
                            ? {
                                ...current,
                                name: event.target.value
                              }
                            : current
                        )
                      }
                    />
                  ) : (
                    line.name
                  )}
                </td>
                <td>
                  {isEditing ? (
                    <input
                      value={editingDraft.groupName}
                      onChange={(event) =>
                        setEditingDraft((current) =>
                          current
                            ? {
                                ...current,
                                groupName: event.target.value
                              }
                            : current
                        )
                      }
                    />
                  ) : (
                    line.groupName
                  )}
                </td>
                <td>
                  {isEditing ? (
                    <input
                      type="number"
                      step="0.01"
                      value={editingDraft.plannedAmount}
                      onChange={(event) =>
                        setEditingDraft((current) =>
                          current
                            ? {
                                ...current,
                                plannedAmount: event.target.value
                              }
                            : current
                        )
                      }
                    />
                  ) : (
                    formatCurrency(line.planned)
                  )}
                </td>
                <td>{formatCurrency(line.spent)}</td>
                <td className={line.difference < 0 ? 'negative' : ''}>{formatCurrency(line.difference)}</td>
                <td>
                  <div className="row-actions">
                    {isEditing ? (
                      <>
                        <button type="button" onClick={() => saveEdit(line)}>
                          Salvar
                        </button>
                        <button type="button" className="button-secondary" onClick={cancelEdit}>
                          Cancelar
                        </button>
                      </>
                    ) : (
                      <>
                        <button type="button" disabled={readOnly} onClick={() => startEdit(line)}>
                          Editar
                        </button>
                        <button type="button" disabled={readOnly} onClick={() => handleDelete(line.id)}>
                          Excluir
                        </button>
                      </>
                    )}
                  </div>
                  {editingId === line.id && editingError && (
                    <p className="inline-error" role="alert">
                      {editingError}
                    </p>
                  )}
                </td>
              </tr>
            );
          })}
          <tr className="totals-row">
            <td>Total</td>
            <td>-</td>
            <td>{formatCurrency(budget.plannedTotal)}</td>
            <td>{formatCurrency(budget.spentTotal)}</td>
            <td className={budget.differenceTotal < 0 ? 'negative' : ''}>
              {formatCurrency(budget.differenceTotal)}
            </td>
            <td />
          </tr>
        </tbody>
      </table>

      <form
        className="inline-form"
        onSubmit={createCategoryForm.handleSubmit(async (data) => {
          await onCreateCategory(data);
          createCategoryForm.reset({ name: '', groupName: data.groupName, plannedAmount: 0 });
        })}
      >
        <h3>Nova categoria</h3>
        <input placeholder="Nome" disabled={readOnly} {...createCategoryForm.register('name')} />
        <input placeholder="Grupo" disabled={readOnly} {...createCategoryForm.register('groupName')} />
        <input
          type="number"
          step="0.01"
          placeholder="Previsto"
          disabled={readOnly}
          {...createCategoryForm.register('plannedAmount')}
        />
        <button type="submit" disabled={readOnly}>
          Adicionar
        </button>
      </form>
    </section>
  );
}
