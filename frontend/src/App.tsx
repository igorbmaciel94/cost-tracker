import { lazy, Suspense, useEffect, useMemo, useState } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './api/client';
import { Layout } from './components/Layout';

const DashboardPage = lazy(() =>
  import('./pages/DashboardPage').then((module) => ({ default: module.DashboardPage }))
);

const OrcamentoPage = lazy(() =>
  import('./pages/OrcamentoPage').then((module) => ({ default: module.OrcamentoPage }))
);

const LancamentosPage = lazy(() =>
  import('./pages/LancamentosPage').then((module) => ({ default: module.LancamentosPage }))
);

const MetasPage = lazy(() =>
  import('./pages/MetasPage').then((module) => ({ default: module.MetasPage }))
);

const HistoricoPage = lazy(() =>
  import('./pages/HistoricoPage').then((module) => ({ default: module.HistoricoPage }))
);

export default function App() {
  const queryClient = useQueryClient();
  const [selectedMonthId, setSelectedMonthId] = useState<string | null>(null);

  const monthsQuery = useQuery({
    queryKey: ['months'],
    queryFn: api.getMonths
  });

  const createMonthMutation = useMutation({
    mutationFn: () => api.createNewMonth(),
    onSuccess: async (newMonth) => {
      setSelectedMonthId(newMonth.id);
      await queryClient.invalidateQueries({ queryKey: ['months'] });
      await queryClient.invalidateQueries();
    }
  });

  const months = monthsQuery.data ?? [];

  useEffect(() => {
    if (months.length === 0) {
      return;
    }

    const selectedExists = selectedMonthId && months.some((month) => month.id === selectedMonthId);
    if (selectedExists) {
      return;
    }

    const openMonth = months.find((month) => month.status === 'OPEN');
    setSelectedMonthId(openMonth?.id ?? months[0].id);
  }, [months, selectedMonthId]);

  const selectedMonth = useMemo(
    () => months.find((month) => month.id === selectedMonthId) ?? null,
    [months, selectedMonthId]
  );

  if (monthsQuery.isLoading) {
    return <p className="center-message">Carregando meses...</p>;
  }

  if (monthsQuery.isError) {
    return <p className="center-message">Falha ao carregar os meses.</p>;
  }

  return (
    <Layout
      months={months}
      selectedMonthId={selectedMonthId}
      selectedMonth={selectedMonth}
      onSelectMonth={setSelectedMonthId}
      onCreateMonth={() => {
        createMonthMutation.mutate();
      }}
      creatingMonth={createMonthMutation.isPending}
    >
      <Suspense fallback={<p className="center-message">Carregando página...</p>}>
        <Routes>
          <Route path="/" element={<DashboardPage monthId={selectedMonthId} />} />
          <Route
            path="/orcamento"
            element={
              <OrcamentoPage monthId={selectedMonthId} readOnly={selectedMonth?.status === 'CLOSED'} />
            }
          />
          <Route
            path="/lancamentos"
            element={
              <LancamentosPage monthId={selectedMonthId} readOnly={selectedMonth?.status === 'CLOSED'} />
            }
          />
          <Route
            path="/metas"
            element={<MetasPage monthId={selectedMonthId} readOnly={selectedMonth?.status === 'CLOSED'} />}
          />
          <Route path="/historico" element={<HistoricoPage months={months} />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </Suspense>
    </Layout>
  );
}
