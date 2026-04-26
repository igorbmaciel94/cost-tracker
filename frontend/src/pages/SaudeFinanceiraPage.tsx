import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../api/client';
import { formatCurrency, formatPercent } from '../utils/format';
import { PrivacyMask } from '../contexts/PrivacyContext';

const RATES = [
  { label: 'Conta remunerada (~2% a.a.)', monthly: 0.02 / 12 },
  { label: 'Depósito a prazo (~3.5% a.a.)', monthly: 0.035 / 12 },
  { label: 'ETF obrigações (~4.5% a.a.)', monthly: 0.045 / 12 },
  { label: 'ETF ações globais (~7% a.a.)', monthly: 0.07 / 12 },
];

function compoundFV(monthly: number, rate: number, months: number): number {
  if (rate === 0) return monthly * months;
  return monthly * ((Math.pow(1 + rate, months) - 1) / rate);
}

function ProgressBar({ value, max }: { value: number; max: number }) {
  const pct = max > 0 ? Math.min(100, (value / max) * 100) : 0;
  const color = pct >= 100 ? '#0f766e' : pct >= 50 ? '#f59e0b' : '#ef4444';
  return (
    <div style={{ background: 'var(--border)', borderRadius: 8, height: 10, overflow: 'hidden', margin: '0.5rem 0' }}>
      <div style={{ width: `${pct}%`, background: color, height: '100%', borderRadius: 8, transition: 'width 0.3s' }} />
    </div>
  );
}

export function SaudeFinanceiraPage({ monthId, salary }: { monthId: string | null; salary: number }) {
  const queryClient = useQueryClient();
  const [essentials, setEssentials] = useState('');
  const [saved, setSaved] = useState('');
  const [committedEssentials, setCommittedEssentials] = useState(0);
  const [committedSaved, setCommittedSaved] = useState(0);
  const [monthlyInvest, setMonthlyInvest] = useState('');
  const [committedMonthly, setCommittedMonthly] = useState(0);
  const [rateIndex, setRateIndex] = useState(1);
  const [profileLoaded, setProfileLoaded] = useState(false);

  const profileQuery = useQuery({
    queryKey: ['health-profile'],
    queryFn: api.getHealthProfile
  });

  const targetsQuery = useQuery({
    queryKey: ['targets', monthId],
    queryFn: () => api.getTargets(monthId!),
    enabled: !!monthId
  });

  useEffect(() => {
    if (profileLoaded || !profileQuery.data || (!!monthId && !targetsQuery.data)) return;
    const profile = profileQuery.data;

    const items = targetsQuery.data?.items ?? [];
    const targetPct = (name: string) => {
      const g = items.find((g) => g.groupName === name);
      return g && salary > 0 ? salary * g.targetPercent : 0;
    };

    let ess = profile.essentialExpenses;
    if (ess === 0) ess = targetPct('Essenciais');

    let sav = profile.savedEmergencyFund;
    if (sav === 0) sav = targetPct('Saving');

    let inv = targetPct('Investimento');

    setEssentials(ess > 0 ? String(ess) : '');
    setSaved(sav > 0 ? String(sav) : '');
    setCommittedEssentials(ess);
    setCommittedSaved(sav);

    if (inv > 0) {
      setMonthlyInvest(String(inv));
      setCommittedMonthly(inv);
    }

    setProfileLoaded(true);
  }, [profileQuery.data, targetsQuery.data, profileLoaded, salary, monthId]);

  const saveProfile = useMutation({
    mutationFn: api.updateHealthProfile,
    onSuccess: () => { void queryClient.invalidateQueries({ queryKey: ['health-profile'] }); }
  });

  function handleSave() {
    const essentialsVal = Number(essentials.replace(',', '.')) || 0;
    const savedVal = Number(saved.replace(',', '.')) || 0;
    setCommittedEssentials(essentialsVal);
    setCommittedSaved(savedVal);
    saveProfile.mutate({ essentialExpenses: essentialsVal, savedEmergencyFund: savedVal });
  }

  const essentialsVal = committedEssentials;
  const savedVal = committedSaved;
  const monthlyVal = committedMonthly;
  const rate = RATES[rateIndex].monthly;

  const targets = [
    { label: '3 meses (mínimo)', months: 3 },
    { label: '6 meses (recomendado)', months: 6 },
    { label: '12 meses (ideal)', months: 12 },
  ];

  const projections = [
    { label: '1 ano', months: 12 },
    { label: '2 anos', months: 24 },
    { label: '5 anos', months: 60 },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>

      {/* Colchão de Emergência */}
      <section className="panel">
        <header className="panel-header">
          <h2>Colchão de emergência</h2>
        </header>

        <div className="inline-form" style={{ marginBottom: '1.5rem' }}>
          <label htmlFor="essentials-input">Gastos essenciais mensais (€)</label>
          <input
            id="essentials-input"
            type="number"
            step="0.01"
            placeholder="Ex: 3000"
            value={essentials}
            onChange={(e) => setEssentials(e.target.value)}
            style={{ maxWidth: 180 }}
          />
          <label htmlFor="saved-input">Já reservado (€)</label>
          <input
            id="saved-input"
            type="number"
            step="0.01"
            placeholder="Ex: 5000"
            value={saved}
            onChange={(e) => setSaved(e.target.value)}
            style={{ maxWidth: 180 }}
          />
          <button
            type="button"
            onClick={handleSave}
            disabled={saveProfile.isPending}
          >
            {saveProfile.isPending ? 'A guardar...' : 'Guardar'}
          </button>
        </div>

        {essentialsVal > 0 ? (
          <table className="data-table">
            <thead>
              <tr>
                <th>Cenário</th>
                <th>Meta</th>
                <th>Já reservado</th>
                <th>Faltam</th>
                <th>Progresso</th>
              </tr>
            </thead>
            <tbody>
              {targets.map(({ label, months }) => {
                const target = essentialsVal * months;
                const gap = Math.max(0, target - savedVal);
                const pct = target > 0 ? Math.min(1, savedVal / target) : 0;
                return (
                  <tr key={months}>
                    <td>{label}</td>
                    <td><PrivacyMask value={formatCurrency(target)} /></td>
                    <td><PrivacyMask value={formatCurrency(Math.min(savedVal, target))} /></td>
                    <td className={gap > 0 ? 'negative' : ''}>
                      <PrivacyMask value={gap > 0 ? formatCurrency(gap) : '✓ Atingido'} />
                    </td>
                    <td style={{ minWidth: 140 }}>
                      <ProgressBar value={savedVal} max={target} />
                      <small style={{ color: 'var(--text-secondary)' }}>{formatPercent(pct)}</small>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        ) : (
          <p style={{ color: 'var(--text-secondary)' }}>
            Informe seus gastos essenciais mensais para calcular o colchão ideal.
          </p>
        )}
      </section>

      {/* Simulador de Investimento */}
      <section className="panel">
        <header className="panel-header">
          <h2>Simulador de investimento</h2>
        </header>

        <div className="inline-form" style={{ marginBottom: '1.5rem' }}>
          <label htmlFor="monthly-invest">Aporte mensal (€)</label>
          <input
            id="monthly-invest"
            type="number"
            step="0.01"
            placeholder="Ex: 500"
            value={monthlyInvest}
            onChange={(e) => setMonthlyInvest(e.target.value)}
            style={{ maxWidth: 160 }}
          />
          <label htmlFor="rate-select">Produto</label>
          <select
            id="rate-select"
            value={rateIndex}
            onChange={(e) => setRateIndex(Number(e.target.value))}
          >
            {RATES.map((r, i) => (
              <option key={r.label} value={i}>{r.label}</option>
            ))}
          </select>
          <button
            type="button"
            onClick={() => setCommittedMonthly(Number(monthlyInvest.replace(',', '.')) || 0)}
          >
            Simular
          </button>
        </div>

        {monthlyVal > 0 ? (
          <table className="data-table">
            <thead>
              <tr>
                <th>Período</th>
                <th>Total investido</th>
                <th>Saldo estimado</th>
                <th>Rendimento</th>
              </tr>
            </thead>
            <tbody>
              {projections.map(({ label, months }) => {
                const invested = monthlyVal * months;
                const fv = compoundFV(monthlyVal, rate, months);
                const gain = fv - invested;
                return (
                  <tr key={months}>
                    <td>{label}</td>
                    <td><PrivacyMask value={formatCurrency(invested)} /></td>
                    <td><PrivacyMask value={formatCurrency(fv)} /></td>
                    <td style={{ color: '#0f766e' }}>
                      <PrivacyMask value={`+${formatCurrency(gain)}`} />
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        ) : (
          <p style={{ color: 'var(--text-secondary)' }}>
            Informe o aporte mensal para ver a projeção de crescimento.
          </p>
        )}
      </section>

      {/* Simulador de ETFs/Ações */}
      <section className="panel">
        <header className="panel-header">
          <h2>Estimativa ETFs / ações</h2>
        </header>

        <p style={{ color: 'var(--text-secondary)', fontSize: '0.875rem', marginBottom: '1rem' }}>
          ⚠️ Valores estimados com base em históricos. Renda variável não garante retorno. Use como referência educativa.
        </p>

        {monthlyVal > 0 ? (
          <table className="data-table">
            <thead>
              <tr>
                <th>Período</th>
                <th>Total investido</th>
                <th>Cenário conservador <small>(~5% a.a.)</small></th>
                <th>Cenário otimista <small>(~10% a.a.)</small></th>
              </tr>
            </thead>
            <tbody>
              {projections.map(({ label, months }) => {
                const invested = monthlyVal * months;
                const conservative = compoundFV(monthlyVal, 0.05 / 12, months);
                const optimistic = compoundFV(monthlyVal, 0.10 / 12, months);
                return (
                  <tr key={months}>
                    <td>{label}</td>
                    <td><PrivacyMask value={formatCurrency(invested)} /></td>
                    <td><PrivacyMask value={formatCurrency(conservative)} /></td>
                    <td><PrivacyMask value={formatCurrency(optimistic)} /></td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        ) : (
          <p style={{ color: 'var(--text-secondary)' }}>
            Informe o aporte mensal no simulador acima para ver as estimativas de renda variável.
          </p>
        )}
      </section>

    </div>
  );
}
