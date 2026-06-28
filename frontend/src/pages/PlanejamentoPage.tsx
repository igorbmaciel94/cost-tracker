import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../api/client';
import type { PlanningGoalDto, UpdatePlanningGoalRequest } from '../api/types';
import { formatCurrency } from '../utils/format';
import { PrivacyMask } from '../contexts/PrivacyContext';

interface GoalDraft {
  name: string;
  totalAmount: string;
  savedAmount: string;
  months: string;
}

const emptyGoalDraft: GoalDraft = {
  name: '',
  totalAmount: '',
  savedAmount: '',
  months: ''
};

function calcMonthly(goal: PlanningGoalDto) {
  const remaining = Math.max(0, goal.totalAmount - goal.savedAmount);
  return goal.months > 0 ? remaining / goal.months : 0;
}

function parseDecimal(value: string) {
  const normalized = value.trim().replace(',', '.');
  if (!normalized) return 0;
  return Number(normalized);
}

function draftFromGoal(goal: PlanningGoalDto): GoalDraft {
  return {
    name: goal.name,
    totalAmount: String(goal.totalAmount),
    savedAmount: String(goal.savedAmount),
    months: String(goal.months)
  };
}

function validateDraft(draft: GoalDraft): { ok: true; payload: UpdatePlanningGoalRequest } | { ok: false; error: string } {
  const total = parseDecimal(draft.totalAmount);
  const saved = parseDecimal(draft.savedAmount);
  const months = Number(draft.months);

  if (!draft.name.trim()) return { ok: false, error: 'Informe o nome da meta.' };
  if (!Number.isFinite(total) || total <= 0) return { ok: false, error: 'Informe um valor total válido.' };
  if (!Number.isFinite(saved) || saved < 0 || saved > total) return { ok: false, error: 'Valor já guardado inválido.' };
  if (!Number.isFinite(months) || months <= 0 || !Number.isInteger(months)) {
    return { ok: false, error: 'Informe um prazo em meses válido.' };
  }

  return {
    ok: true,
    payload: {
      name: draft.name.trim(),
      totalAmount: total,
      savedAmount: saved,
      months
    }
  };
}

function MonthlyImpact({ monthly, salary }: { monthly: number; salary: number }) {
  if (salary <= 0) return null;
  const pct = (monthly / salary) * 100;
  return (
    <span className={pct > 30 ? 'negative' : ''}>
      {' '}({pct.toFixed(1)}% do salário)
    </span>
  );
}

export function PlanejamentoPage({ salary }: { salary: number }) {
  const queryClient = useQueryClient();
  const [name, setName] = useState('');
  const [totalAmount, setTotalAmount] = useState('');
  const [savedAmount, setSavedAmount] = useState('');
  const [months, setMonths] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editDraft, setEditDraft] = useState<GoalDraft>(emptyGoalDraft);
  const [editError, setEditError] = useState<string | null>(null);

  const goalsQuery = useQuery({
    queryKey: ['planning-goals'],
    queryFn: api.getPlanningGoals
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['planning-goals'] });

  const createGoal = useMutation({
    mutationFn: api.createPlanningGoal,
    onSuccess: invalidate
  });

  const updateGoal = useMutation({
    mutationFn: ({ goalId, payload }: { goalId: string; payload: UpdatePlanningGoalRequest }) =>
      api.updatePlanningGoal(goalId, payload),
    onSuccess: async () => {
      await invalidate();
      setEditingId(null);
      setEditDraft(emptyGoalDraft);
      setEditError(null);
    }
  });

  const deleteGoal = useMutation({
    mutationFn: api.deletePlanningGoal,
    onSuccess: invalidate
  });

  async function addGoal() {
    const result = validateDraft({ name, totalAmount, savedAmount, months });
    if (!result.ok) {
      setError(result.error);
      return;
    }

    setError(null);
    await createGoal.mutateAsync(result.payload);
    setName('');
    setTotalAmount('');
    setSavedAmount('');
    setMonths('');
  }

  function startEditing(goal: PlanningGoalDto) {
    setEditingId(goal.id);
    setEditDraft(draftFromGoal(goal));
    setEditError(null);
  }

  function cancelEditing() {
    setEditingId(null);
    setEditDraft(emptyGoalDraft);
    setEditError(null);
  }

  async function saveEditedGoal(goalId: string) {
    const result = validateDraft(editDraft);
    if (!result.ok) {
      setEditError(result.error);
      return;
    }

    setEditError(null);
    await updateGoal.mutateAsync({ goalId, payload: result.payload });
  }

  const goals = goalsQuery.data ?? [];
  const parsedSalary = salary;
  const totalMonthly = goals.reduce((sum, g) => sum + calcMonthly(g), 0);

  if (goalsQuery.isLoading) {
    return <p className="center-message">A carregar metas...</p>;
  }

  if (goalsQuery.isError) {
    return <p className="center-message">Falha ao carregar as metas.</p>;
  }

  return (
    <section className="panel">
      <header className="panel-header">
        <h2>Metas</h2>
      </header>

      <h3>Nova meta</h3>
      <div className="inline-form">
        <input
          placeholder="Nome da meta"
          value={name}
          onChange={(e) => setName(e.target.value)}
          style={{ minWidth: 180 }}
        />
        <input
          type="number"
          step="0.01"
          placeholder="Valor total (€)"
          value={totalAmount}
          onChange={(e) => setTotalAmount(e.target.value)}
        />
        <input
          type="number"
          step="0.01"
          placeholder="Já guardado (€)"
          value={savedAmount}
          onChange={(e) => setSavedAmount(e.target.value)}
        />
        <input
          type="number"
          step="1"
          placeholder="Prazo (meses)"
          value={months}
          onChange={(e) => setMonths(e.target.value)}
        />
        <button type="button" onClick={() => { void addGoal(); }} disabled={createGoal.isPending}>
          {createGoal.isPending ? 'A adicionar...' : 'Adicionar'}
        </button>
      </div>
      {error && <p className="inline-error" role="alert">{error}</p>}

      {goals.length > 0 && (
        <>
          <div className="table-scroll planning-goals-scroll">
            <table className="data-table planning-goals-table">
              <thead>
                <tr>
                  <th>Meta</th>
                  <th>Valor total</th>
                  <th>Já guardado</th>
                  <th>Falta</th>
                  <th>Prazo</th>
                  <th>Guardar/mês</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {goals.map((goal) => {
                  const monthly = calcMonthly(goal);
                  const remaining = Math.max(0, goal.totalAmount - goal.savedAmount);
                  const isEditing = editingId === goal.id;
                  const editedTotal = parseDecimal(editDraft.totalAmount);
                  const editedSaved = parseDecimal(editDraft.savedAmount);
                  const editedMonths = Number(editDraft.months);
                  const editedRemaining = Number.isFinite(editedTotal) && Number.isFinite(editedSaved)
                    ? Math.max(0, editedTotal - editedSaved)
                    : 0;
                  const editedMonthly = Number.isFinite(editedMonths) && editedMonths > 0
                    ? editedRemaining / editedMonths
                    : 0;

                  if (isEditing) {
                    return (
                      <tr key={goal.id}>
                        <td>
                          <input
                            aria-label="Nome da meta"
                            value={editDraft.name}
                            onChange={(e) => setEditDraft((current) => ({ ...current, name: e.target.value }))}
                          />
                        </td>
                        <td>
                          <input
                            aria-label="Valor total"
                            type="number"
                            step="0.01"
                            value={editDraft.totalAmount}
                            onChange={(e) => setEditDraft((current) => ({ ...current, totalAmount: e.target.value }))}
                          />
                        </td>
                        <td>
                          <input
                            aria-label="Já guardado"
                            type="number"
                            step="0.01"
                            value={editDraft.savedAmount}
                            onChange={(e) => setEditDraft((current) => ({ ...current, savedAmount: e.target.value }))}
                          />
                        </td>
                        <td><PrivacyMask value={formatCurrency(editedRemaining)} /></td>
                        <td>
                          <input
                            aria-label="Prazo em meses"
                            type="number"
                            step="1"
                            value={editDraft.months}
                            onChange={(e) => setEditDraft((current) => ({ ...current, months: e.target.value }))}
                          />
                        </td>
                        <td>
                          <PrivacyMask value={formatCurrency(editedMonthly)} />
                          <MonthlyImpact monthly={editedMonthly} salary={parsedSalary} />
                        </td>
                        <td>
                          <div className="row-actions">
                            <button
                              type="button"
                              onClick={() => { void saveEditedGoal(goal.id); }}
                              disabled={updateGoal.isPending}
                            >
                              {updateGoal.isPending ? 'A guardar...' : 'Guardar'}
                            </button>
                            <button
                              type="button"
                              className="button-secondary"
                              onClick={cancelEditing}
                              disabled={updateGoal.isPending}
                            >
                              Cancelar
                            </button>
                          </div>
                          {editError && <p className="inline-error" role="alert">{editError}</p>}
                        </td>
                      </tr>
                    );
                  }

                  return (
                    <tr key={goal.id}>
                      <td>{goal.name}</td>
                      <td><PrivacyMask value={formatCurrency(goal.totalAmount)} /></td>
                      <td><PrivacyMask value={formatCurrency(goal.savedAmount)} /></td>
                      <td><PrivacyMask value={formatCurrency(remaining)} /></td>
                      <td>{goal.months} {goal.months === 1 ? 'mês' : 'meses'}</td>
                      <td>
                        <PrivacyMask value={formatCurrency(monthly)} />
                        <MonthlyImpact monthly={monthly} salary={parsedSalary} />
                      </td>
                      <td>
                        <div className="row-actions">
                          <button
                            type="button"
                            className="button-secondary"
                            onClick={() => startEditing(goal)}
                            disabled={Boolean(editingId) || updateGoal.isPending || deleteGoal.isPending}
                          >
                            Editar
                          </button>
                          <button
                            type="button"
                            className="button-secondary"
                            onClick={() => { void deleteGoal.mutateAsync(goal.id); }}
                            disabled={Boolean(editingId) || deleteGoal.isPending}
                          >
                            Remover
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          <div className="panel-header" style={{ marginTop: '1rem', borderTop: '1px solid var(--border)', paddingTop: '1rem' }}>
            <p>
              <strong>Total a guardar por mês: </strong>
              <PrivacyMask value={formatCurrency(totalMonthly)} />
              <MonthlyImpact monthly={totalMonthly} salary={parsedSalary} />
            </p>
          </div>
        </>
      )}

      {goals.length === 0 && (
        <p style={{ marginTop: '1.5rem', color: 'var(--text-secondary)' }}>
          Nenhuma meta adicionada ainda. Use o formulário acima para calcular quanto guardar por mês.
        </p>
      )}
    </section>
  );
}
