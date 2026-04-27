import { useEffect, useMemo, useState } from 'react';
import type { TargetGroupDto, TargetsResponseDto, UpdateTargetsRequest } from '../api/types';
import { formatPercent } from '../utils/format';
import { applyDirection, compareNumbers, compareStrings, sortIndicator, toggleSort, type SortState } from '../utils/sorting';

interface TargetTableProps {
  targets: TargetsResponseDto;
  readOnly: boolean;
  onSave: (request: UpdateTargetsRequest) => Promise<void>;
}

type TargetSortKey =
  | 'groupName'
  | 'targetPercent'
  | 'currentPlannedPercent'
  | 'plannedStatus'
  | 'currentSpentPercent'
  | 'spentStatus';

const initialSortState: SortState<TargetSortKey> = {
  key: 'groupName',
  direction: 'asc'
};

function toDraftMap(items: TargetGroupDto[]): Record<string, number> {
  return items.reduce<Record<string, number>>((acc, item) => {
    acc[item.groupName] = Number((item.targetPercent * 100).toFixed(2));
    return acc;
  }, {});
}

export function TargetTable({ targets, readOnly, onSave }: TargetTableProps) {
  const [draft, setDraft] = useState<Record<string, number>>(toDraftMap(targets.items));
  const [sortState, setSortState] = useState<SortState<TargetSortKey>>(initialSortState);

  useEffect(() => {
    setDraft(toDraftMap(targets.items));
  }, [targets.items]);

  const sortedItems = useMemo(() => {
    const items = [...targets.items];

    items.sort((left, right) => {
      let comparison = 0;

      switch (sortState.key) {
        case 'groupName':
          comparison = compareStrings(left.groupName, right.groupName);
          break;
        case 'targetPercent':
          comparison = compareNumbers(left.targetPercent, right.targetPercent);
          break;
        case 'currentPlannedPercent':
          comparison = compareNumbers(left.currentPlannedPercent, right.currentPlannedPercent);
          break;
        case 'plannedStatus':
          comparison = compareStrings(left.plannedStatus, right.plannedStatus);
          break;
        case 'currentSpentPercent':
          comparison = compareNumbers(left.currentSpentPercent, right.currentSpentPercent);
          break;
        case 'spentStatus':
          comparison = compareStrings(left.spentStatus, right.spentStatus);
          break;
      }

      if (comparison === 0) {
        return compareStrings(left.groupName, right.groupName);
      }

      return applyDirection(comparison, sortState.direction);
    });

    return items;
  }, [targets.items, sortState]);

  function sortBy(key: TargetSortKey) {
    setSortState((current) => toggleSort(current, key));
  }

  return (
    <section className="panel">
      <header className="panel-header">
        <h2>Metas por grupo ({targets.referenceMonth})</h2>
        <button
          type="button"
          disabled={readOnly}
          onClick={async () => {
            await onSave({
              items: Object.entries(draft).map(([groupName, targetPercent]) => ({
                groupName,
                targetPercent: Number((targetPercent / 100).toFixed(4))
              }))
            });
          }}
        >
          Salvar metas
        </button>
      </header>

      <div className="table-scroll">
      <table className="data-table">
        <thead>
          <tr>
            <th>
              <button type="button" className="sort-header" onClick={() => sortBy('groupName')}>
                Grupo <span>{sortIndicator(sortState, 'groupName')}</span>
              </button>
            </th>
            <th>
              <button type="button" className="sort-header" onClick={() => sortBy('targetPercent')}>
                % alvo <span>{sortIndicator(sortState, 'targetPercent')}</span>
              </button>
            </th>
            <th>
              <button type="button" className="sort-header" onClick={() => sortBy('currentPlannedPercent')}>
                % atual (previsto) <span>{sortIndicator(sortState, 'currentPlannedPercent')}</span>
              </button>
            </th>
            <th>
              <button type="button" className="sort-header" onClick={() => sortBy('plannedStatus')}>
                Status previsto <span>{sortIndicator(sortState, 'plannedStatus')}</span>
              </button>
            </th>
            <th>
              <button type="button" className="sort-header" onClick={() => sortBy('currentSpentPercent')}>
                % atual (gasto) <span>{sortIndicator(sortState, 'currentSpentPercent')}</span>
              </button>
            </th>
            <th>
              <button type="button" className="sort-header" onClick={() => sortBy('spentStatus')}>
                Status gasto <span>{sortIndicator(sortState, 'spentStatus')}</span>
              </button>
            </th>
          </tr>
        </thead>
        <tbody>
          {sortedItems.map((item) => (
            <tr key={item.groupName}>
              <td>{item.groupName}</td>
              <td>
                <input
                  type="number"
                  min={0}
                  max={100}
                  step={0.1}
                  disabled={readOnly}
                  value={draft[item.groupName] ?? Number((item.targetPercent * 100).toFixed(2))}
                  onChange={(event) => {
                    const parsed = Number(event.target.value);
                    setDraft((current) => ({
                      ...current,
                      [item.groupName]: Number.isNaN(parsed) ? 0 : Math.min(100, Math.max(0, parsed))
                    }));
                  }}
                />
              </td>
              <td>{formatPercent(item.currentPlannedPercent)}</td>
              <td>{item.plannedStatus}</td>
              <td>{formatPercent(item.currentSpentPercent)}</td>
              <td>{item.spentStatus}</td>
            </tr>
          ))}
        </tbody>
      </table>
      </div>
    </section>
  );
}
