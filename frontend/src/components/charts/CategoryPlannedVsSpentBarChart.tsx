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
            <CartesianGrid strokeDasharray="4 4" />
            <XAxis type="number" tickFormatter={(value) => formatCurrency(value)} />
            <YAxis type="category" dataKey="category" width={140} tick={{ fontSize: 12 }} />
            <Tooltip formatter={(value: number) => formatCurrency(value)} />
            <Legend />
            <Bar dataKey="planned" fill="#1d4ed8" name="Previsto" barSize={12} />
            <Bar dataKey="spent" fill="#ea580c" name="Gasto" barSize={12} />
          </BarChart>
        </ResponsiveContainer>
      </div>
    </section>
  );
}
