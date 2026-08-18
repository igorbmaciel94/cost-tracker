import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { z } from 'zod';
import { investmentErrorMessage, investmentsApi } from '../api';
import { InvestmentMoney } from '../components/InvestmentMoney';
import { StatePanel } from '../components/StatePanel';
import { ValuationBreakdown } from '../components/ValuationBreakdown';
import { ASSET_CLASS_META, isMarketAssetClass } from '../constants';
import { investmentQueryKeys } from '../queryKeys';
import type { CurrencyCode, TransactionType } from '../types';
import { createIdempotencyKey, todayIso } from '../utils';
import { formatDateIsoToPt } from '../../../utils/format';
import { PrivacyMask } from '../../../contexts/PrivacyContext';
import { ConfirmModal } from '../../../components/ConfirmModal';
import { DividendEventsPanel } from '../components/DividendEventsPanel';

const transactionSchema = z.object({
  type: z.enum(['BUY', 'SELL', 'DEPOSIT', 'WITHDRAWAL', 'ADJUSTMENT']),
  occurredOn: z.string().min(1),
  quantity: z.union([z.literal(''), z.coerce.number().refine((value) => value !== 0, 'A quantidade não pode ser zero.')]),
  unitPrice: z.union([z.literal(''), z.coerce.number().positive()]),
  amount: z.union([z.literal(''), z.coerce.number().positive()]),
  fees: z.union([z.literal(''), z.coerce.number().nonnegative()]),
  exchangeRate: z.union([z.literal(''), z.coerce.number().positive()]),
  currency: z.string().length(3)
}).superRefine((values, context) => {
  if (values.quantity === '') {
    context.addIssue({ code: z.ZodIssueCode.custom, path: ['quantity'], message: 'Informe a quantidade.' });
  }
  if ((values.type === 'BUY' || values.type === 'SELL') && values.quantity !== '' && Number(values.quantity) <= 0) {
    context.addIssue({ code: z.ZodIssueCode.custom, path: ['quantity'], message: 'Compras e vendas exigem quantidade positiva.' });
  }
  if ((values.type === 'BUY' || values.type === 'SELL') && values.unitPrice === '') {
    context.addIssue({ code: z.ZodIssueCode.custom, path: ['unitPrice'], message: 'Informe o preço unitário.' });
  }
});

const valuationSchema = z.object({
  amount: z.coerce.number().nonnegative('Informe um saldo válido.'),
  currency: z.string().length(3),
  asOf: z.string().min(1)
});

const quoteSchema = z.object({
  price: z.coerce.number().positive('Informe uma cotação positiva.'),
  currency: z.string().length(3),
  asOf: z.string().min(1),
  providerSymbol: z.string().max(128, 'O símbolo não pode exceder 128 caracteres.')
});

type TransactionForm = z.input<typeof transactionSchema>;
type ValuationForm = z.input<typeof valuationSchema>;
type QuoteForm = z.input<typeof quoteSchema>;

export function InstrumentDetailPage() {
  const { instrumentId = '' } = useParams();
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const [archiveConfirmationOpen, setArchiveConfirmationOpen] = useState(false);
  const [quotePollingDeadline] = useState(() => Date.now() + 20_000);
  const instrumentsQuery = useQuery({
    queryKey: investmentQueryKeys.instruments(),
    queryFn: investmentsApi.getInstruments,
    refetchInterval: (query) => {
      const current = query.state.data?.find((item) => item.instrumentId === instrumentId);
      return Date.now() < quotePollingDeadline &&
        current?.valuationMode === 'MARKET_QUOTE' &&
        !current.marketData
        ? 1_000
        : false;
    }
  });
  const transactionsQuery = useQuery({
    queryKey: investmentQueryKeys.transactions(instrumentId),
    queryFn: () => investmentsApi.getTransactions(instrumentId),
    enabled: Boolean(instrumentId)
  });
  const instrument = instrumentsQuery.data?.find((item) => item.instrumentId === instrumentId);
  const manual = instrument ? !isMarketAssetClass(instrument.assetClass) : false;
  const valuationsQuery = useQuery({
    queryKey: [...investmentQueryKeys.instrument(instrumentId), 'manual-valuations'],
    queryFn: () => investmentsApi.getManualValuations(instrumentId),
    enabled: Boolean(instrumentId && manual)
  });

  const transactionForm = useForm<TransactionForm>({
    resolver: zodResolver(transactionSchema),
    defaultValues: { type: 'BUY', occurredOn: todayIso(), quantity: '', unitPrice: '', amount: '', fees: '', exchangeRate: '', currency: '' }
  });
  const valuationForm = useForm<ValuationForm>({
    resolver: zodResolver(valuationSchema),
    defaultValues: { amount: 0, currency: '', asOf: todayIso() }
  });
  const quoteForm = useForm<QuoteForm>({
    resolver: zodResolver(quoteSchema),
    defaultValues: { price: 0, currency: '', asOf: todayIso(), providerSymbol: '' }
  });
  const selectedTransactionType = transactionForm.watch('type');

  const transactionMutation = useMutation({
    mutationFn: (values: TransactionForm) => investmentsApi.createTransaction(instrumentId, {
      transactionType: values.type as TransactionType,
      transactionDate: values.occurredOn,
      quantity: values.quantity === '' ? undefined : Number(values.quantity),
      unitPrice: values.unitPrice === '' ? undefined : Number(values.unitPrice),
      amount: values.amount === '' ? undefined : Number(values.amount),
      feeAmount: values.fees === '' ? undefined : Number(values.fees),
      currencyPerEurRate: values.exchangeRate === '' ? undefined : Number(values.exchangeRate),
      currency: values.currency as CurrencyCode,
      idempotencyKey: createIdempotencyKey('investment-transaction')
    }),
    onSuccess: async () => {
      transactionForm.reset({ ...transactionForm.getValues(), quantity: '', unitPrice: '', amount: '', fees: '', exchangeRate: '' });
      await queryClient.invalidateQueries({ queryKey: investmentQueryKeys.all });
    }
  });
  const valuationMutation = useMutation({
    mutationFn: (values: ValuationForm) => investmentsApi.createManualValuation(instrumentId, {
      amount: Number(values.amount), currency: values.currency as CurrencyCode, asOf: values.asOf,
      idempotencyKey: createIdempotencyKey('manual-valuation')
    }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: investmentQueryKeys.all });
    }
  });
  const quoteMutation = useMutation({
    mutationFn: (values: QuoteForm) => investmentsApi.recordManualQuote(instrumentId, {
      price: Number(values.price),
      currency: values.currency as CurrencyCode,
      asOf: values.asOf,
      providerSymbol: values.providerSymbol || instrument?.ticker || undefined,
      mic: instrument?.mic || undefined
    }),
    onSuccess: async () => {
      quoteForm.setValue('price', 0);
      await queryClient.invalidateQueries({ queryKey: investmentQueryKeys.all });
    }
  });
  const archiveMutation = useMutation({
    mutationFn: () => investmentsApi.archiveInstrument(instrumentId, instrument?.version),
    onSuccess: async () => {
      setArchiveConfirmationOpen(false);
      await queryClient.invalidateQueries({ queryKey: investmentQueryKeys.all });
      navigate('/investimentos');
    }
  });

  if (instrumentsQuery.isLoading) return <StatePanel title="A carregar o histórico…" />;
  if (instrumentsQuery.isError || !instrument) {
    return <StatePanel title="Ativo não encontrado" tone="danger" action={<Link className="investment-secondary-link" to="/investimentos">Voltar</Link>} />;
  }

  if (!transactionForm.getValues('currency')) transactionForm.setValue('currency', instrument.nativeCurrency);
  if (!valuationForm.getValues('currency')) valuationForm.setValue('currency', instrument.nativeCurrency);
  if (!quoteForm.getValues('currency')) quoteForm.setValue('currency', instrument.nativeCurrency);
  if (!quoteForm.getValues('providerSymbol') && instrument.ticker) quoteForm.setValue('providerSymbol', instrument.ticker);

  return (
    <div className="investment-page-stack">
      <section className="investment-panel instrument-detail-hero">
        <header className="investment-panel-header">
          <div>
            <span className="asset-class-label">{ASSET_CLASS_META[instrument.assetClass].label}</span>
            <h2>{instrument.ticker ? `${instrument.ticker} · ${instrument.name}` : instrument.name}</h2>
            <p>{instrument.nativeCurrency}{instrument.mic ? ` · ${instrument.mic}` : ''} · {instrument.valuationMode === 'MARKET_QUOTE' ? 'último fechamento disponível' : 'avaliação manual'}</p>
          </div>
          <div className="investment-header-actions">
            <Link className="investment-secondary-link" to={`/investimentos/ativos/${instrumentId}/editar`}>Editar</Link>
            <button className="button-danger" type="button" disabled={archiveMutation.isPending} onClick={() => setArchiveConfirmationOpen(true)}>Arquivar</button>
          </div>
        </header>
        <dl className="instrument-detail-kpis">
          <div><dt>Valor atual</dt><dd><InvestmentMoney value={instrument.valueEur} /></dd></div>
          <div><dt>Valor nativo</dt><dd><InvestmentMoney value={instrument.nativeValue} currency={instrument.nativeCurrency} /></dd></div>
          <div><dt>{manual ? 'Saldo informado' : 'Quantidade'}</dt><dd>{manual ? <InvestmentMoney value={instrument.manualBalance} currency={instrument.nativeCurrency} /> : <PrivacyMask value={instrument.quantity == null ? '—' : String(instrument.quantity)} />}</dd></div>
          <div><dt>Custo médio</dt><dd><InvestmentMoney value={instrument.averageCost} currency={instrument.nativeCurrency} /></dd></div>
          <div><dt>Aportes líquidos</dt><dd><InvestmentMoney value={instrument.contributedEur} /></dd></div>
          <div><dt>Nota</dt><dd>{instrument.allocationScore}</dd></div>
        </dl>
      </section>

      <section className="investment-panel valuation-audit-panel">
        <header className="investment-panel-header">
          <div><h2>Como este valor foi calculado</h2><p>Cotação, câmbio, fontes e datas usados no valor final da posição.</p></div>
        </header>
        <ValuationBreakdown position={instrument} />
      </section>

      {!manual && <DividendEventsPanel instrumentId={instrumentId} nativeCurrency={instrument.nativeCurrency} />}

      <div className="instrument-detail-grid">
        <section className="investment-panel">
          <header className="investment-panel-header"><div><h2>{manual ? 'Atualizar saldo' : 'Registrar movimentação'}</h2><p>O histórico é acrescido; valores anteriores não são sobrescritos.</p></div></header>
          {manual ? (
            <form className="compact-investment-form" onSubmit={valuationForm.handleSubmit((values) => valuationMutation.mutate(values))}>
              <label><span>Novo saldo resgatável</span><input type="number" min={0} step="0.01" {...valuationForm.register('amount')} /></label>
              <label><span>Moeda</span><select {...valuationForm.register('currency')}><option value={instrument.nativeCurrency}>{instrument.nativeCurrency}</option></select></label>
              <label><span>Data</span><input type="date" max={todayIso()} {...valuationForm.register('asOf')} /></label>
              <button type="submit" disabled={valuationMutation.isPending}>{valuationMutation.isPending ? 'A guardar…' : 'Guardar avaliação'}</button>
            </form>
          ) : (
            <>
              <form className="compact-investment-form" onSubmit={transactionForm.handleSubmit((values) => transactionMutation.mutate(values))}>
                <label><span>Tipo</span><select {...transactionForm.register('type')}><option value="BUY">Compra</option><option value="SELL">Venda</option><option value="ADJUSTMENT">Ajuste</option></select></label>
                <label><span>Data</span><input type="date" max={todayIso()} {...transactionForm.register('occurredOn')} /></label>
                <label><span>Quantidade</span><input type="number" min={selectedTransactionType === 'ADJUSTMENT' ? undefined : 0} step="any" {...transactionForm.register('quantity')} />{transactionForm.formState.errors.quantity?.message && <small className="field-error">{transactionForm.formState.errors.quantity.message}</small>}</label>
                <label><span>Preço unitário</span><input type="number" min={0} step="any" {...transactionForm.register('unitPrice')} />{transactionForm.formState.errors.unitPrice?.message && <small className="field-error">{transactionForm.formState.errors.unitPrice.message}</small>}</label>
                <label><span>Taxas</span><input type="number" min={0} step="0.01" {...transactionForm.register('fees')} /></label>
                <label><span>Moeda</span><select {...transactionForm.register('currency')}><option value={instrument.nativeCurrency}>{instrument.nativeCurrency}</option></select></label>
                {instrument.nativeCurrency !== 'EUR' && <label><span>{instrument.nativeCurrency} por EUR na data (opcional)</span><input type="number" min={0} step="any" {...transactionForm.register('exchangeRate')} /></label>}
                <button type="submit" disabled={transactionMutation.isPending}>{transactionMutation.isPending ? 'A guardar…' : 'Registrar movimentação'}</button>
              </form>
              <details className="manual-quote-fallback">
                <summary>Informar cotação manual de fallback</summary>
                <p>Use apenas quando os provedores públicos não devolverem este ativo. O valor ficará identificado como manual.</p>
                <form className="compact-investment-form" onSubmit={quoteForm.handleSubmit((values) => quoteMutation.mutate(values))}>
                  <label><span>Preço atual</span><input type="number" min={0} step="any" {...quoteForm.register('price')} />{quoteForm.formState.errors.price?.message && <small className="field-error">{quoteForm.formState.errors.price.message}</small>}</label>
                  <label><span>Moeda</span><select {...quoteForm.register('currency')}><option value={instrument.nativeCurrency}>{instrument.nativeCurrency}</option></select></label>
                  <label><span>Data da cotação</span><input type="date" max={todayIso()} {...quoteForm.register('asOf')} /></label>
                  <label><span>Símbolo do provedor</span><input type="text" {...quoteForm.register('providerSymbol')} /></label>
                  <button type="submit" disabled={quoteMutation.isPending}>{quoteMutation.isPending ? 'A guardar…' : 'Guardar cotação manual'}</button>
                </form>
              </details>
            </>
          )}
          {(transactionMutation.isError || valuationMutation.isError || quoteMutation.isError || archiveMutation.isError) && <p className="investment-alert" data-tone="danger" role="alert">{investmentErrorMessage(transactionMutation.error ?? valuationMutation.error ?? quoteMutation.error ?? archiveMutation.error)}</p>}
        </section>

        <section className="investment-panel">
          <header className="investment-panel-header"><div><h2>Histórico</h2><p>Movimentações e snapshots manuais em ordem decrescente.</p></div></header>
          {(transactionsQuery.isLoading || valuationsQuery.isLoading) && <p>A carregar histórico…</p>}
          {(transactionsQuery.isError || valuationsQuery.isError) && <p className="investment-alert" data-tone="danger">Parte do histórico não pôde ser carregada.</p>}
          <ol className="investment-timeline">
            {(transactionsQuery.data ?? []).map((transaction) => (
              <li key={transaction.id}>
                <span className="timeline-dot" />
                <div><strong>{transaction.type.replaceAll('_', ' ')}</strong><small>{formatDateIsoToPt(transaction.occurredOn)}</small></div>
                <div>{transaction.quantity != null && <span><PrivacyMask value={`${transaction.quantity} un.`} /></span>}<InvestmentMoney value={transaction.amount ?? (transaction.unitPrice != null && transaction.quantity != null ? transaction.unitPrice * transaction.quantity : null)} currency={transaction.currency} /></div>
              </li>
            ))}
            {(valuationsQuery.data ?? []).map((valuation) => (
              <li key={valuation.id}>
                <span className="timeline-dot" />
                <div><strong>Avaliação manual</strong><small>{formatDateIsoToPt(valuation.asOf)}</small></div>
                <InvestmentMoney value={valuation.amount} currency={valuation.currency} />
              </li>
            ))}
          </ol>
          {(transactionsQuery.data?.length ?? 0) === 0 && (valuationsQuery.data?.length ?? 0) === 0 && !transactionsQuery.isLoading && <p className="investment-empty-copy">Ainda não há eventos registrados.</p>}
        </section>
      </div>
      <ConfirmModal
        open={archiveConfirmationOpen}
        title="Arquivar ativo?"
        description="O ativo deixará de aparecer na carteira, mas todo o histórico será preservado."
        confirmLabel={archiveMutation.isPending ? 'A arquivar…' : 'Arquivar'}
        onConfirm={() => {
          if (!archiveMutation.isPending) archiveMutation.mutate();
        }}
        onCancel={() => {
          if (!archiveMutation.isPending) setArchiveConfirmationOpen(false);
        }}
      />
    </div>
  );
}
