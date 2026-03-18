import { lazy, Suspense, useEffect, useMemo, useState } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './api/client';
import type { AuthSessionDto, LoginRequest } from './api/types';
import { Layout } from './components/Layout';
import { LoginPage } from './pages/LoginPage';
import { PrivacyProvider } from './contexts/PrivacyContext';

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

interface AuthenticatedAppProps {
  session: AuthSessionDto;
  onLogout: () => Promise<void>;
  loggingOut: boolean;
}

function AuthenticatedApp({ session, onLogout, loggingOut }: AuthenticatedAppProps) {
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
    <PrivacyProvider>
      <Layout
        months={months}
        selectedMonthId={selectedMonthId}
        selectedMonth={selectedMonth}
        onSelectMonth={setSelectedMonthId}
        onCreateMonth={() => {
          createMonthMutation.mutate();
        }}
        creatingMonth={createMonthMutation.isPending}
        username={session.username}
        onLogout={onLogout}
        loggingOut={loggingOut}
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
    </PrivacyProvider>
  );
}

export default function App() {
  const queryClient = useQueryClient();

  const sessionQuery = useQuery({
    queryKey: ['auth', 'session'],
    queryFn: api.getSession,
    retry: false
  });

  const loginMutation = useMutation({
    mutationFn: (request: LoginRequest) => api.login(request),
    onSuccess: async (session) => {
      queryClient.setQueryData(['auth', 'session'], session);
      await queryClient.invalidateQueries();
    }
  });

  const logoutMutation = useMutation({
    mutationFn: api.logout,
    onSuccess: (session) => {
      queryClient.clear();
      queryClient.setQueryData(['auth', 'session'], session);
    }
  });

  if (sessionQuery.isLoading) {
    return <p className="center-message">Validando sessão...</p>;
  }

  if (sessionQuery.isError || !sessionQuery.data) {
    return <p className="center-message">Falha ao validar a sessão.</p>;
  }

  const session = sessionQuery.data;

  return (
    <Routes>
      {session.isAuthenticated ? (
        <>
          <Route path="/login" element={<Navigate to="/" replace />} />
          <Route
            path="/*"
            element={
              <AuthenticatedApp
                session={session}
                onLogout={async () => {
                  await logoutMutation.mutateAsync();
                }}
                loggingOut={logoutMutation.isPending}
              />
            }
          />
        </>
      ) : (
        <>
          <Route
            path="/login"
            element={
              <LoginPage
                onLogin={async (request) => {
                  await loginMutation.mutateAsync(request);
                }}
                loading={loginMutation.isPending}
                errorMessage={loginMutation.error instanceof Error ? loginMutation.error.message : null}
              />
            }
          />
          <Route path="*" element={<Navigate to="/login" replace />} />
        </>
      )}
    </Routes>
  );
}
