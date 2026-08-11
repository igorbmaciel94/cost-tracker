export type AssetClass =
  | 'STOCKS'
  | 'REITS'
  | 'BRAZIL_FIXED_INCOME'
  | 'INTERNATIONAL_FIXED_INCOME';

export type InstrumentKind = 'STOCK' | 'ETF' | 'ADR' | 'REIT' | 'BOND' | 'ACCOUNT';
export type ValuationMode = 'MARKET_QUOTE' | 'MANUAL';
export type CurrencyCode = 'EUR' | 'USD' | 'BRL' | 'GBP' | 'GBX' | (string & {});
export type FreshnessStatus = 'FRESH' | 'STALE' | 'BLOCKED' | 'MISSING';
export type TransactionType =
  | 'OPENING_BALANCE'
  | 'BUY'
  | 'SELL'
  | 'DEPOSIT'
  | 'WITHDRAWAL'
  | 'ADJUSTMENT';
export type ContributionPlanStatus = 'DRAFT' | 'CONFIRMED' | 'EXPIRED' | 'CANCELLED';

export interface MoneyDto {
  amount: number;
  currency: CurrencyCode;
}

export interface AllocationTargetDto {
  assetClass: AssetClass;
  weight: number;
  currentWeight?: number;
  currentValueEur?: number;
}

export interface MarketDataReferenceDto {
  asOf: string | null;
  fetchedAt?: string | null;
  source?: string | null;
  freshness: FreshnessStatus;
  isFallback?: boolean;
}

export interface InstrumentPositionDto {
  instrumentId: string;
  version?: number | string;
  name: string;
  ticker?: string | null;
  mic?: string | null;
  isin?: string | null;
  assetClass: AssetClass;
  kind: InstrumentKind;
  valuationMode: ValuationMode;
  nativeCurrency: CurrencyCode;
  allocationScore: number;
  quantity?: number | null;
  manualBalance?: number | null;
  currentPrice?: number | null;
  averageCost?: number | null;
  knownCostEur?: number | null;
  contributedEur?: number | null;
  nativeValue?: number | null;
  valueEur?: number | null;
  gainLossEur?: number | null;
  portfolioWeight?: number | null;
  classWeight?: number | null;
  quantityStep?: number;
  archived?: boolean;
  freshness?: FreshnessStatus;
  marketData?: MarketDataReferenceDto | null;
  fxData?: MarketDataReferenceDto | null;
  lastValuationAsOf?: string | null;
}

export interface PortfolioSummaryDto {
  totalValueEur: number;
  knownCostEur?: number | null;
  gainLossEur?: number | null;
  asOf?: string | null;
  freshness: FreshnessStatus;
  isPartial?: boolean;
}

export interface PortfolioDto {
  id: string;
  baseCurrency: CurrencyCode;
  version: number | string;
  configured: boolean;
  targets: AllocationTargetDto[];
  summary: PortfolioSummaryDto;
  positions: InstrumentPositionDto[];
}

export interface UpdateAllocationRequest {
  items: Array<{ assetClass: AssetClass; weight: number }>;
  expectedVersion?: number | string;
}

export interface CreateInstrumentRequest {
  assetClass: AssetClass;
  kind: InstrumentKind;
  name: string;
  identifier?: string;
  ticker?: string;
  mic?: string;
  isin?: string;
  nativeCurrency: CurrencyCode;
  valuationMode: ValuationMode;
  allocationScore: number;
  quantityStep?: number;
  openingTransaction?: {
    transactionType: 'OPENING_BALANCE';
    transactionDate: string;
    quantity?: number;
    unitPrice?: number;
    currency?: CurrencyCode;
    feeAmount?: number;
    currencyPerEurRate?: number;
    notes?: string;
    idempotencyKey: string;
  };
  manualValuation?: {
    amount: number;
    currency: CurrencyCode;
    asOf: string;
    idempotencyKey: string;
  };
}

export type UpdateInstrumentRequest = Omit<CreateInstrumentRequest, 'openingTransaction' | 'manualValuation'> & {
  expectedVersion?: number | string;
};

export interface InvestmentTransactionDto {
  id: string;
  instrumentId: string;
  type: TransactionType;
  occurredOn: string;
  quantity?: number | null;
  unitPrice?: number | null;
  amount?: number | null;
  currency: CurrencyCode;
  fees?: number | null;
  exchangeRateToEur?: number | null;
  contributedEur?: number | null;
  createdAt?: string;
}

export interface ManualValuationDto {
  id: string;
  instrumentId: string;
  amount: number;
  currency: CurrencyCode;
  asOf: string;
  recordedAt?: string;
}

export interface InstrumentDetailDto {
  instrument: InstrumentPositionDto;
  transactions: InvestmentTransactionDto[];
  manualValuations: ManualValuationDto[];
}

export interface CreateTransactionRequest {
  transactionType: TransactionType;
  transactionDate: string;
  quantity?: number;
  unitPrice?: number;
  amount?: number;
  currency?: CurrencyCode;
  feeAmount?: number;
  currencyPerEurRate?: number;
  notes?: string;
  idempotencyKey: string;
}

export interface CreateManualValuationRequest {
  amount: number;
  currency: CurrencyCode;
  asOf: string;
  idempotencyKey: string;
}

export interface ManualMarketQuoteRequest {
  price: number;
  currency: CurrencyCode;
  asOf: string;
  providerSymbol?: string;
  exchange?: string;
  mic?: string;
}

export interface MarketDataStatusDto {
  asOf?: string | null;
  lastRefreshAt?: string | null;
  source?: string | null;
  freshness: FreshnessStatus;
  message?: string | null;
  staleInstrumentIds?: string[];
  missingInstrumentIds?: string[];
  failures?: Array<{ provider: string; subject: string; message: string; isTransient: boolean }>;
}

export interface ContributionPlanLineDto {
  id: string;
  assetClass: AssetClass;
  instrumentId?: string | null;
  instrumentName?: string | null;
  ticker?: string | null;
  nativeCurrency?: CurrencyCode | null;
  currentValueEur: number;
  targetWeight: number;
  recommendedAmountEur: number;
  recommendedNativeAmount?: number | null;
  suggestedQuantity?: number | null;
  unitPrice?: number | null;
  allocationScore?: number | null;
  explanation: string;
  quoteAsOf?: string | null;
  fxAsOf?: string | null;
  freshness: FreshnessStatus;
}

export interface ContributionPlanDto {
  id: string;
  status: ContributionPlanStatus;
  contributionAmountEur: number;
  totalSuggestedEur: number;
  residualAmountEur: number;
  portfolioVersion: number | string;
  strategyVersion: string;
  createdAt: string;
  expiresAt: string;
  classRecommendations: Array<{
    assetClass: AssetClass;
    recommendedAmountEur: number;
  }>;
  lines: ContributionPlanLineDto[];
}

export interface CreateContributionPlanRequest {
  contributionAmountEur: number;
  allowStaleData?: boolean;
}

export interface ContributionExecutionLineRequest {
  planLineId: string;
  instrumentId?: string;
  occurredOn: string;
  actualAmountEur: number;
  actualNativeAmount?: number;
  actualQuantity?: number;
  actualUnitPrice?: number;
  fees?: number;
  currency?: CurrencyCode;
}

export interface ConfirmContributionPlanRequest {
  idempotencyKey: string;
  executions: ContributionExecutionLineRequest[];
}
