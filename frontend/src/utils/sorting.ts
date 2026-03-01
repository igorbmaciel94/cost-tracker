export type SortDirection = 'asc' | 'desc';

export interface SortState<K extends string> {
  key: K;
  direction: SortDirection;
}

export function toggleSort<K extends string>(
  current: SortState<K>,
  nextKey: K,
  firstDirection: SortDirection = 'asc'
): SortState<K> {
  if (current.key !== nextKey) {
    return {
      key: nextKey,
      direction: firstDirection
    };
  }

  return {
    key: nextKey,
    direction: current.direction === 'asc' ? 'desc' : 'asc'
  };
}

export function applyDirection(value: number, direction: SortDirection): number {
  return direction === 'asc' ? value : -value;
}

export function compareStrings(left: string, right: string): number {
  return left.localeCompare(right, 'pt', {
    sensitivity: 'base'
  });
}

export function compareNumbers(left: number, right: number): number {
  return left - right;
}

export function compareDateIso(left: string, right: string): number {
  return left.localeCompare(right);
}

export function sortIndicator<K extends string>(state: SortState<K>, key: K): string {
  if (state.key !== key) {
    return '';
  }

  return state.direction === 'asc' ? '↑' : '↓';
}
