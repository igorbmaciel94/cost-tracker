import { useEffect, useMemo, useState, type CSSProperties } from 'react';
import type { TargetGroupDto, TargetsResponseDto, UpdateTargetsRequest } from '../api/types';
import { CANONICAL_GROUP_NAMES } from '../constants/groups';
import { PrivacyMask } from '../contexts/PrivacyContext';
import { formatCurrency, formatPercent } from '../utils/format';

interface TargetTableProps {
  targets: TargetsResponseDto;
  readOnly: boolean;
  plannedTotal: number;
  spentTotal: number;
  onSave: (request: UpdateTargetsRequest) => Promise<void>;
}

const groupColors: Record<string, string> = {
  'Custos Fixos': '#38bdf8',
  Conforto: '#5eead4',
  Metas: '#facc15',
  Prazeres: '#e879f9',
  'Liberdade Financeira': '#60a5fa',
  Conhecimento: '#fb923c'
};

function toDraftMap(items: TargetGroupDto[]): Record<string, number> {
  return items.reduce<Record<string, number>>((acc, item) => {
    acc[item.groupName] = clampPercent(Number((item.targetPercent * 100).toFixed(0)));
    return acc;
  }, {});
}

function clampPercent(value: number) {
  if (!Number.isFinite(value)) return 0;
  return Math.min(100, Math.max(0, Math.round(value)));
}

function formatPercentValue(value: number) {
  return `${value.toLocaleString('pt-PT', { maximumFractionDigits: 0 })}%`;
}

function getGroupColor(groupName: string, index: number) {
  return groupColors[groupName] ?? `hsl(${(index * 57) % 360} 82% 64%)`;
}

function getGroupOrder(groupName: string) {
  const index = CANONICAL_GROUP_NAMES.findIndex((name) => name === groupName);
  return index === -1 ? Number.MAX_SAFE_INTEGER : index;
}

function buildDonutGradient(items: TargetGroupDto[], draft: Record<string, number>) {
  const total = items.reduce((sum, item) => sum + (draft[item.groupName] ?? 0), 0);
  const denominator = Math.max(100, total);
  let cursor = 0;
  const segments = items.flatMap((item, index) => {
    const value = draft[item.groupName] ?? 0;
    if (value <= 0) return [];

    const start = (cursor / denominator) * 100;
    cursor += value;
    const end = (cursor / denominator) * 100;
    return `${getGroupColor(item.groupName, index)} ${start}% ${end}%`;
  });

  if (cursor < 100) {
    segments.push(`var(--border) ${(cursor / denominator) * 100}% 100%`);
  }

  return `conic-gradient(${segments.length > 0 ? segments.join(', ') : 'var(--border) 0 100%'})`;
}

function resolveStatus(difference: number) {
  if (Math.abs(difference) <= 0.005) {
    return 'OK';
  }

  return difference > 0 ? 'Acima' : 'Abaixo';
}

function getFiniteAmount(value: number | undefined, fallback: number): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
}

function getSpentUsagePercent(spentAmount: number, targetAmount: number) {
  if (targetAmount <= 0) {
    return spentAmount > 0 ? 1 : 0;
  }

  return spentAmount / targetAmount;
}

function formatSpentBalance(spentDifference: number, targetAmount: number) {
  if (Math.abs(spentDifference) <= 0.005) {
    return targetAmount > 0 ? 'Limite fechado' : 'Sem gasto previsto';
  }

  return spentDifference > 0
    ? `Excedeu ${formatCurrency(spentDifference)}`
    : `Restam ${formatCurrency(Math.abs(spentDifference))}`;
}

export function TargetTable({ targets, readOnly, plannedTotal, spentTotal, onSave }: TargetTableProps) {
  const [draft, setDraft] = useState<Record<string, number>>(toDraftMap(targets.items));
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    setDraft(toDraftMap(targets.items));
  }, [targets.items]);

  const sortedItems = useMemo(() => {
    const items = [...targets.items];

    items.sort((left, right) => {
      const orderComparison = getGroupOrder(left.groupName) - getGroupOrder(right.groupName);
      if (orderComparison !== 0) {
        return orderComparison;
      }

      return left.groupName.localeCompare(right.groupName, 'pt-PT');
    });

    return items;
  }, [targets.items]);

  const totalPercent = sortedItems.reduce((sum, item) => sum + (draft[item.groupName] ?? 0), 0);
  const isBalanced = totalPercent === 100;
  const totalDelta = 100 - totalPercent;
  const donutGradient = buildDonutGradient(sortedItems, draft);
  const canSave = !readOnly && isBalanced && !isSaving;

  async function saveTargets() {
    if (!canSave) return;

    setIsSaving(true);
    try {
      await onSave({
        items: sortedItems.map((item) => ({
          groupName: item.groupName,
          targetPercent: Number(((draft[item.groupName] ?? 0) / 100).toFixed(4))
        }))
      });
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <section className="panel">
      <header className="panel-header">
        <h2>Planejamento mensal por grupo ({targets.referenceMonth})</h2>
        <button
          type="button"
          disabled={!canSave}
          onClick={() => { void saveTargets(); }}
        >
          {isSaving ? 'Salvando...' : 'Salvar planejamento'}
        </button>
      </header>

      <div className="target-total-banner" data-balanced={isBalanced}>
        <strong>Total: {formatPercentValue(totalPercent)}</strong>
        <span>
          {isBalanced
            ? 'Distribuição fechada em 100%.'
            : totalDelta > 0
              ? `Ainda faltam ${formatPercentValue(totalDelta)} para liberar o salvamento.`
              : `Reduza ${formatPercentValue(Math.abs(totalDelta))} para liberar o salvamento.`}
        </span>
      </div>

      <div className="target-planner">
        <aside className="target-summary" aria-label="Resumo do planejamento">
          <div className="target-donut" style={{ background: donutGradient }} aria-hidden="true" />
          <div className="target-legend">
            {sortedItems.map((item, index) => {
              const percent = draft[item.groupName] ?? 0;
              const color = getGroupColor(item.groupName, index);
              return (
                <div className="target-legend-item" key={item.groupName}>
                  <span className="target-legend-dot" style={{ background: color }} />
                  <span>{item.groupName}</span>
                  <strong>{formatPercentValue(percent)}</strong>
                </div>
              );
            })}
          </div>
        </aside>

        <div className="target-slider-list">
          {sortedItems.map((item, index) => {
            const percent = draft[item.groupName] ?? 0;
            const color = getGroupColor(item.groupName, index);
            const sliderStyle = {
              '--target-color': color,
              '--target-progress': `${percent}%`
            } as CSSProperties;

            return (
              <label className="target-slider-row" key={item.groupName}>
                <span className="target-slider-header">
                  <span>{item.groupName}</span>
                  <strong>{formatPercentValue(percent)}</strong>
                </span>
                <input
                  className="target-slider"
                  type="range"
                  min={0}
                  max={100}
                  step={1}
                  disabled={readOnly}
                  value={percent}
                  style={sliderStyle}
                  onChange={(event) => {
                    const parsed = clampPercent(Number(event.target.value));
                    setDraft((current) => ({
                      ...current,
                      [item.groupName]: parsed
                    }));
                  }}
                />
                <span className="target-slider-scale" aria-hidden="true">
                  <span>0%</span>
                  <span>100%</span>
                </span>
              </label>
            );
          })}
        </div>
      </div>

      <section className="target-checklist" aria-label="Valores planejados por grupo">
        <header className="target-checklist-header">
          <h3>Leitura do planejamento</h3>
          <p>Previsto em percentuais; gasto contra o limite em valor.</p>
        </header>

        <div className="table-scroll target-checklist-scroll">
          <table className="data-table target-checklist-table">
            <thead>
              <tr>
                <th>Setor</th>
                <th>Meta</th>
                <th>Planejado atual</th>
                <th>Status planejado</th>
                <th>Gasto atual</th>
                <th>Status gasto</th>
              </tr>
            </thead>
            <tbody>
              {sortedItems.map((item, index) => {
                const targetPercent = (draft[item.groupName] ?? 0) / 100;
                const plannedDifference = item.currentPlannedPercent - targetPercent;
                const plannedStatus = resolveStatus(plannedDifference);
                const targetAmount = plannedTotal * targetPercent;
                const plannedAmount = getFiniteAmount(
                  item.currentPlannedAmount,
                  plannedTotal * item.currentPlannedPercent
                );
                const spentAmount = getFiniteAmount(
                  item.currentSpentAmount,
                  spentTotal * item.currentSpentPercent
                );
                const spentDifference = spentAmount - targetAmount;
                const spentStatus = resolveStatus(spentDifference);
                const spentUsagePercent = getSpentUsagePercent(spentAmount, targetAmount);
                const color = getGroupColor(item.groupName, index);

                return (
                  <tr key={item.groupName}>
                    <td>
                      <span className="target-sector-cell">
                        <span className="target-legend-dot" style={{ background: color }} />
                        {item.groupName}
                      </span>
                    </td>
                    <td>
                      <strong>{formatPercentValue(Math.round(targetPercent * 100))}</strong>
                      <small><PrivacyMask value={formatCurrency(targetAmount)} /></small>
                    </td>
                    <td>
                      <strong>{formatPercentValue(Math.round(item.currentPlannedPercent * 100))}</strong>
                      <small><PrivacyMask value={formatCurrency(plannedAmount)} /></small>
                    </td>
                    <td>
                      <span className="status-pill" data-status={plannedStatus}>
                        {plannedStatus}
                      </span>
                    </td>
                    <td>
                      <strong>
                        <PrivacyMask value={`${formatCurrency(spentAmount)} de ${formatCurrency(targetAmount)}`} />
                      </strong>
                      <small>
                        <PrivacyMask value={formatSpentBalance(spentDifference, targetAmount)} />
                        {targetAmount > 0 ? ` - ${formatPercent(spentUsagePercent)} do limite` : null}
                      </small>
                    </td>
                    <td>
                      <span className="status-pill status-pill-spent" data-status={spentStatus}>
                        {spentStatus}
                      </span>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </section>
    </section>
  );
}
