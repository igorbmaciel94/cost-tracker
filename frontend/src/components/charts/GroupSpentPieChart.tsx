import { Cell, Legend, Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts';
import type { DashboardGroupPointDto } from '../../api/types';
import { formatCurrency } from '../../utils/format';

const COLORS = ['#0f766e', '#0369a1', '#9333ea', '#ca8a04', '#dc2626'];

interface GroupSpentPieChartProps {
  data: DashboardGroupPointDto[];
}

export function GroupSpentPieChart({ data }: GroupSpentPieChartProps) {
  return (
    <section className="chart-card">
      <h3>Distribuição de gasto por grupo</h3>
      <div className="chart-container">
        <ResponsiveContainer width="100%" height={300}>
          <PieChart>
            <Pie data={data} dataKey="spent" nameKey="groupName" outerRadius={110} label>
              {data.map((entry, index) => (
                <Cell key={entry.groupName} fill={COLORS[index % COLORS.length]} />
              ))}
            </Pie>
            <Tooltip formatter={(value: number) => formatCurrency(value)} />
            <Legend />
          </PieChart>
        </ResponsiveContainer>
      </div>
    </section>
  );
}
