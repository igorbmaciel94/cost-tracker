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
});
