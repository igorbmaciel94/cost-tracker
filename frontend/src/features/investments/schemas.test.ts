import { describe, expect, it } from 'vitest';
import { decimalTextSchema } from './schemas';

describe('decimalTextSchema', () => {
  it('accepts dot decimals and rejects locale commas', () => {
    const schema = decimalTextSchema();

    expect(schema.safeParse('123.45').success).toBe(true);
    expect(schema.safeParse('.75').success).toBe(true);
    expect(schema.safeParse('123,45').success).toBe(false);
    expect(schema.safeParse('0').success).toBe(false);
  });

  it('supports optional non-negative decimal text', () => {
    const schema = decimalTextSchema({ allowEmpty: true, allowZero: true });

    expect(schema.safeParse('').success).toBe(true);
    expect(schema.safeParse('0').success).toBe(true);
    expect(schema.safeParse('0.00').success).toBe(true);
  });
});
