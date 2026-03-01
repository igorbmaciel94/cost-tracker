import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../api/client';
import { TargetTable } from '../components/TargetTable';
import type { UpdateTargetsRequest } from '../api/types';

interface MetasPageProps {
  monthId: string | null;
  readOnly: boolean;
}

export function MetasPage({ monthId, readOnly }: MetasPageProps) {
  const queryClient = useQueryClient();

  const targetsQuery = useQuery({
    queryKey: ['targets', monthId],
    queryFn: () => api.getTargets(monthId as string),
    enabled: Boolean(monthId)
  });

  const updateTargets = useMutation({
    mutationFn: (payload: UpdateTargetsRequest) => api.updateTargets(monthId as string, payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['targets', monthId] });
      await queryClient.invalidateQueries({ queryKey: ['dashboard', monthId] });
    }
  });

  if (!monthId) {
    return <p>Selecione um mês para gerenciar metas.</p>;
  }

  if (targetsQuery.isLoading) {
    return <p>Carregando metas...</p>;
  }

  if (targetsQuery.isError || !targetsQuery.data) {
    return <p>Falha ao carregar metas.</p>;
  }

  return (
    <TargetTable
      targets={targetsQuery.data}
      readOnly={readOnly}
      onSave={async (payload) => {
        await updateTargets.mutateAsync(payload);
      }}
    />
  );
}
