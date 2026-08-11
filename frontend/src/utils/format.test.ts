import { describe, expect, it } from 'vitest';
import { formatCurrency } from './format';

describe('formatCurrency', () => {
  it('formats the requested currency instead of assuming EUR', () => {
    const eur = formatCurrency(1234.5, 'EUR');
    const usd = formatCurrency(1234.5, 'USD');
    const brl = formatCurrency(1234.5, 'BRL');

    expect(eur).not.toBe(usd);
    expect(usd).not.toBe(brl);
    expect(eur).toMatch(/1(?:[.\s])?234,50/);
  });

  it('converts pence to pounds while preserving the GBX source value', () => {
    const formatted = formatCurrency(12_345, 'GBX');
    expect(formatted).toContain('123,45');
    expect(formatted).toContain('GBX');
  });
});
