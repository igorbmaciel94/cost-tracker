import { useQuery } from '@tanstack/react-query';
import { api } from '../api/client';
import { CategoryPlannedVsSpentBarChart } from '../components/charts/CategoryPlannedVsSpentBarChart';
import { GroupSpentPieChart } from '../components/charts/GroupSpentPieChart';

interface DashboardPageProps {
  monthId: string | null;
}

export function DashboardPage({ monthId }: DashboardPageProps) {
  const dashboardQuery = useQuery({
    queryKey: ['dashboard', monthId],
    queryFn: () => api.getDashboard(monthId as string),
    enabled: Boolean(monthId)
  });

  if (!monthId) {
    return <p>Selecione um mês para começar.</p>;
  }

  if (dashboardQuery.isLoading) {
    return <p>Carregando dashboard...</p>;
  }

  if (dashboardQuery.isError || !dashboardQuery.data) {
    return <p>Falha ao carregar dashboard.</p>;
  }

  return (
    <div className="page-stack">
      <div className="charts-grid">
        <CategoryPlannedVsSpentBarChart data={dashboardQuery.data.categoryChart} />
        <GroupSpentPieChart data={dashboardQuery.data.groupPie} />
      </div>
    </div>
  );
}
