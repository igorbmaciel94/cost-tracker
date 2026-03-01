import { describe, expect, it } from 'vitest';
import { categorySchema, entrySchema, salarySchema } from './validators';

describe('validators', () => {
  it('should reject negative salary', () => {
    const result = salarySchema.safeParse({ salary: -1 });
    expect(result.success).toBe(false);
  });

  it('should reject negative category amount', () => {
    const result = categorySchema.safeParse({
      name: 'Mercado',
      groupName: 'Essenciais',
      plannedAmount: -10
    });

    expect(result.success).toBe(false);
  });

  it('should reject negative entry amount', () => {
    const result = entrySchema.safeParse({
      categoryBudgetId: 'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11',
      entryDate: '2026-02-28',
      description: 'Compra',
      amount: -5
    });

    expect(result.success).toBe(false);
  });
});
