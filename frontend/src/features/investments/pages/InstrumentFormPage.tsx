import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { investmentErrorMessage, investmentsApi } from '../api';
import { InstrumentForm } from '../components/InstrumentForm';
import { StatePanel } from '../components/StatePanel';
import { investmentQueryKeys } from '../queryKeys';
import type { CreateInstrumentRequest, UpdateInstrumentRequest } from '../types';

export function InstrumentFormPage() {
  const { instrumentId } = useParams();
  const editing = Boolean(instrumentId);
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const [submitError, setSubmitError] = useState<string | null>(null);
  const instrumentsQuery = useQuery({
    queryKey: investmentQueryKeys.instruments(),
    queryFn: investmentsApi.getInstruments,
    enabled: editing
  });
  const instrument = instrumentsQuery.data?.find((item) => item.instrumentId === instrumentId) ?? null;

  const createMutation = useMutation({ mutationFn: investmentsApi.createInstrument });
  const updateMutation = useMutation({
    mutationFn: ({ id, request }: { id: string; request: UpdateInstrumentRequest }) => investmentsApi.updateInstrument(id, request)
  });

  if (editing && instrumentsQuery.isLoading) {
    return <StatePanel title="A carregar o ativo…" />;
  }

  if (editing && (instrumentsQuery.isError || !instrument)) {
    return (
      <StatePanel title="Ativo não encontrado" tone="danger" action={<Link className="investment-secondary-link" to="/investimentos">Voltar à carteira</Link>}>
        <p>Não foi possível localizar esta posição ativa.</p>
      </StatePanel>
    );
  }

  return (
    <section className="investment-panel investment-form-page">
      <header className="investment-panel-header">
        <div>
          <h2>{editing ? 'Editar ativo' : 'Cadastrar ativo'}</h2>
          <p>{editing ? 'A classe não pode ser trocada depois que existe histórico.' : 'A forma muda conforme o ativo é negociado em mercado ou avaliado manualmente.'}</p>
        </div>
        <Link to={editing && instrumentId ? `/investimentos/ativos/${instrumentId}` : '/investimentos'}>Cancelar</Link>
      </header>

      <InstrumentForm
        instrument={instrument}
        disabled={createMutation.isPending || updateMutation.isPending}
        onSubmit={async (request) => {
          setSubmitError(null);
          try {
            const result = editing && instrumentId
              ? await updateMutation.mutateAsync({
                  id: instrumentId,
                  request: { ...(request as UpdateInstrumentRequest), expectedVersion: instrument?.version }
                })
              : await createMutation.mutateAsync(request as CreateInstrumentRequest);
            await queryClient.invalidateQueries({ queryKey: investmentQueryKeys.all });
            const createdId = result.instrumentId ?? (result as typeof result & { id?: string }).id;
            navigate(createdId ? `/investimentos/ativos/${createdId}` : '/investimentos');
          } catch (error) {
            setSubmitError(investmentErrorMessage(error, 'Não foi possível guardar o ativo.'));
          }
        }}
      />
      {submitError && <p className="investment-alert" data-tone="danger" role="alert">{submitError}</p>}
    </section>
  );
}
