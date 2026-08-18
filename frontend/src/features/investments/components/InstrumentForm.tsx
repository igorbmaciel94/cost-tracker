import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { ASSET_CLASS_META, CURRENCIES, INVESTABLE_ASSET_CLASSES, defaultKindFor, isMarketAssetClass } from '../constants';
import { instrumentFormSchema, type InstrumentFormValues } from '../schemas';
import type { CreateInstrumentRequest, InstrumentPositionDto, UpdateInstrumentRequest } from '../types';
import { createIdempotencyKey, todayIso } from '../utils';

interface InstrumentFormProps {
  instrument?: InstrumentPositionDto | null;
  disabled?: boolean;
  onSubmit: (request: CreateInstrumentRequest | UpdateInstrumentRequest) => Promise<void> | void;
}

export function InstrumentForm({ instrument, disabled, onSubmit }: InstrumentFormProps) {
  const {
    register,
    watch,
    setValue,
    handleSubmit,
    formState: { errors, isSubmitting }
  } = useForm<InstrumentFormValues>({
    resolver: zodResolver(instrumentFormSchema),
    defaultValues: {
      assetClass: instrument?.assetClass ?? 'STOCKS',
      kind: instrument?.kind ?? 'STOCK',
      name: instrument?.name ?? '',
      ticker: instrument?.ticker ?? '',
      mic: instrument?.mic ?? '',
      isin: instrument?.isin ?? '',
      identityConfirmed: Boolean(instrument),
      nativeCurrency: instrument?.nativeCurrency ?? '',
      allocationScore: instrument?.allocationScore ?? 1,
      quantityStep: instrument?.quantityStep ?? 0.000001,
      openingQuantity: '',
      openingUnitCost: '',
      openingBalance: '',
      asOf: todayIso()
    }
  });

  const assetClass = watch('assetClass');
  const isMarket = isMarketAssetClass(assetClass);

  useEffect(() => {
    if (!instrument) setValue('kind', defaultKindFor(assetClass));
  }, [assetClass, instrument, setValue]);

  return (
    <form
      className="investment-form"
      onSubmit={handleSubmit(async (values) => {
        const market = isMarketAssetClass(values.assetClass);
        const openingQuantity = values.openingQuantity === '' ? undefined : Number(values.openingQuantity);
        const openingUnitCost = values.openingUnitCost === '' ? undefined : Number(values.openingUnitCost);
        const openingBalance = values.openingBalance === '' ? undefined : Number(values.openingBalance);
        const base = {
          assetClass: values.assetClass,
          kind: values.kind,
          name: values.name.trim(),
          ticker: values.ticker.trim() || undefined,
          mic: values.mic.trim() || undefined,
          isin: values.isin.trim() || undefined,
          nativeCurrency: values.nativeCurrency.toUpperCase(),
          valuationMode: market ? 'MARKET_QUOTE' as const : 'MANUAL' as const,
          allocationScore: market ? Number(values.allocationScore) : 0,
          quantityStep: market ? Number(values.quantityStep) : undefined
        };

        if (instrument) {
          await onSubmit(base);
          return;
        }

        await onSubmit({
          ...base,
          openingTransaction: market && openingQuantity !== undefined && openingQuantity > 0
            ? {
                transactionType: 'OPENING_BALANCE',
                transactionDate: values.asOf,
                quantity: openingQuantity,
                unitPrice: openingUnitCost,
                currency: values.nativeCurrency.toUpperCase(),
                idempotencyKey: createIdempotencyKey('opening-position')
              }
            : undefined,
          manualValuation: !market && openingBalance !== undefined
            ? {
                amount: openingBalance,
                currency: values.nativeCurrency.toUpperCase(),
                asOf: values.asOf,
                idempotencyKey: createIdempotencyKey('manual-valuation')
              }
            : undefined
        });
      })}
    >
      <div className="investment-form-grid">
        <label>
          <span>Classe</span>
          <select {...register('assetClass')} disabled={Boolean(instrument) || disabled || isSubmitting}>
            {INVESTABLE_ASSET_CLASSES.map((value) => <option key={value} value={value}>{ASSET_CLASS_META[value].label}</option>)}
          </select>
          {errors.assetClass?.message && <small className="field-error">{errors.assetClass.message}</small>}
        </label>

        <label>
          <span>Tipo do instrumento</span>
          <select {...register('kind')} disabled={disabled || isSubmitting}>
            {isMarket ? (
              <>
                {assetClass === 'STOCKS' && <option value="STOCK">Ação</option>}
                {assetClass === 'STOCKS' && <option value="ETF">ETF</option>}
                {assetClass === 'STOCKS' && <option value="ADR">ADR</option>}
                {assetClass === 'REITS' && <option value="REIT">REIT</option>}
              </>
            ) : (
              <>
                <option value="BOND">Título / aplicação</option>
                <option value="ACCOUNT">Conta remunerada</option>
              </>
            )}
          </select>
        </label>

        <label className="investment-field-wide">
          <span>Nome</span>
          <input {...register('name')} placeholder={isMarket ? 'Ex.: Coca-Cola' : 'Ex.: CDB Banco X'} disabled={disabled || isSubmitting} />
          {errors.name?.message && <small className="field-error">{errors.name.message}</small>}
        </label>

        {isMarket && (
          <>
            <label>
              <span>Ticker</span>
              <input {...register('ticker')} autoCapitalize="characters" placeholder="Ex.: KO" disabled={disabled || isSubmitting} />
              {errors.ticker?.message && <small className="field-error">{errors.ticker.message}</small>}
            </label>
            <label>
              <span>Bolsa / MIC</span>
              <input {...register('mic')} autoCapitalize="characters" placeholder="Ex.: XNYS ou XLON" disabled={disabled || isSubmitting} />
              {errors.mic?.message && <small className="field-error">{errors.mic.message}</small>}
            </label>
            <label>
              <span>ISIN (opcional)</span>
              <input {...register('isin')} autoCapitalize="characters" placeholder="Identificador internacional" disabled={disabled || isSubmitting} />
            </label>
          </>
        )}

        <label>
          <span>Moeda nativa</span>
          <select {...register('nativeCurrency')} disabled={disabled || isSubmitting}>
            <option value="">Selecione e confirme</option>
            {CURRENCIES.map(({ code, label }) => <option key={code} value={code}>{label}</option>)}
          </select>
          {errors.nativeCurrency?.message && <small className="field-error">{errors.nativeCurrency.message}</small>}
        </label>

        {isMarket && (
          <>
            <label>
              <span>Nota de alocação (0–100)</span>
              <input type="number" min={0} max={100} step={1} {...register('allocationScore')} disabled={disabled || isSubmitting} />
              <small>Nota 0 não participa dos próximos aportes.</small>
            </label>
            <label>
              <span>Fração mínima</span>
              <input type="number" min={0.000001} max={1} step={0.000001} {...register('quantityStep')} disabled={disabled || isSubmitting} />
            </label>
          </>
        )}
      </div>

      {isMarket && (
        <label className="identity-confirmation">
          <input type="checkbox" {...register('identityConfirmed')} disabled={disabled || isSubmitting} />
          <span>
            Confirmei o ativo, a bolsa e a moeda. Tickers iguais podem identificar ativos diferentes e a moeda não é inferida automaticamente.
          </span>
          {errors.identityConfirmed?.message && <small className="field-error">{errors.identityConfirmed.message}</small>}
        </label>
      )}

      {!instrument && (
        <section className="opening-position">
          <header>
            <h3>{isMarket ? 'Posição inicial' : 'Saldo atual'}</h3>
            <p>{isMarket ? 'Opcional. Cadastre o que já possui sem transformar a cotação atual em custo histórico.' : 'Informe o valor líquido/resgatável na data de referência.'}</p>
          </header>
          <div className="investment-form-grid">
            {isMarket ? (
              <>
                <label>
                  <span>Quantidade fracionária</span>
                  <input type="number" min={0} step="any" inputMode="decimal" {...register('openingQuantity')} />
                </label>
                <label>
                  <span>Custo unitário (opcional)</span>
                  <input type="text" autoCapitalize="none" autoCorrect="off" spellCheck={false} placeholder="0.00" {...register('openingUnitCost')} />
                  {errors.openingUnitCost?.message && <small className="field-error">{errors.openingUnitCost.message}</small>}
                </label>
              </>
            ) : (
              <label>
                <span>Saldo resgatável</span>
                <input type="number" min={0} step="0.01" inputMode="decimal" {...register('openingBalance')} />
                {errors.openingBalance?.message && <small className="field-error">{errors.openingBalance.message}</small>}
              </label>
            )}
            <label>
              <span>Data de referência</span>
              <input type="date" max={todayIso()} {...register('asOf')} />
              {errors.asOf?.message && <small className="field-error">{errors.asOf.message}</small>}
            </label>
          </div>
        </section>
      )}

      <div className="investment-form-actions">
        <button type="submit" disabled={disabled || isSubmitting}>{isSubmitting ? 'A guardar…' : instrument ? 'Guardar alterações' : 'Cadastrar ativo'}</button>
      </div>
    </form>
  );
}
