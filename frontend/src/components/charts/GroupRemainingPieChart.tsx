import { Cell, Legend, Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts';
import type { DashboardGroupPointDto } from '../../api/types';
import { formatCurrency } from '../../utils/format';

const COLORS = ['#2463eb', '#0f766e', '#f59e0b', '#ef4444', '#6366f1', '#0891b2'];

interface GroupRemainingPieChartProps {
  data: DashboardGroupPointDto[];
}

export function GroupRemainingPieChart({ data }: GroupRemainingPieChartProps) {
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
              label
            >
              {data.map((entry, index) => (
                <Cell key={entry.groupName} fill={COLORS[index % COLORS.length]} />
              ))}
            </Pie>
            <Tooltip
              formatter={(value: number) => [formatCurrency(value), 'Saldo disponível']}
              contentStyle={{
                borderRadius: 12,
                border: '1px solid #d9e2f4',
                backgroundColor: '#ffffff'
              }}
            />
            <Legend wrapperStyle={{ paddingTop: 8 }} />
          </PieChart>
        </ResponsiveContainer>
      </div>
    </section>
  );
}
