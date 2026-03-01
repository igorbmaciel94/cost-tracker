import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../api/client';
import { EntriesTable } from '../components/EntriesTable';
import type { CreateEntryRequest, UpdateEntryRequest } from '../api/types';

interface LancamentosPageProps {
  monthId: string | null;
  readOnly: boolean;
}

export function LancamentosPage({ monthId, readOnly }: LancamentosPageProps) {
  const queryClient = useQueryClient();

  const entriesQuery = useQuery({
    queryKey: ['entries', monthId],
    queryFn: () => api.getEntries(monthId as string),
    enabled: Boolean(monthId)
  });

  const budgetQuery = useQuery({
    queryKey: ['budget', monthId],
    queryFn: () => api.getBudget(monthId as string),
    enabled: Boolean(monthId)
  });

  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey: ['entries', monthId] });
    await queryClient.invalidateQueries({ queryKey: ['budget', monthId] });
    await queryClient.invalidateQueries({ queryKey: ['dashboard', monthId] });
    await queryClient.invalidateQueries({ queryKey: ['months'] });
    await queryClient.invalidateQueries({ queryKey: ['targets', monthId] });
  };

  const createEntry = useMutation({
    mutationFn: (payload: CreateEntryRequest) => api.createEntry(monthId as string, payload),
    onSuccess: invalidate
  });

  const updateEntry = useMutation({
    mutationFn: ({ entryId, payload }: { entryId: string; payload: UpdateEntryRequest }) =>
      api.updateEntry(monthId as string, entryId, payload),
    onSuccess: invalidate
  });

  const deleteEntry = useMutation({
    mutationFn: (entryId: string) => api.deleteEntry(monthId as string, entryId),
    onSuccess: invalidate
  });

  if (!monthId) {
    return <p>Selecione um mês para gerenciar lançamentos.</p>;
  }

  if (entriesQuery.isLoading || budgetQuery.isLoading) {
    return <p>Carregando lançamentos...</p>;
  }

  if (entriesQuery.isError || budgetQuery.isError || !entriesQuery.data || !budgetQuery.data) {
    return <p>Falha ao carregar lançamentos.</p>;
  }

  return (
    <EntriesTable
      entries={entriesQuery.data}
      categories={budgetQuery.data.lines}
      readOnly={readOnly}
      onCreateEntry={async (payload) => {
        await createEntry.mutateAsync(payload);
      }}
      onUpdateEntry={async (entryId, payload) => {
        await updateEntry.mutateAsync({ entryId, payload });
      }}
      onDeleteEntry={async (entryId) => {
        await deleteEntry.mutateAsync(entryId);
      }}
    />
  );
}
