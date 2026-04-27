import { useMutation, useQuery } from '@tanstack/react-query';
import { api } from '../api/client';
import { CategoryRemainingTreemap } from '../components/charts/CategoryRemainingTreemap';
import { GroupRemainingPieChart } from '../components/charts/GroupRemainingPieChart';

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

  const analysisMutation = useMutation({
    mutationFn: () => api.generateAiAnalysis(monthId as string),
    onSuccess: (blob) => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `analise-${monthId}.pdf`;
      a.click();
      URL.revokeObjectURL(url);
    }
  });

  return (
    <div className="page-stack">
      <div className="section-header">
        <button
          type="button"
          className="btn btn-primary"
          disabled={analysisMutation.isPending}
          onClick={() => analysisMutation.mutate()}
        >
          {analysisMutation.isPending ? 'Gerando análise…' : 'Análise Inteligente'}
        </button>
        {analysisMutation.isError && (
          <p className="inline-error">Falha ao gerar análise.</p>
        )}
      </div>
      <div className="charts-grid">
        <CategoryRemainingTreemap data={dashboardQuery.data.categoryChart} />
        <GroupRemainingPieChart data={dashboardQuery.data.groupPie} />
      </div>
    </div>
  );
}
