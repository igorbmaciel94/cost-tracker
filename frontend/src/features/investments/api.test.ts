import { afterEach, describe, expect, it, vi } from 'vitest';
import { investmentsApi } from './api';

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('investments API adapters', () => {
  it('normalizes the F1 portfolio contract for the UI', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      id: 'portfolio-1',
      baseCurrency: 'EUR',
      version: 3,
      isConfigured: true,
      allocationTargets: [
        { assetClass: 'STOCKS', weight: 0.4 },
        { assetClass: 'REITS', weight: 0.1 },
        { assetClass: 'BRAZIL_FIXED_INCOME', weight: 0.3 },
        { assetClass: 'INTERNATIONAL_FIXED_INCOME', weight: 0.2 },
        { assetClass: 'CRYPTOCURRENCIES', weight: 0 }
      ]
    }), { status: 200, headers: { 'Content-Type': 'application/json' } })));

    const portfolio = await investmentsApi.getPortfolio();

    expect(portfolio.configured).toBe(true);
    expect(portfolio.targets).toHaveLength(5);
    expect(portfolio.targets.at(-1)).toMatchObject({ assetClass: 'CRYPTOCURRENCIES', weight: 0 });
    expect(portfolio.targets.reduce((sum, target) => sum + target.weight, 0)).toBe(1);
  });

  it('normalizes instrument position and manual balance fields', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify([{
      id: 'instrument-1',
      assetClass: 'BRAZIL_FIXED_INCOME',
      kind: 'BOND',
      name: 'CDB',
      nativeCurrency: 'BRL',
      valuationMode: 'MANUAL',
      allocationScore: 0,
      isArchived: false,
      position: {
        quantity: 0,
        currentManualValueNative: 2500,
        currentManualValueAsOf: '2026-08-10',
        netInvestedEur: 350
      }
    }]), { status: 200, headers: { 'Content-Type': 'application/json' } })));

    const instruments = await investmentsApi.getInstruments();

    expect(instruments[0]).toMatchObject({
      instrumentId: 'instrument-1',
      manualBalance: 2500,
      nativeValue: 2500,
      lastValuationAsOf: '2026-08-10',
      contributedEur: 350
    });
  });

  it('keeps quote and exchange-rate provenance used by the valuation', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      id: 'portfolio-1',
      isConfigured: true,
      positions: [{
        instrumentId: 'instrument-1',
        name: 'Example',
        assetClass: 'STOCKS',
        nativeCurrency: 'USD',
        marketData: {
          asOf: '2026-08-17', source: 'TWELVE_DATA', freshness: 'FRESH', price: 42.5,
          currency: 'USD', priceKind: 'CLOSE', providerSymbol: 'EXM', mic: 'XNYS'
        },
        fxData: {
          asOf: '2026-08-18', source: 'ECB', freshness: 'FRESH', rate: 1.17,
          baseCurrency: 'EUR', quoteCurrency: 'USD', rateKind: 'REFERENCE'
        }
      }]
    }), { status: 200, headers: { 'Content-Type': 'application/json' } })));

    const portfolio = await investmentsApi.getPortfolio();

    expect(portfolio.positions[0].marketData).toMatchObject({ source: 'TWELVE_DATA', price: 42.5, providerSymbol: 'EXM' });
    expect(portfolio.positions[0].fxData).toMatchObject({ source: 'ECB', rate: 1.17, baseCurrency: 'EUR', quoteCurrency: 'USD' });
  });

  it('normalizes dividend cash balances with their exchange-rate source', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      totalEur: 10,
      isPartial: false,
      balances: [{
        currency: 'USD', amount: 12, amountEur: 10, lastPaymentDate: '2026-08-18',
        fxData: { asOf: '2026-08-18', source: 'ECB', freshness: 'FRESH', rate: 1.2, baseCurrency: 'EUR', quoteCurrency: 'USD', rateKind: 'REFERENCE' }
      }]
    }), { status: 200, headers: { 'Content-Type': 'application/json' } })));

    const cash = await investmentsApi.getDividendCash();

    expect(cash.totalEur).toBe(10);
    expect(cash.balances[0]).toMatchObject({ currency: 'USD', amount: 12, amountEur: 10 });
    expect(cash.balances[0].fxData).toMatchObject({ source: 'ECB', rate: 1.2 });
  });
});
