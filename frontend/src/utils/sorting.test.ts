import { describe, expect, it } from 'vitest';
import {
  applyDirection,
  compareDateIso,
  compareNumbers,
  compareStrings,
  sortIndicator,
  toggleSort,
  type SortState
} from './sorting';

describe('sorting utils', () => {
  it('toggles sort direction on same key', () => {
    const initial: SortState<'name' | 'amount'> = { key: 'name', direction: 'asc' };

    const next = toggleSort(initial, 'name');
    expect(next).toEqual({ key: 'name', direction: 'desc' });
  });

  it('resets to first direction on new key', () => {
    const initial: SortState<'name' | 'amount'> = { key: 'name', direction: 'desc' };

    const next = toggleSort(initial, 'amount');
    expect(next).toEqual({ key: 'amount', direction: 'asc' });
  });

  it('compares primitive types correctly', () => {
    expect(compareStrings('Agua', 'Luz')).toBeLessThan(0);
    expect(compareNumbers(20, 10)).toBeGreaterThan(0);
    expect(compareDateIso('2026-03-01', '2026-03-10')).toBeLessThan(0);
  });

  it('applies direction and indicator', () => {
    expect(applyDirection(3, 'asc')).toBe(3);
    expect(applyDirection(3, 'desc')).toBe(-3);

    const state: SortState<'date' | 'name'> = { key: 'date', direction: 'desc' };
    expect(sortIndicator(state, 'date')).toBe('↓');
    expect(sortIndicator(state, 'name')).toBe('');
  });
});
