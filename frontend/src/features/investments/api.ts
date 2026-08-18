import type {
  ConfirmContributionPlanRequest,
  ContributionPlanDto,
  CreateDividendEventRequest,
  CreateContributionPlanRequest,
  CreateInstrumentRequest,
  CreateManualValuationRequest,
  CreateTransactionRequest,
  InvestmentTransactionDto,
  DividendCashSummaryDto,
  DividendEventDto,
  InstrumentPositionDto,
  ManualValuationDto,
  ManualMarketQuoteRequest,
  MarketDataStatusDto,
  PortfolioDto,
  UpdateAllocationRequest,
  UpdateInstrumentRequest
} from './types';

type UnknownRecord = Record<string, unknown>;

function asRecord(value: unknown): UnknownRecord {
  return typeof value === 'object' && value !== null ? value as UnknownRecord : {};
}

function asNumber(value: unknown): number | undefined {
  return typeof value === 'number' && Number.isFinite(value) ? value : undefined;
}

function normalizeMarketData(value: UnknownRecord): InstrumentPositionDto['marketData'] {
  if (Object.keys(value).length === 0) return null;
  return {
    asOf: value.asOf == null ? null : String(value.asOf),
    fetchedAt: value.fetchedAt == null ? null : String(value.fetchedAt),
    source: value.source == null ? null : String(value.source),
    freshness: String(value.freshness ?? 'MISSING') as NonNullable<InstrumentPositionDto['marketData']>['freshness'],
    isFallback: Boolean(value.isFallback),
    price: asNumber(value.price) ?? 0,
    currency: String(value.currency ?? 'EUR'),
    priceKind: String(value.priceKind ?? 'LATEST_AVAILABLE'),
    providerSymbol: String(value.providerSymbol ?? ''),
    exchange: value.exchange == null ? null : String(value.exchange),
    mic: value.mic == null ? null : String(value.mic)
  };
}

function normalizeFxData(value: UnknownRecord): InstrumentPositionDto['fxData'] {
  if (Object.keys(value).length === 0) return null;
  return {
    asOf: value.asOf == null ? null : String(value.asOf),
    fetchedAt: value.fetchedAt == null ? null : String(value.fetchedAt),
    source: value.source == null ? null : String(value.source),
    freshness: String(value.freshness ?? 'MISSING') as NonNullable<InstrumentPositionDto['fxData']>['freshness'],
    isFallback: Boolean(value.isFallback),
    rate: asNumber(value.rate) ?? 0,
    baseCurrency: String(value.baseCurrency ?? 'EUR'),
    quoteCurrency: String(value.quoteCurrency ?? 'EUR'),
    rateKind: String(value.rateKind ?? 'LATEST_AVAILABLE')
  };
}

function normalizeInstrument(value: unknown): InstrumentPositionDto {
  const root = asRecord(value);
  const source = asRecord(root.instrument ?? root);
  const position = asRecord(source.position);
  const latestManual = asRecord(source.latestManualValuation);
  const marketData = asRecord(source.marketData);
  const fxData = asRecord(source.fxData);
  const currentManual = asNumber(position.currentManualValueNative) ?? asNumber(latestManual.amount);
  return {
    instrumentId: String(source.instrumentId ?? source.id ?? ''),
    version: source.version as number | string | undefined,
    name: String(source.name ?? source.ticker ?? 'Ativo'),
    ticker: source.ticker == null ? null : String(source.ticker),
    mic: source.mic == null ? null : String(source.mic),
    isin: source.isin == null ? null : String(source.isin),
    assetClass: String(source.assetClass ?? 'STOCKS') as InstrumentPositionDto['assetClass'],
    kind: String(source.kind ?? 'STOCK') as InstrumentPositionDto['kind'],
    valuationMode: String(source.valuationMode ?? 'MARKET_QUOTE') as InstrumentPositionDto['valuationMode'],
    nativeCurrency: String(source.nativeCurrency ?? 'EUR'),
    allocationScore: asNumber(source.allocationScore) ?? 0,
    quantity: asNumber(source.quantity) ?? asNumber(source.currentQuantity) ?? asNumber(position.quantity) ?? null,
    manualBalance: asNumber(source.manualBalance) ?? currentManual ?? null,
    currentPrice: asNumber(source.currentPrice) ?? asNumber(marketData.price) ?? null,
    averageCost: asNumber(source.averageCost) ?? asNumber(position.averageCostNative) ?? null,
    knownCostEur: asNumber(source.knownCostEur) ?? (position.isCostKnown === true ? asNumber(position.netInvestedEur) ?? null : null),
    contributedEur: asNumber(source.contributedEur) ?? asNumber(position.netInvestedEur) ?? null,
    nativeValue: asNumber(source.nativeValue) ?? currentManual ?? null,
    valueEur: asNumber(source.valueEur) ?? null,
    gainLossEur: asNumber(source.gainLossEur) ?? null,
    portfolioWeight: asNumber(source.portfolioWeight) ?? null,
    classWeight: asNumber(source.classWeight) ?? null,
    quantityStep: asNumber(source.quantityStep) ?? 0.000001,
    archived: Boolean(source.archived ?? source.isArchived),
    freshness: source.freshness == null ? undefined : String(source.freshness) as InstrumentPositionDto['freshness'],
    marketData: normalizeMarketData(marketData),
    fxData: normalizeFxData(fxData),
    lastValuationAsOf: source.lastValuationAsOf == null
      ? position.currentManualValueAsOf == null ? latestManual.asOf == null ? null : String(latestManual.asOf) : String(position.currentManualValueAsOf)
      : String(source.lastValuationAsOf)
  };
}

function normalizePortfolio(value: unknown): PortfolioDto {
  const source = asRecord(value);
  const rawTargets = (source.targets ?? source.allocationTargets ?? []) as unknown[];
  const rawPositions = (source.positions ?? []) as unknown[];
  const summary = asRecord(source.summary);
  return {
    id: String(source.id ?? ''),
    baseCurrency: String(source.baseCurrency ?? 'EUR'),
    version: source.version as number | string ?? 0,
    configured: Boolean(source.configured ?? source.isConfigured),
    targets: rawTargets.map((item) => {
      const target = asRecord(item);
      return {
        assetClass: String(target.assetClass) as PortfolioDto['targets'][number]['assetClass'],
        weight: asNumber(target.weight) ?? 0,
        currentWeight: asNumber(target.currentWeight),
        currentValueEur: asNumber(target.currentValueEur)
      };
    }),
    summary: {
      totalValueEur: asNumber(summary.totalValueEur) ?? asNumber(source.totalValueEur) ?? 0,
      knownCostEur: asNumber(summary.knownCostEur) ?? null,
      gainLossEur: asNumber(summary.gainLossEur) ?? null,
      asOf: summary.asOf == null ? null : String(summary.asOf),
      freshness: String(summary.freshness ?? 'MISSING') as PortfolioDto['summary']['freshness'],
      isPartial: Boolean(summary.isPartial)
    },
    positions: rawPositions.map(normalizeInstrument)
  };
}

function normalizeTransaction(value: unknown): InvestmentTransactionDto {
  const source = asRecord(value);
  return {
    id: String(source.id ?? ''),
    instrumentId: String(source.instrumentId ?? ''),
    type: String(source.type ?? source.transactionType ?? 'ADJUSTMENT') as InvestmentTransactionDto['type'],
    occurredOn: String(source.occurredOn ?? source.transactionDate ?? source.date ?? ''),
    quantity: asNumber(source.quantity) ?? null,
    unitPrice: asNumber(source.unitPrice) ?? null,
    amount: asNumber(source.amount) ?? null,
    currency: String(source.currency ?? 'EUR'),
    fees: asNumber(source.fees) ?? asNumber(source.feeAmount) ?? null,
    exchangeRateToEur: asNumber(source.exchangeRateToEur) ?? asNumber(source.currencyPerEurRate) ?? null,
    contributedEur: asNumber(source.contributedEur) ?? null,
    createdAt: source.createdAt == null ? undefined : String(source.createdAt)
  };
}

function normalizeManualValuation(value: unknown): ManualValuationDto {
  const source = asRecord(value);
  return {
    id: String(source.id ?? ''),
    instrumentId: String(source.instrumentId ?? ''),
    amount: asNumber(source.amount) ?? 0,
    currency: String(source.currency ?? 'EUR'),
    asOf: String(source.asOf ?? ''),
    recordedAt: source.recordedAt == null ? undefined : String(source.recordedAt)
  };
}

function normalizeCreatedTransaction(value: unknown): InvestmentTransactionDto {
  const source = asRecord(value);
  const transactions = Array.isArray(source.transactions) ? source.transactions : [];
  return normalizeTransaction(transactions[0] ?? source);
}

function normalizeCreatedValuation(value: unknown): ManualValuationDto {
  const source = asRecord(value);
  const valuations = Array.isArray(source.manualValuations) ? source.manualValuations : [];
  return normalizeManualValuation(valuations[0] ?? source);
}

function normalizeContributionPlan(value: unknown): ContributionPlanDto {
  const source = asRecord(value);
  const classLines = Array.isArray(source.classLines) ? source.classLines : [];
  const instrumentLines = Array.isArray(source.instrumentLines) ? source.instrumentLines : [];
  const instrumentClasses = new Set(instrumentLines.map((item) => String(asRecord(item).assetClass)));
  const rawLines = Array.isArray(source.lines)
    ? source.lines
    : [
        ...instrumentLines,
        ...classLines.filter((item) => !instrumentClasses.has(String(asRecord(item).assetClass)))
      ];
  return {
    id: String(source.id ?? source.planId ?? ''),
    status: String(source.status ?? 'DRAFT') as ContributionPlanDto['status'],
    contributionAmountEur: asNumber(source.contributionAmountEur) ?? asNumber(source.availableAmountEur) ?? 0,
    totalSuggestedEur: asNumber(source.totalSuggestedEur) ?? asNumber(source.totalRecommendedEur) ?? 0,
    residualAmountEur: asNumber(source.residualAmountEur) ?? asNumber(source.residualEur) ?? 0,
    portfolioVersion: source.portfolioVersion as number | string ?? 0,
    strategyVersion: String(source.strategyVersion ?? source.algorithmVersion ?? 'allocation-by-score-v1'),
    createdAt: String(source.createdAt ?? new Date().toISOString()),
    expiresAt: String(source.expiresAt ?? new Date(Date.now() + 30 * 60_000).toISOString()),
    classRecommendations: classLines.map((value) => {
      const line = asRecord(value);
      return {
        assetClass: String(line.assetClass ?? 'STOCKS') as ContributionPlanDto['classRecommendations'][number]['assetClass'],
        recommendedAmountEur: asNumber(line.recommendedAmountEur) ?? asNumber(line.amountEur) ?? 0
      };
    }),
    lines: rawLines.map((value, index) => {
      const line = asRecord(value);
      const quote = asRecord(line.quote);
      const fx = asRecord(line.fx);
      return {
        id: String(line.id ?? line.lineId ?? `${source.id ?? 'plan'}-${index}`),
        assetClass: String(line.assetClass ?? 'STOCKS') as ContributionPlanDto['lines'][number]['assetClass'],
        instrumentId: line.instrumentId == null ? null : String(line.instrumentId),
        instrumentName: line.instrumentName == null ? line.name == null ? null : String(line.name) : String(line.instrumentName),
        ticker: line.ticker == null ? null : String(line.ticker),
        nativeCurrency: line.nativeCurrency == null ? line.currency == null ? null : String(line.currency) : String(line.nativeCurrency),
        currentValueEur: asNumber(line.currentValueEur) ?? 0,
        targetWeight: asNumber(line.targetWeight) ?? 0,
        recommendedAmountEur: asNumber(line.recommendedAmountEur) ?? asNumber(line.amountEur) ?? 0,
        recommendedNativeAmount: asNumber(line.recommendedNativeAmount) ?? asNumber(line.nativeAmount) ?? null,
        suggestedQuantity: asNumber(line.suggestedQuantity) ?? asNumber(line.quantity) ?? null,
        unitPrice: asNumber(line.unitPrice) ?? asNumber(quote.price) ?? null,
        allocationScore: asNumber(line.allocationScore) ?? asNumber(line.score) ?? null,
        explanation: String(line.explanation ?? line.reason ?? 'Distribuição calculada para aproximar a carteira da meta.'),
        quoteAsOf: line.quoteAsOf == null ? quote.asOf == null ? null : String(quote.asOf) : String(line.quoteAsOf),
        fxAsOf: line.fxAsOf == null ? fx.asOf == null ? null : String(fx.asOf) : String(line.fxAsOf),
        freshness: String(line.freshness ?? 'FRESH') as ContributionPlanDto['lines'][number]['freshness']
      };
    })
  };
}

function normalizeMarketDataStatus(value: unknown): MarketDataStatusDto {
  const source = asRecord(value);
  const failures = Array.isArray(source.failures) ? source.failures : [];
  return {
    asOf: source.asOf == null ? null : String(source.asOf),
    lastRefreshAt: source.lastRefreshAt == null ? null : String(source.lastRefreshAt),
    source: source.source == null ? null : String(source.source),
    freshness: String(source.freshness ?? source.status ?? 'MISSING') as MarketDataStatusDto['freshness'],
    message: source.message == null ? null : String(source.message),
    staleInstrumentIds: Array.isArray(source.staleInstrumentIds) ? source.staleInstrumentIds.map(String) : [],
    missingInstrumentIds: Array.isArray(source.missingInstrumentIds) ? source.missingInstrumentIds.map(String) : [],
    failures: failures.map((value) => {
      const failure = asRecord(value);
      return {
        provider: String(failure.provider ?? 'PROVIDER'),
        subject: String(failure.subject ?? ''),
        message: String(failure.message ?? 'Falha sem detalhe.'),
        isTransient: Boolean(failure.isTransient)
      };
    })
  };
}

function normalizeDividendEvent(value: unknown): DividendEventDto {
  const source = asRecord(value);
  return {
    id: String(source.id ?? ''),
    instrumentId: String(source.instrumentId ?? ''),
    instrumentName: String(source.instrumentName ?? 'Ativo'),
    ticker: source.ticker == null ? null : String(source.ticker),
    grossAmountPerUnit: asNumber(source.grossAmountPerUnit) ?? 0,
    withholdingTaxPercent: asNumber(source.withholdingTaxPercent) ?? 0,
    currency: String(source.currency ?? 'EUR'),
    exDate: String(source.exDate ?? ''),
    paymentDate: String(source.paymentDate ?? ''),
    notes: source.notes == null ? null : String(source.notes),
    status: String(source.status ?? 'SCHEDULED') as DividendEventDto['status'],
    eligibleQuantity: asNumber(source.eligibleQuantity) ?? null,
    grossAmount: asNumber(source.grossAmount) ?? null,
    withholdingTaxAmount: asNumber(source.withholdingTaxAmount) ?? null,
    netAmount: asNumber(source.netAmount) ?? null,
    currencyPerEurRate: asNumber(source.currencyPerEurRate) ?? null,
    netAmountEur: asNumber(source.netAmountEur) ?? null,
    fxAsOf: source.fxAsOf == null ? null : String(source.fxAsOf),
    fxSource: source.fxSource == null ? null : String(source.fxSource),
    processedAt: source.processedAt == null ? null : String(source.processedAt),
    createdAt: String(source.createdAt ?? ''),
    canDelete: Boolean(source.canDelete)
  };
}

function normalizeDividendCash(value: unknown): DividendCashSummaryDto {
  const source = asRecord(value);
  const balances = Array.isArray(source.balances) ? source.balances : [];
  return {
    totalEur: asNumber(source.totalEur) ?? null,
    isPartial: Boolean(source.isPartial),
    balances: balances.map((value) => {
      const balance = asRecord(value);
      return {
        currency: String(balance.currency ?? 'EUR'),
        amount: asNumber(balance.amount) ?? 0,
        amountEur: asNumber(balance.amountEur) ?? null,
        lastPaymentDate: balance.lastPaymentDate == null ? null : String(balance.lastPaymentDate),
        fxData: normalizeFxData(asRecord(balance.fxData))
      };
    })
  };
}

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ??
  (import.meta.env.PROD ? '/api' : 'http://localhost:8080/api');

export class InvestmentApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly details?: unknown
  ) {
    super(message);
    this.name = 'InvestmentApiError';
  }
}

async function investmentFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;
  const response = await fetch(`${API_BASE_URL}${normalizedPath}`, {
    ...init,
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      ...(init?.headers ?? {})
    }
  });

  if (!response.ok) {
    const contentType = response.headers.get('content-type') ?? '';
    const details = contentType.includes('application/json')
      ? await response.json().catch(() => null)
      : await response.text();
    const message = typeof details === 'object' && details !== null
      ? String((details as { detail?: string; title?: string }).detail ?? (details as { title?: string }).title ?? `Erro HTTP ${response.status}`)
      : String(details || `Erro HTTP ${response.status}`);
    throw new InvestmentApiError(message, response.status, details);
  }

  if (response.status === 204) return undefined as T;
  return await response.json() as T;
}

export const investmentsApi = {
  getPortfolio: async () => normalizePortfolio(await investmentFetch<unknown>('/investments/portfolio/valuation')),
  updateAllocation: async (request: UpdateAllocationRequest) =>
    normalizePortfolio(await investmentFetch<unknown>('/investments/allocation', {
      method: 'PUT',
      body: JSON.stringify(request)
    })),
  getInstruments: async () => {
    const response = await investmentFetch<unknown>('/investments/portfolio/valuation');
    const root = asRecord(response);
    const instruments = Array.isArray(response)
      ? response
      : Array.isArray(root.positions) ? root.positions : [];
    return instruments.map(normalizeInstrument);
  },
  createInstrument: async (request: CreateInstrumentRequest) =>
    normalizeInstrument(await investmentFetch<unknown>('/investments/instruments', {
      method: 'POST',
      body: JSON.stringify(request)
    })),
  updateInstrument: async (id: string, request: UpdateInstrumentRequest) =>
    normalizeInstrument(await investmentFetch<unknown>(`/investments/instruments/${id}`, {
      method: 'PUT',
      body: JSON.stringify(request)
    })),
  archiveInstrument: (id: string, expectedVersion?: number | string) =>
    investmentFetch<void>(`/investments/instruments/${id}/archive${expectedVersion === undefined ? '' : `?expectedVersion=${encodeURIComponent(String(expectedVersion))}`}`, { method: 'POST' }),
  getTransactions: async (id: string) =>
    (await investmentFetch<unknown[]>(`/investments/instruments/${id}/transactions`)).map(normalizeTransaction),
  createTransaction: async (id: string, request: CreateTransactionRequest) =>
    normalizeCreatedTransaction(await investmentFetch<unknown>(`/investments/instruments/${id}/transactions`, {
      method: 'POST',
      body: JSON.stringify(request)
    })),
  getManualValuations: async (id: string) =>
    (await investmentFetch<unknown[]>(`/investments/instruments/${id}/manual-valuations`)).map(normalizeManualValuation),
  createManualValuation: async (id: string, request: CreateManualValuationRequest) =>
    normalizeCreatedValuation(await investmentFetch<unknown>(`/investments/instruments/${id}/manual-valuations`, {
      method: 'POST',
      body: JSON.stringify(request)
    })),
  recordManualQuote: (id: string, request: ManualMarketQuoteRequest) =>
    investmentFetch<void>(`/investments/market-data/instruments/${id}/manual-quote`, {
      method: 'POST',
      body: JSON.stringify(request)
    }),
  getMarketDataStatus: async () =>
    normalizeMarketDataStatus(await investmentFetch<unknown>('/investments/market-data/status')),
  refreshMarketData: async () =>
    normalizeMarketDataStatus(await investmentFetch<unknown>('/investments/market-data/refresh', { method: 'POST' })),
  getDividendEvents: async (instrumentId?: string) => {
    const query = instrumentId ? `?instrumentId=${encodeURIComponent(instrumentId)}` : '';
    return (await investmentFetch<unknown[]>(`/investments/dividends${query}`)).map(normalizeDividendEvent);
  },
  createDividendEvent: async (instrumentId: string, request: CreateDividendEventRequest) =>
    normalizeDividendEvent(await investmentFetch<unknown>(`/investments/instruments/${instrumentId}/dividends`, {
      method: 'POST',
      body: JSON.stringify(request)
    })),
  deleteDividendEvent: (eventId: string) =>
    investmentFetch<void>(`/investments/dividends/${eventId}`, { method: 'DELETE' }),
  getDividendCash: async () => normalizeDividendCash(await investmentFetch<unknown>('/investments/dividends/cash')),
  createContributionPlan: async (request: CreateContributionPlanRequest) =>
    normalizeContributionPlan(await investmentFetch<unknown>('/investments/contribution-plans', {
      method: 'POST',
      body: JSON.stringify(request)
    })),
  getContributionPlan: async (id: string) =>
    normalizeContributionPlan(await investmentFetch<unknown>(`/investments/contribution-plans/${id}`)),
  getContributionPlans: async () =>
    (await investmentFetch<unknown[]>('/investments/contribution-plans')).map(normalizeContributionPlan),
  confirmContributionPlan: async (id: string, request: ConfirmContributionPlanRequest) =>
    normalizeContributionPlan(await investmentFetch<unknown>(`/investments/contribution-plans/${id}/confirm`, {
      method: 'POST',
      body: JSON.stringify(request)
    }))
};

export function investmentErrorMessage(error: unknown, fallback = 'Não foi possível concluir a operação.') {
  return error instanceof Error && error.message ? error.message : fallback;
}
