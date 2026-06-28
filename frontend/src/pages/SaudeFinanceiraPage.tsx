import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../api/client';
import { formatCurrency, formatPercent } from '../utils/format';
import { PrivacyMask } from '../contexts/PrivacyContext';

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
    if (ess === 0) ess = targetPct('Custos Fixos');

    const round2 = (n: number) => Math.round(n * 100) / 100;
    const savedFund = round2(profile.savedEmergencyFund);

    setEssentials(ess > 0 ? String(round2(ess)) : '');
    setSaved(savedFund > 0 ? String(savedFund) : '');
    setCommittedEssentials(round2(ess));
    setCommittedSaved(savedFund);

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

  const targets = [
    { label: '3 meses (mínimo)', months: 3 },
    { label: '6 meses (recomendado)', months: 6 },
    { label: '12 meses (ideal)', months: 12 },
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
          <div className="table-scroll">
            <table className="data-table health-table">
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
                      <td><PrivacyMask value={formatCurrency(savedVal)} /></td>
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
          </div>
        ) : (
          <p style={{ color: 'var(--text-secondary)' }}>
            Informe seus gastos essenciais mensais para calcular o colchão ideal.
          </p>
        )}
      </section>

    </div>
  );
}
