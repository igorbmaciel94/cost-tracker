import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { ConfirmModal } from '../../../components/ConfirmModal';
import { PrivacyMask } from '../../../contexts/PrivacyContext';
import { formatDateIsoToPt } from '../../../utils/format';
import { investmentErrorMessage, investmentsApi } from '../api';
import { investmentQueryKeys } from '../queryKeys';
import type { CurrencyCode, DividendEventDto } from '../types';
import { createIdempotencyKey } from '../utils';
import { InvestmentMoney } from './InvestmentMoney';

const dividendSchema = z.object({
  grossAmountPerUnit: z.coerce.number().positive('Informe um valor positivo.'),
  withholdingTaxPercent: z.coerce.number().min(0).lt(100, 'O imposto deve ser inferior a 100%.'),
  currency: z.string().length(3),
  exDate: z.string().min(1, 'Informe a data ex.'),
  paymentDate: z.string().min(1, 'Informe a data de pagamento.'),
  notes: z.string().max(512, 'A observação não pode exceder 512 caracteres.')
}).superRefine((values, context) => {
  if (values.exDate && values.paymentDate && values.paymentDate < values.exDate) {
    context.addIssue({ code: z.ZodIssueCode.custom, path: ['paymentDate'], message: 'O pagamento não pode ser anterior à data ex.' });
  }
});

type DividendForm = z.input<typeof dividendSchema>;

const statusLabel: Record<DividendEventDto['status'], string> = {
  SCHEDULED: 'Agendado',
  DUE: 'Aguardando crédito',
  CREDITED: 'Creditado',
  NO_ENTITLEMENT: 'Sem direito'
};

export function DividendEventsPanel({ instrumentId, nativeCurrency }: { instrumentId: string; nativeCurrency: CurrencyCode }) {
  const queryClient = useQueryClient();
  const [deleteCandidate, setDeleteCandidate] = useState<DividendEventDto | null>(null);
  const eventsQuery = useQuery({
    queryKey: investmentQueryKeys.dividends(instrumentId),
    queryFn: () => investmentsApi.getDividendEvents(instrumentId),
    enabled: Boolean(instrumentId)
  });
  const form = useForm<DividendForm>({
    resolver: zodResolver(dividendSchema),
    defaultValues: {
      grossAmountPerUnit: 0,
      withholdingTaxPercent: 0,
      currency: nativeCurrency,
      exDate: '',
      paymentDate: '',
      notes: ''
    }
  });
  const exDate = form.watch('exDate');
  const currencies = Array.from(new Set([nativeCurrency, 'EUR', 'USD', 'BRL', 'GBP']));

  const createMutation = useMutation({
    mutationFn: (values: DividendForm) => investmentsApi.createDividendEvent(instrumentId, {
      grossAmountPerUnit: Number(values.grossAmountPerUnit),
      withholdingTaxPercent: Number(values.withholdingTaxPercent),
      currency: values.currency as CurrencyCode,
      exDate: values.exDate,
      paymentDate: values.paymentDate,
      notes: values.notes || undefined,
      idempotencyKey: createIdempotencyKey('dividend-event')
    }),
    onSuccess: async () => {
      form.reset({ grossAmountPerUnit: 0, withholdingTaxPercent: 0, currency: nativeCurrency, exDate: '', paymentDate: '', notes: '' });
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: investmentQueryKeys.dividends(instrumentId) }),
        queryClient.invalidateQueries({ queryKey: investmentQueryKeys.dividendCash() })
      ]);
    }
  });
  const deleteMutation = useMutation({
    mutationFn: (eventId: string) => investmentsApi.deleteDividendEvent(eventId),
    onSuccess: async () => {
      setDeleteCandidate(null);
      await queryClient.invalidateQueries({ queryKey: investmentQueryKeys.dividends(instrumentId) });
    }
  });

  return (
    <section className="investment-panel dividend-events-panel">
      <header className="investment-panel-header">
        <div><h2>Dividendos</h2><p>Cadastre quando a empresa divulgar a data ex e a data de pagamento.</p></div>
      </header>
      <details className="dividend-registration" open={(eventsQuery.data?.length ?? 0) === 0}>
        <summary>Cadastrar dividendo</summary>
        <p>A quantidade com direito será a que você possuía antes da data ex. O valor líquido entra no caixa às 06:00 do dia do pagamento.</p>
        <form className="compact-investment-form" onSubmit={form.handleSubmit((values) => createMutation.mutate(values))}>
          <label><span>Valor bruto por ação/cota</span><input type="number" min={0} step="any" {...form.register('grossAmountPerUnit')} />{form.formState.errors.grossAmountPerUnit?.message && <small className="field-error">{form.formState.errors.grossAmountPerUnit.message}</small>}</label>
          <label><span>Moeda</span><select {...form.register('currency')}>{currencies.map((currency) => <option key={currency} value={currency}>{currency}</option>)}</select></label>
          <label><span>Data ex</span><input type="date" {...form.register('exDate')} />{form.formState.errors.exDate?.message && <small className="field-error">{form.formState.errors.exDate.message}</small>}</label>
          <label><span>Data de pagamento</span><input type="date" min={exDate || undefined} {...form.register('paymentDate')} />{form.formState.errors.paymentDate?.message && <small className="field-error">{form.formState.errors.paymentDate.message}</small>}</label>
          <label><span>Imposto retido (%)</span><input type="number" min={0} max={99.9999} step="any" {...form.register('withholdingTaxPercent')} /></label>
          <label><span>Observação (opcional)</span><input type="text" maxLength={512} {...form.register('notes')} /></label>
          <button type="submit" disabled={createMutation.isPending}>{createMutation.isPending ? 'A cadastrar…' : 'Cadastrar dividendo'}</button>
        </form>
      </details>
      {createMutation.isError && <p className="investment-alert" data-tone="danger" role="alert">{investmentErrorMessage(createMutation.error)}</p>}
      {eventsQuery.isLoading && <p className="investment-empty-copy">A carregar dividendos…</p>}
      {eventsQuery.isError && <p className="investment-alert" data-tone="danger">Não foi possível carregar os dividendos.</p>}
      {eventsQuery.data?.length === 0 && <p className="investment-empty-copy">Nenhum dividendo cadastrado para este ativo.</p>}
      {(eventsQuery.data?.length ?? 0) > 0 && (
        <div className="dividend-event-list">
          {eventsQuery.data?.map((event) => (
            <article key={event.id}>
              <header>
                <div><strong><InvestmentMoney value={event.grossAmountPerUnit} currency={event.currency} /> por unidade</strong><small>Ex: {formatDateIsoToPt(event.exDate)} · pagamento: {formatDateIsoToPt(event.paymentDate)}</small></div>
                <span className="plan-status" data-status={event.status}>{statusLabel[event.status]}</span>
              </header>
              {event.processedAt && (
                <dl>
                  <div><dt>Quantidade com direito</dt><dd><PrivacyMask value={String(event.eligibleQuantity ?? 0)} /></dd></div>
                  <div><dt>Bruto</dt><dd><InvestmentMoney value={event.grossAmount} currency={event.currency} /></dd></div>
                  <div><dt>Imposto</dt><dd><InvestmentMoney value={event.withholdingTaxAmount} currency={event.currency} /></dd></div>
                  <div><dt>Líquido no caixa</dt><dd><InvestmentMoney value={event.netAmount} currency={event.currency} /></dd></div>
                </dl>
              )}
              {event.notes && <p>{event.notes}</p>}
              {event.canDelete && <button type="button" className="investment-text-button" onClick={() => setDeleteCandidate(event)}>Excluir agendamento</button>}
            </article>
          ))}
        </div>
      )}
      {(deleteMutation.isError) && <p className="investment-alert" data-tone="danger">{investmentErrorMessage(deleteMutation.error)}</p>}
      <ConfirmModal
        open={Boolean(deleteCandidate)}
        title="Excluir dividendo agendado?"
        description="O evento ainda não foi creditado e será removido. Esta ação não altera as movimentações do ativo."
        confirmLabel={deleteMutation.isPending ? 'A excluir…' : 'Excluir'}
        onConfirm={() => deleteCandidate && !deleteMutation.isPending && deleteMutation.mutate(deleteCandidate.id)}
        onCancel={() => !deleteMutation.isPending && setDeleteCandidate(null)}
      />
    </section>
  );
}
