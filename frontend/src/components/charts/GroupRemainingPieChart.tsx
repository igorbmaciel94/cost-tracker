import { Cell, Legend, Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts';
import type { DashboardGroupPointDto } from '../../api/types';
import { formatCurrency } from '../../utils/format';
import { usePrivacy } from '../../contexts/PrivacyContext';
import { useTheme } from '../../contexts/ThemeContext';

const COLORS = ['#2563eb', '#0f766e', '#f59e0b', '#ef4444', '#6366f1', '#0891b2'];

interface GroupRemainingPieChartProps {
  data: DashboardGroupPointDto[];
}

export function GroupRemainingPieChart({ data }: GroupRemainingPieChartProps) {
  const { hidden } = usePrivacy();
  const { theme } = useTheme();

  const isDark = theme === 'dark';
  const labelColor = isDark ? '#f1f5f9' : '#111827';
  const tooltipStyle = {
    borderRadius: 8,
    border: `1px solid ${isDark ? '#2d3f57' : '#e5e7eb'}`,
    backgroundColor: isDark ? '#1e293b' : '#ffffff',
    color: labelColor,
    fontSize: '0.85rem',
  };
  const legendStyle = {
    color: labelColor,
    fontSize: '0.8rem',
  };

  if (data.length === 0) {
    return (
      <section className="chart-card">
        <h3>Distribuição de saldo por grupo</h3>
        <p className="chart-empty">Nenhum grupo com saldo disponível neste mês.</p>
      </section>
    );
  }

  return (
    <section className="chart-card">
      <h3>Distribuição de saldo por grupo</h3>
      <div className="chart-container">
        <ResponsiveContainer width="100%" height={300}>
          <PieChart>
            <Pie
              data={data}
              dataKey="remaining"
              nameKey="groupName"
              innerRadius={58}
              outerRadius={110}
              paddingAngle={2}
              label={!hidden ? { fill: labelColor, fontSize: 12 } : false}
              stroke="none"
            >
              {data.map((entry, index) => (
                <Cell key={entry.groupName} fill={COLORS[index % COLORS.length]} />
              ))}
            </Pie>
            <Tooltip
              formatter={(value: number) => [hidden ? '***' : formatCurrency(value), 'Saldo disponível']}
              contentStyle={tooltipStyle}
              itemStyle={{ color: labelColor }}
              labelStyle={{ color: labelColor }}
            />
            <Legend wrapperStyle={{ paddingTop: 8, ...legendStyle }} />
          </PieChart>
        </ResponsiveContainer>
      </div>
    </section>
  );
}
