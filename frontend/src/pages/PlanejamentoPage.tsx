import { useState } from 'react';
import { formatCurrency } from '../utils/format';
import { PrivacyMask } from '../contexts/PrivacyContext';

type Goal = {
  id: number;
  name: string;
  totalAmount: number;
  savedAmount: number;
  months: number;
};

let nextId = 1;

function calcMonthly(goal: Goal) {
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

export function PlanejamentoPage() {
  const [goals, setGoals] = useState<Goal[]>([]);
  const [salary, setSalary] = useState('');
  const [name, setName] = useState('');
  const [totalAmount, setTotalAmount] = useState('');
  const [savedAmount, setSavedAmount] = useState('');
  const [months, setMonths] = useState('');
  const [error, setError] = useState<string | null>(null);

  function addGoal() {
    const total = Number(totalAmount.replace(',', '.'));
    const saved = Number(savedAmount.replace(',', '.'));
    const m = Number(months);

    if (!name.trim()) { setError('Informe o nome do objetivo.'); return; }
    if (!total || total <= 0) { setError('Informe um valor total válido.'); return; }
    if (saved < 0 || saved > total) { setError('Valor já guardado inválido.'); return; }
    if (!m || m <= 0 || !Number.isInteger(m)) { setError('Informe um prazo em meses válido.'); return; }

    setError(null);
    setGoals((prev) => [...prev, { id: nextId++, name: name.trim(), totalAmount: total, savedAmount: saved, months: m }]);
    setName('');
    setTotalAmount('');
    setSavedAmount('');
    setMonths('');
  }

  function removeGoal(id: number) {
    setGoals((prev) => prev.filter((g) => g.id !== id));
  }

  const parsedSalary = Number(salary.replace(',', '.'));
  const totalMonthly = goals.reduce((sum, g) => sum + calcMonthly(g), 0);

  return (
    <section className="panel">
      <header className="panel-header">
        <h2>Planejamento de objetivos</h2>
      </header>

      <div className="inline-form" style={{ marginBottom: '1.5rem' }}>
        <label htmlFor="plan-salary">Salário (referência)</label>
        <input
          id="plan-salary"
          type="number"
          step="0.01"
          placeholder="Ex: 5000"
          value={salary}
          onChange={(e) => setSalary(e.target.value)}
          style={{ maxWidth: 160 }}
        />
      </div>

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
          placeholder="Valor total (R$)"
          value={totalAmount}
          onChange={(e) => setTotalAmount(e.target.value)}
        />
        <input
          type="number"
          step="0.01"
          placeholder="Já guardado (R$)"
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
        <button type="button" onClick={addGoal}>
          Adicionar
        </button>
      </div>
      {error && <p className="inline-error" role="alert">{error}</p>}

      {goals.length > 0 && (
        <>
          <table className="data-table" style={{ marginTop: '1.5rem' }}>
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
                      <button type="button" className="button-secondary" onClick={() => removeGoal(goal.id)}>
                        Remover
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>

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
