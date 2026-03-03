import { Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import type { DashboardCategoryPointDto } from '../../api/types';
import { formatCurrency } from '../../utils/format';

interface CategoryPlannedVsSpentBarChartProps {
  data: DashboardCategoryPointDto[];
}

export function CategoryPlannedVsSpentBarChart({ data }: CategoryPlannedVsSpentBarChartProps) {
  const chartHeight = Math.max(360, data.length * 28);

  return (
    <section className="chart-card">
      <h3>Previsto x Gasto por categoria</h3>
      <div className="chart-container">
        <ResponsiveContainer width="100%" height={chartHeight}>
          <BarChart data={data} layout="vertical" margin={{ top: 8, right: 24, left: 8, bottom: 8 }}>
            <CartesianGrid strokeDasharray="4 4" stroke="#d9e2f4" />
            <XAxis type="number" tickFormatter={(value) => formatCurrency(value)} tick={{ fill: '#465570' }} />
            <YAxis type="category" dataKey="category" width={140} tick={{ fontSize: 12, fill: '#465570' }} />
            <Tooltip
              formatter={(value: number) => formatCurrency(value)}
              contentStyle={{
                borderRadius: 12,
                border: '1px solid #d9e2f4',
                backgroundColor: '#ffffff'
              }}
            />
            <Legend wrapperStyle={{ paddingTop: 8 }} />
            <Bar dataKey="planned" fill="#2463eb" name="Previsto" barSize={12} radius={[8, 8, 8, 8]} />
            <Bar dataKey="spent" fill="#f97316" name="Gasto" barSize={12} radius={[8, 8, 8, 8]} />
          </BarChart>
        </ResponsiveContainer>
      </div>
    </section>
  );
}
