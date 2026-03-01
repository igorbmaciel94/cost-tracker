import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../api/client';
import { BudgetTable } from '../components/BudgetTable';
import type { CreateCategoryRequest, UpdateCategoryRequest } from '../api/types';

interface OrcamentoPageProps {
  monthId: string | null;
  readOnly: boolean;
}

export function OrcamentoPage({ monthId, readOnly }: OrcamentoPageProps) {
  const queryClient = useQueryClient();

  const budgetQuery = useQuery({
    queryKey: ['budget', monthId],
    queryFn: () => api.getBudget(monthId as string),
    enabled: Boolean(monthId)
  });

  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey: ['months'] });
    await queryClient.invalidateQueries({ queryKey: ['budget', monthId] });
    await queryClient.invalidateQueries({ queryKey: ['dashboard', monthId] });
    await queryClient.invalidateQueries({ queryKey: ['targets', monthId] });
  };

  const updateSalary = useMutation({
    mutationFn: (salary: number) => api.updateSalary(monthId as string, { salary }),
    onSuccess: invalidate
  });

  const createCategory = useMutation({
    mutationFn: (payload: CreateCategoryRequest) => api.createCategory(monthId as string, payload),
    onSuccess: invalidate
  });

  const updateCategory = useMutation({
    mutationFn: ({ categoryId, payload }: { categoryId: string; payload: UpdateCategoryRequest }) =>
      api.updateCategory(monthId as string, categoryId, payload),
    onSuccess: invalidate
  });

  const deleteCategory = useMutation({
    mutationFn: (categoryId: string) => api.deleteCategory(monthId as string, categoryId),
    onSuccess: invalidate
  });

  if (!monthId) {
    return <p>Selecione um mês para gerenciar orçamento.</p>;
  }

  if (budgetQuery.isLoading) {
    return <p>Carregando orçamento...</p>;
  }

  if (budgetQuery.isError || !budgetQuery.data) {
    return <p>Falha ao carregar orçamento.</p>;
  }

  return (
    <div className="page-stack">
      <BudgetTable
        budget={budgetQuery.data}
        readOnly={readOnly}
        onUpdateSalary={async (salary) => {
          await updateSalary.mutateAsync(salary);
        }}
        onCreateCategory={async (payload) => {
          await createCategory.mutateAsync(payload);
        }}
        onUpdateCategory={async (categoryId, payload) => {
          await updateCategory.mutateAsync({ categoryId, payload });
        }}
        onDeleteCategory={async (categoryId) => {
          await deleteCategory.mutateAsync(categoryId);
        }}
      />
    </div>
  );
}
