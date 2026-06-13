import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../api/client';
import type { PlanningGoalDto } from '../api/types';
import { formatCurrency } from '../utils/format';
import { PrivacyMask } from '../contexts/PrivacyContext';

function calcMonthly(goal: PlanningGoalDto) {
  const remaining = Math.max(0, goal.totalAmount - goal.savedAmount);
  return goal.months > 0 ? remaining / goal.months : 0;
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

  const goalsQuery = useQuery({
    queryKey: ['planning-goals'],
    queryFn: api.getPlanningGoals
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['planning-goals'] });

  const createGoal = useMutation({
    mutationFn: api.createPlanningGoal,
    onSuccess: invalidate
  });

  const deleteGoal = useMutation({
    mutationFn: api.deletePlanningGoal,
    onSuccess: invalidate
  });

  async function addGoal() {
    const total = Number(totalAmount.replace(',', '.'));
    const saved = Number(savedAmount.replace(',', '.'));
    const m = Number(months);

    if (!name.trim()) { setError('Informe o nome do objetivo.'); return; }
    if (!total || total <= 0) { setError('Informe um valor total válido.'); return; }
    if (saved < 0 || saved > total) { setError('Valor já guardado inválido.'); return; }
    if (!m || m <= 0 || !Number.isInteger(m)) { setError('Informe um prazo em meses válido.'); return; }

    setError(null);
    await createGoal.mutateAsync({ name: name.trim(), totalAmount: total, savedAmount: saved, months: m });
    setName('');
    setTotalAmount('');
    setSavedAmount('');
    setMonths('');
  }

  const goals = goalsQuery.data ?? [];
  const parsedSalary = salary;
  const totalMonthly = goals.reduce((sum, g) => sum + calcMonthly(g), 0);

  if (goalsQuery.isLoading) {
    return <p className="center-message">A carregar objetivos...</p>;
  }

  if (goalsQuery.isError) {
    return <p className="center-message">Falha ao carregar os objetivos.</p>;
  }

  return (
    <section className="panel">
      <header className="panel-header">
        <h2>Planejamento de objetivos</h2>
      </header>

      <h3>Novo objetivo</h3>
      <div className="inline-form">
        <input
          placeholder="Nome do objetivo"
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
                  <th>Objetivo</th>
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
                        <button
                          type="button"
                          className="button-secondary"
                          onClick={() => { void deleteGoal.mutateAsync(goal.id); }}
                          disabled={deleteGoal.isPending}
                        >
                          Remover
                        </button>
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
          Nenhum objetivo adicionado ainda. Use o formulário acima para calcular quanto guardar por mês.
        </p>
      )}
    </section>
  );
}
