import { Cell, Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts';
import { ASSET_CLASS_META, ASSET_CLASSES } from '../constants';
import type { AssetClass } from '../types';
import { formatPercent } from '../../../utils/format';

interface AllocationDonutProps {
  values: Partial<Record<AssetClass, number>>;
  centerLabel?: string;
  centerValue?: string;
  title?: string;
}

export function AllocationDonut({
  values,
  centerLabel = 'Distribuição',
  centerValue,
  title = 'Distribuição por classe'
}: AllocationDonutProps) {
  const data = ASSET_CLASSES.map((assetClass) => ({
    assetClass,
    label: ASSET_CLASS_META[assetClass].label,
    value: Math.max(0, values[assetClass] ?? 0)
  })).filter((item) => item.value > 0);
  const chartData = data.length > 0 ? data : [{ assetClass: 'STOCKS' as const, label: 'Sem dados', value: 1 }];

  return (
    <figure className="allocation-figure">
      <figcaption>{title}</figcaption>
      <div className="allocation-chart" aria-hidden="true">
        <ResponsiveContainer width="100%" height="100%">
          <PieChart>
            <Pie data={chartData} dataKey="value" nameKey="label" innerRadius="64%" outerRadius="88%" stroke="var(--surface)" strokeWidth={2}>
              {chartData.map((entry) => (
                <Cell
                  key={entry.assetClass}
                  fill={data.length === 0 ? 'var(--border)' : ASSET_CLASS_META[entry.assetClass].color}
                />
              ))}
            </Pie>
            {data.length > 0 && (
              <Tooltip formatter={(value) => formatPercent(Number(value))} />
            )}
          </PieChart>
        </ResponsiveContainer>
        <div className="allocation-chart-center">
          {centerValue && <strong>{centerValue}</strong>}
          <span>{centerLabel}</span>
        </div>
      </div>
      <ul className="allocation-legend" aria-label="Resumo textual da distribuição">
        {ASSET_CLASSES.map((assetClass) => (
          <li key={assetClass}>
            <span className="allocation-dot" style={{ background: ASSET_CLASS_META[assetClass].color }} />
            <span>{ASSET_CLASS_META[assetClass].shortLabel}</span>
            <strong>{formatPercent(values[assetClass] ?? 0)}</strong>
          </li>
        ))}
      </ul>
    </figure>
  );
}
