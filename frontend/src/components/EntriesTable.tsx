import { useEffect, useMemo, useState, useCallback } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import type {
  BudgetLineDto,
  CreateEntryRequest,
  EntriesResponseDto,
  UpdateEntryRequest
} from '../api/types';
import { formatCurrency, formatDateIsoToPt } from '../utils/format';
import { PrivacyMask } from '../contexts/PrivacyContext';
import {
  applyDirection,
  compareDateIso,
  compareNumbers,
  compareStrings,
  sortIndicator,
  toggleSort,
  type SortState
} from '../utils/sorting';
import { entrySchema } from '../utils/validators';
import { ConfirmModal } from './ConfirmModal';

interface EntriesTableProps {
  entries: EntriesResponseDto;
  categories: BudgetLineDto[];
  readOnly: boolean;
  onCreateEntry: (payload: CreateEntryRequest) => Promise<void>;
  onUpdateEntry: (entryId: string, payload: UpdateEntryRequest) => Promise<void>;
  onDeleteEntry: (entryId: string) => Promise<void>;
}

type EntryFormData = {
  categoryBudgetId: string;
  entryDate: string;
  description: string;
  amount: number;
};

type EntrySortKey = 'entryDate' | 'categoryName' | 'description' | 'amount';

type EntryEditDraft = {
  categoryBudgetId: string;
  entryDate: string;
  description: string;
  amount: string;
};

const initialSortState: SortState<EntrySortKey> = {
  key: 'entryDate',
  direction: 'desc'
};

function todayIsoDate(): string {
  return new Date().toISOString().slice(0, 10);
}

export function EntriesTable({
  entries,
  categories,
  readOnly,
  onCreateEntry,
  onUpdateEntry,
  onDeleteEntry
}: EntriesTableProps) {
  const [sortState, setSortState] = useState<SortState<EntrySortKey>>(initialSortState);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editingDraft, setEditingDraft] = useState<EntryEditDraft | null>(null);
  const [editingError, setEditingError] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const form = useForm<EntryFormData>({
    resolver: zodResolver(entrySchema),
    defaultValues: {
      categoryBudgetId: categories[0]?.id ?? '',
      entryDate: todayIsoDate(),
      description: '',
      amount: 0
    }
  });

  useEffect(() => {
    const currentCategory = form.getValues('categoryBudgetId');
    if (!categories.some((category) => category.id === currentCategory)) {
      form.setValue('categoryBudgetId', categories[0]?.id ?? '');
    }
  }, [categories, form]);

  useEffect(() => {
    if (!editingId) {
      return;
    }

    const exists = entries.items.some((item) => item.id === editingId);
    if (!exists) {
      setEditingId(null);
      setEditingDraft(null);
      setEditingError(null);
    }
  }, [entries.items, editingId]);

  const sortedItems = useMemo(() => {
    const items = [...entries.items];

    items.sort((left, right) => {
      let comparison = 0;

      switch (sortState.key) {
        case 'entryDate':
          comparison = compareDateIso(left.entryDate, right.entryDate);
          break;
        case 'categoryName':
          comparison = compareStrings(left.categoryName, right.categoryName);
          break;
        case 'description':
          comparison = compareStrings(left.description, right.description);
          break;
        case 'amount':
          comparison = compareNumbers(left.amount, right.amount);
          break;
      }

      if (comparison === 0) {
        return compareDateIso(left.entryDate, right.entryDate);
      }

      return applyDirection(comparison, sortState.direction);
    });

    return items;
  }, [entries.items, sortState]);

  function startEdit(
    entryId: string,
    categoryBudgetId: string,
    entryDate: string,
    description: string,
    amount: number
  ) {
    if (readOnly) {
      return;
    }

    setEditingId(entryId);
    setEditingDraft({
      categoryBudgetId,
      entryDate,
      description,
      amount: String(amount)
    });
    setEditingError(null);
  }

  function cancelEdit() {
    setEditingId(null);
    setEditingDraft(null);
    setEditingError(null);
  }

  async function saveEdit(entryId: string) {
    if (!editingDraft) {
      return;
    }

    const amount = Number(editingDraft.amount.replace(',', '.'));

    const validation = entrySchema.safeParse({
      categoryBudgetId: editingDraft.categoryBudgetId,
      entryDate: editingDraft.entryDate,
      description: editingDraft.description,
      amount
    });

    if (!validation.success) {
      setEditingError(validation.error.issues[0]?.message ?? 'Dados inválidos.');
      return;
    }

    await onUpdateEntry(entryId, validation.data);
    cancelEdit();
  }

  function handleDelete(entryId: string) {
    if (readOnly) return;
    setDeletingId(entryId);
  }

  const confirmDelete = useCallback(async () => {
    if (deletingId) {
      await onDeleteEntry(deletingId);
      setDeletingId(null);
    }
  }, [deletingId, onDeleteEntry]);

  function sortBy(key: EntrySortKey) {
    setSortState((current) => toggleSort(current, key));
  }

  return (
    <section className="panel">
      <header className="panel-header">
        <h2>Lançamentos do mês {entries.referenceMonth}</h2>
        <p>Total gasto: <PrivacyMask value={formatCurrency(entries.totalSpent)} /></p>
      </header>

      <form
        className="inline-form"
        onSubmit={form.handleSubmit(async (data) => {
          await onCreateEntry(data);
          form.reset({
            categoryBudgetId: data.categoryBudgetId,
            entryDate: todayIsoDate(),
            description: '',
            amount: 0
          });
        })}
      >
        <h3>Novo lançamento</h3>
        <select disabled={readOnly} {...form.register('categoryBudgetId')}>
          {categories.map((category) => (
            <option key={category.id} value={category.id}>
              {category.name}
            </option>
          ))}
        </select>
        <input type="date" disabled={readOnly} {...form.register('entryDate')} />
        <input placeholder="Descrição" disabled={readOnly} {...form.register('description')} />
        <input type="number" step="0.01" disabled={readOnly} {...form.register('amount')} />
        <button type="submit" disabled={readOnly}>
          Lançar
        </button>
      </form>

      <div className="table-scroll">
      <table className="data-table">
        <thead>
          <tr>
            <th>
              <button type="button" className="sort-header" onClick={() => sortBy('entryDate')}>
                Data <span>{sortIndicator(sortState, 'entryDate')}</span>
              </button>
            </th>
            <th>
              <button type="button" className="sort-header" onClick={() => sortBy('categoryName')}>
                Categoria <span>{sortIndicator(sortState, 'categoryName')}</span>
              </button>
            </th>
            <th>
              <button type="button" className="sort-header" onClick={() => sortBy('description')}>
                Descrição <span>{sortIndicator(sortState, 'description')}</span>
              </button>
            </th>
            <th>
              <button type="button" className="sort-header" onClick={() => sortBy('amount')}>
                Valor <span>{sortIndicator(sortState, 'amount')}</span>
              </button>
            </th>
            <th>Ações</th>
          </tr>
        </thead>
        <tbody>
          {sortedItems.map((entry) => {
            const isEditing = editingId === entry.id && editingDraft;

            return (
              <tr key={entry.id}>
                <td>
                  {isEditing ? (
                    <input
                      type="date"
                      value={editingDraft.entryDate}
                      onChange={(event) =>
                        setEditingDraft((current) =>
                          current
                            ? {
                                ...current,
                                entryDate: event.target.value
                              }
                            : current
                        )
                      }
                    />
                  ) : (
                    formatDateIsoToPt(entry.entryDate)
                  )}
                </td>
                <td>
                  {isEditing ? (
                    <select
                      value={editingDraft.categoryBudgetId}
                      onChange={(event) =>
                        setEditingDraft((current) =>
                          current
                            ? {
                                ...current,
                                categoryBudgetId: event.target.value
                              }
                            : current
                        )
                      }
                    >
                      {categories.map((category) => (
                        <option key={category.id} value={category.id}>
                          {category.name}
                        </option>
                      ))}
                    </select>
                  ) : (
                    entry.categoryName
                  )}
                </td>
                <td>
                  {isEditing ? (
                    <input
                      value={editingDraft.description}
                      onChange={(event) =>
                        setEditingDraft((current) =>
                          current
                            ? {
                                ...current,
                                description: event.target.value
                              }
                            : current
                        )
                      }
                    />
                  ) : (
                    entry.description
                  )}
                </td>
                <td>
                  {isEditing ? (
                    <input
                      type="number"
                      step="0.01"
                      value={editingDraft.amount}
                      onChange={(event) =>
                        setEditingDraft((current) =>
                          current
                            ? {
                                ...current,
                                amount: event.target.value
                              }
                            : current
                        )
                      }
                    />
                  ) : (
                    <PrivacyMask value={formatCurrency(entry.amount)} />
                  )}
                </td>
                <td>
                  <div className="row-actions">
                    {isEditing ? (
                      <>
                        <button type="button" onClick={() => saveEdit(entry.id)}>
                          Salvar
                        </button>
                        <button type="button" className="button-secondary" onClick={cancelEdit}>
                          Cancelar
                        </button>
                      </>
                    ) : (
                      <>
                        <button
                          type="button"
                          disabled={readOnly}
                          onClick={() =>
                            startEdit(
                              entry.id,
                              entry.categoryBudgetId,
                              entry.entryDate,
                              entry.description,
                              entry.amount
                            )
                          }
                        >
                          Editar
                        </button>
                        <button type="button" disabled={readOnly} onClick={() => handleDelete(entry.id)}>
                          Excluir
                        </button>
                      </>
                    )}
                  </div>
                  {editingId === entry.id && editingError && (
                    <p className="inline-error" role="alert">
                      {editingError}
                    </p>
                  )}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
      </div>

      <ConfirmModal
        open={deletingId !== null}
        title="Remover lançamento?"
        description="Esta ação não pode ser desfeita."
        confirmLabel="Remover"
        onConfirm={() => { void confirmDelete(); }}
        onCancel={() => setDeletingId(null)}
      />
    </section>
  );
}
