import type { AssetClass, CurrencyCode, InstrumentKind, InvestableAssetClass } from './types';

export const ASSET_CLASSES: AssetClass[] = [
  'STOCKS',
  'REITS',
  'BRAZIL_FIXED_INCOME',
  'INTERNATIONAL_FIXED_INCOME',
  'CRYPTOCURRENCIES'
];

export const INVESTABLE_ASSET_CLASSES: InvestableAssetClass[] = [
  'STOCKS',
  'REITS',
  'BRAZIL_FIXED_INCOME',
  'INTERNATIONAL_FIXED_INCOME'
];

export const ASSET_CLASS_META: Record<
  AssetClass,
  { label: string; shortLabel: string; color: string; description: string }
> = {
  STOCKS: {
    label: 'Stocks',
    shortLabel: 'Stocks',
    color: '#38bdf8',
    description: 'Ações, ETFs e ADRs negociados em bolsa.'
  },
  REITS: {
    label: 'REITs',
    shortLabel: 'REITs',
    color: '#e879f9',
    description: 'Fundos imobiliários negociados em bolsa.'
  },
  BRAZIL_FIXED_INCOME: {
    label: 'Renda Fixa Brasil',
    shortLabel: 'RF Brasil',
    color: '#fb923c',
    description: 'Saldos resgatáveis informados manualmente, normalmente em BRL.'
  },
  INTERNATIONAL_FIXED_INCOME: {
    label: 'Renda Fixa Internacional',
    shortLabel: 'RF Internacional',
    color: '#a78bfa',
    description: 'Contas remuneradas e títulos com saldo informado manualmente.'
  },
  CRYPTOCURRENCIES: {
    label: 'Criptomoedas',
    shortLabel: 'Cripto',
    color: '#5b8def',
    description: 'Meta percentual de exposição, sem cadastro de ativos ou execução de aportes.'
  }
};

export const CURRENCIES: Array<{ code: CurrencyCode; label: string }> = [
  { code: 'EUR', label: 'Euro (EUR)' },
  { code: 'USD', label: 'Dólar americano (USD)' },
  { code: 'BRL', label: 'Real brasileiro (BRL)' },
  { code: 'GBP', label: 'Libra esterlina (GBP)' },
  { code: 'GBX', label: 'Pence esterlino (GBX)' }
];

export const MARKET_INSTRUMENT_KINDS: InstrumentKind[] = ['STOCK', 'ETF', 'ADR', 'REIT'];

export const PERCENT_TOTAL = 100;

export const DEFAULT_ALLOCATION_PERCENT: Record<AssetClass, number> = {
  STOCKS: 0,
  REITS: 0,
  BRAZIL_FIXED_INCOME: 0,
  INTERNATIONAL_FIXED_INCOME: 0,
  CRYPTOCURRENCIES: 0
};

export function isMarketAssetClass(assetClass: AssetClass) {
  return assetClass === 'STOCKS' || assetClass === 'REITS';
}

export function defaultKindFor(assetClass: InvestableAssetClass): InstrumentKind {
  if (assetClass === 'REITS') return 'REIT';
  if (assetClass === 'STOCKS') return 'STOCK';
  return assetClass === 'BRAZIL_FIXED_INCOME' ? 'BOND' : 'ACCOUNT';
}
