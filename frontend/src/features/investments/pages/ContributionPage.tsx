import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { useFieldArray, useForm } from 'react-hook-form';
import { Link } from 'react-router-dom';
import { z } from 'zod';
import { investmentErrorMessage, investmentsApi } from '../api';
import { ContributionPreview } from '../components/ContributionPreview';
import { InvestmentMoney } from '../components/InvestmentMoney';
import { StatePanel } from '../components/StatePanel';
import { contributionAmountSchema, decimalTextSchema, type ContributionAmountFormValues } from '../schemas';
import { investmentQueryKeys } from '../queryKeys';
import type { ContributionPlanDto } from '../types';
import { createIdempotencyKey, todayIso } from '../utils';

const executionSchema = z.object({
  lines: z.array(z.object({
    planLineId: z.string(),
    instrumentId: z.string(),
    occurredOn: z.string().min(1),
    actualAmountEur: z.coerce.number().nonnegative(),
    actualNativeAmount: z.union([z.literal(''), z.coerce.number().nonnegative()]),
    actualQuantity: z.union([z.literal(''), z.coerce.number().nonnegative()]),
    actualUnitPrice: decimalTextSchema({ allowEmpty: true, allowZero: true }),
    fees: z.union([z.literal(''), z.coerce.number().nonnegative()]),
    currency: z.string()
  }))
}).superRefine(({ lines }, context) => {
  lines.forEach((line, index) => {
    if (Number(line.actualAmountEur) > 0 && !line.instrumentId) {
      context.addIssue({ code: z.ZodIssueCode.custom, path: ['lines', index, 'instrumentId'], message: 'Escolha o ativo que receberá este valor.' });
    }
  });
});
type ExecutionForm = z.input<typeof executionSchema>;

export function ContributionPage() {
  const queryClient = useQueryClient();
  const [plan, setPlan] = useState<ContributionPlanDto | null>(null);
  const [allowStale, setAllowStale] = useState(false);
  const amountForm = useForm<ContributionAmountFormValues>({
    resolver: zodResolver(contributionAmountSchema),
    defaultValues: { amount: 0 }
  });
  const executionForm = useForm<ExecutionForm>({
    resolver: zodResolver(executionSchema),
    defaultValues: { lines: [] }
  });
  const executionFields = useFieldArray({ control: executionForm.control, name: 'lines' });

  const marketStatusQuery = useQuery({
    queryKey: investmentQueryKeys.marketDataStatus(),
    queryFn: investmentsApi.getMarketDataStatus,
    retry: false
  });
  const planHistoryQuery = useQuery({
    queryKey: investmentQueryKeys.contributionPlans(),
    queryFn: investmentsApi.getContributionPlans,
    retry: false
  });
  const instrumentsQuery = useQuery({
    queryKey: investmentQueryKeys.instruments(),
    queryFn: investmentsApi.getInstruments,
    retry: false
  });

  const createMutation = useMutation({
    mutationFn: investmentsApi.createContributionPlan,
    onSuccess: (created) => {
      setPlan(created);
      queryClient.setQueryData(investmentQueryKeys.contributionPlan(created.id), created);
    }
  });
  const confirmMutation = useMutation({
    mutationFn: ({ planId, values }: { planId: string; values: ExecutionForm }) => investmentsApi.confirmContributionPlan(planId, {
      idempotencyKey: createIdempotencyKey('contribution-confirmation'),
      executions: values.lines.map((line) => {
        const selectedInstrument = instrumentsQuery.data?.find((instrument) => instrument.instrumentId === line.instrumentId);
        return {
        planLineId: line.planLineId,
        instrumentId: line.instrumentId || undefined,
        occurredOn: line.occurredOn,
        actualAmountEur: Number(line.actualAmountEur),
        actualNativeAmount: line.actualNativeAmount === '' ? undefined : Number(line.actualNativeAmount),
        actualQuantity: line.actualQuantity === '' ? undefined : Number(line.actualQuantity),
        actualUnitPrice: line.actualUnitPrice === '' ? undefined : Number(line.actualUnitPrice),
        fees: line.fees === '' ? undefined : Number(line.fees),
        currency: selectedInstrument?.nativeCurrency ?? (line.currency || undefined)
        };
      })
    }),
    onSuccess: async (confirmed) => {
      setPlan(confirmed);
      await queryClient.invalidateQueries({ queryKey: investmentQueryKeys.all });
    }
  });

  useEffect(() => {
    if (!plan) {
      executionForm.reset({ lines: [] });
      return;
    }
    executionForm.reset({
      lines: plan.lines.map((line) => ({
        planLineId: line.id,
        instrumentId: line.instrumentId ?? '',
        occurredOn: todayIso(),
        actualAmountEur: line.recommendedAmountEur,
        actualNativeAmount: line.recommendedNativeAmount ?? '',
        actualQuantity: line.suggestedQuantity ?? '',
        actualUnitPrice: line.unitPrice == null ? '' : String(line.unitPrice),
        fees: '',
        currency: line.nativeCurrency ?? 'EUR'
      }))
    });
  }, [executionForm, plan]);

  const marketStatus = marketStatusQuery.data;
  const dataBlocked = marketStatus?.freshness === 'BLOCKED' || marketStatus?.freshness === 'MISSING';
  const requiresOverride = marketStatus?.freshness === 'STALE';
  const planExpired = plan?.status === 'EXPIRED' || (plan?.expiresAt ? new Date(plan.expiresAt).getTime() <= Date.now() : false);

  return (
    <div className="investment-page-stack">
      <section className="investment-panel contribution-start">
        <header className="investment-panel-header">
          <div><h2>Novo aporte</h2><p>Informe quanto está disponível em EUR. Gerar o preview não altera o patrimônio.</p></div>
        </header>
        {marketStatus && marketStatus.freshness !== 'FRESH' && (
          <p className="investment-alert" data-tone={dataBlocked ? 'danger' : 'warning'}>
            {marketStatus.message || (dataBlocked ? 'Há dados ausentes ou expirados; o backend pode bloquear o plano.' : 'Alguns dados estão desatualizados.')}
          </p>
        )}
        <form className="contribution-amount-form" onSubmit={amountForm.handleSubmit(async ({ amount }) => {
          const created = await createMutation.mutateAsync({ contributionAmountEur: Number(amount), allowStaleData: allowStale });
          setPlan(created);
        })}>
          <label><span>Valor disponível</span><div className="currency-input"><span>€</span><input type="number" min={0.01} step="0.01" inputMode="decimal" {...amountForm.register('amount')} /></div>{amountForm.formState.errors.amount?.message && <small className="field-error">{amountForm.formState.errors.amount.message}</small>}</label>
          {requiresOverride && <label className="stale-override"><input type="checkbox" checked={allowStale} onChange={(event) => setAllowStale(event.target.checked)} /><span>Estou ciente de que o cálculo usará dados desatualizados e ficará auditado.</span></label>}
          <button type="submit" disabled={createMutation.isPending || dataBlocked || (requiresOverride && !allowStale)}>{createMutation.isPending ? 'A calcular…' : 'Gerar preview'}</button>
        </form>
        {createMutation.isError && <p className="investment-alert" data-tone="danger" role="alert">{investmentErrorMessage(createMutation.error, 'Não foi possível gerar o preview.')}</p>}
      </section>

      {plan && <ContributionPreview plan={plan} />}

      {plan?.status === 'DRAFT' && (
        <section className="investment-panel contribution-confirmation">
          <header className="investment-panel-header"><div><h2>Registrar o que foi realizado</h2><p>Revise preço, quantidade e taxas reais. Somente esta confirmação cria movimentações.</p></div></header>
          {planExpired && <p className="investment-alert" data-tone="danger" role="alert">Este preview expirou. Gere um novo cálculo antes de registrar.</p>}
          <form onSubmit={executionForm.handleSubmit((values) => confirmMutation.mutate({ planId: plan.id, values }))}>
            <div className="execution-list">
              {executionFields.fields.map((field, index) => {
                const line = plan.lines[index];
                return (
                  <fieldset key={field.id}>
                    <legend>{line?.ticker || line?.instrumentName || `Destino ${index + 1}`}</legend>
                    <label><span>Data</span><input type="date" max={todayIso()} {...executionForm.register(`lines.${index}.occurredOn`)} /></label>
                    {!line?.instrumentId && (
                      <label>
                        <span>Destino</span>
                        <select {...executionForm.register(`lines.${index}.instrumentId`)}>
                          <option value="">Escolha o ativo</option>
                          {(instrumentsQuery.data ?? []).filter((instrument) => instrument.assetClass === line?.assetClass).map((instrument) => (
                            <option key={instrument.instrumentId} value={instrument.instrumentId}>{instrument.ticker || instrument.name}</option>
                          ))}
                        </select>
                        {executionForm.formState.errors.lines?.[index]?.instrumentId?.message && <small className="field-error">{executionForm.formState.errors.lines[index]?.instrumentId?.message}</small>}
                      </label>
                    )}
                    <label><span>Valor EUR</span><input type="number" min={0} step="0.01" {...executionForm.register(`lines.${index}.actualAmountEur`)} /></label>
                    {line && line.assetClass !== 'STOCKS' && line.assetClass !== 'REITS' && (
                      <label><span>Valor na moeda do destino</span><input type="number" min={0} step="any" {...executionForm.register(`lines.${index}.actualNativeAmount`)} /></label>
                    )}
                    <label><span>Quantidade</span><input type="number" min={0} step="any" {...executionForm.register(`lines.${index}.actualQuantity`)} /></label>
                    <label><span>Preço unitário</span><input type="text" autoCapitalize="none" autoCorrect="off" spellCheck={false} placeholder="0.00" {...executionForm.register(`lines.${index}.actualUnitPrice`)} />{executionForm.formState.errors.lines?.[index]?.actualUnitPrice?.message && <small className="field-error">{executionForm.formState.errors.lines[index]?.actualUnitPrice?.message}</small>}</label>
                    <label><span>Taxas</span><input type="number" min={0} step="0.01" {...executionForm.register(`lines.${index}.fees`)} /></label>
                    <input type="hidden" {...executionForm.register(`lines.${index}.planLineId`)} />
                    {line?.instrumentId && <input type="hidden" {...executionForm.register(`lines.${index}.instrumentId`)} />}
                    <input type="hidden" {...executionForm.register(`lines.${index}.currency`)} />
                  </fieldset>
                );
              })}
            </div>
            <div className="confirmation-total"><span>Total do preview</span><strong><InvestmentMoney value={plan.totalSuggestedEur} /></strong></div>
            <button type="submit" disabled={confirmMutation.isPending || planExpired || executionFields.fields.length === 0}>{confirmMutation.isPending ? 'A registrar…' : 'Registrar aporte'}</button>
          </form>
          {confirmMutation.isError && <p className="investment-alert" data-tone="danger" role="alert">{investmentErrorMessage(confirmMutation.error, 'O aporte não foi registrado.')}</p>}
        </section>
      )}

      {plan?.status === 'CONFIRMED' && (
        <StatePanel title="Aporte registrado" action={<Link className="investment-primary-link" to="/investimentos">Ver carteira atualizada</Link>}>
          <p>As movimentações confirmadas foram adicionadas ao histórico. O preview original permanece auditável.</p>
        </StatePanel>
      )}

      {planHistoryQuery.isSuccess && planHistoryQuery.data.length > 0 && (
        <section className="investment-panel contribution-history">
          <header className="investment-panel-header"><div><h2>Planos anteriores</h2><p>Previews e confirmações preservados para consulta.</p></div></header>
          <ul>
            {planHistoryQuery.data.slice(0, 8).map((historyPlan) => (
              <li key={historyPlan.id}><div><strong><InvestmentMoney value={historyPlan.contributionAmountEur} /></strong><small>{new Date(historyPlan.createdAt).toLocaleDateString('pt-PT')}</small></div><span className="plan-status" data-status={historyPlan.status}>{historyPlan.status}</span></li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}
